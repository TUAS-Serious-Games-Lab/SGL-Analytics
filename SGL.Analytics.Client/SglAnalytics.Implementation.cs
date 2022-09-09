using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public partial class SglAnalytics {
		private readonly object lockObject = new object();
		private string appName;
		private string appAPIToken;
		private HttpClient httpClient;
		private SglAnalyticsConfigurator configurator = new SglAnalyticsConfigurator();
		private ICertificateValidator recipientCertificateValidator;
		private RandomGenerator randomGenerator = new RandomGenerator();
		private IRootDataStore rootDataStore;
		private ILogStorage logStorage;
		private ILogCollectorClient logCollectorClient;
		private IUserRegistrationClient userRegistrationClient;

		private LogQueue? currentLogQueue;
		private AsyncConsumerQueue<LogQueue> pendingLogQueues = new AsyncConsumerQueue<LogQueue>();
		private Task? logWriter = null;
		private AsyncConsumerQueue<ILogStorage.ILogFile> uploadQueue = new AsyncConsumerQueue<ILogStorage.ILogFile>();

		private AuthorizationToken? authToken;
		private SynchronizationContext mainSyncContext;
		private string dataDirectory;
		private Task? logUploader = null;

		private ILogger<SglAnalytics> logger;
		private CryptoConfig cryptoConfig;

		/// <inheritdoc/>
		public async ValueTask DisposeAsync() {
			try {
				await FinishAsync();
			}
			catch (OperationCanceledException) { }
			catch (Exception ex) {
				logger.LogError(ex, "Caught error from FinishAsync during disposal.");
			}
			if (configurator.RecipientCertificateValidatorFactory.Dispose) await disposeIfDisposable(recipientCertificateValidator);
			if (configurator.UserRegistrationClientFactory.Dispose) await disposeIfDisposable(userRegistrationClient);
			if (configurator.LogCollectorClientFactory.Dispose) await disposeIfDisposable(logCollectorClient);
			if (configurator.LogStorageFactory.Dispose) await disposeIfDisposable(logStorage);
			if (configurator.RootDataStoreFactory.Dispose) await disposeIfDisposable(rootDataStore);
			configurator.CustomArgumentFactories.Dispose();
			if (configurator.LoggerFactory.Dispose) await disposeIfDisposable(LoggerFactory);
		}

		private async Task disposeIfDisposable(object obj) {
			if (obj is IAsyncDisposable ad) {
				await ad.DisposeAsync().AsTask();
			}
			if (obj is IDisposable d) {
				d.Dispose();
			}
		}

		private class EnumNamingPolicy : JsonNamingPolicy {
			public override string ConvertName(string name) => name;
		}

		private readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions() {
			WriteIndented = true
		};

		private class LogQueue {
			internal AsyncConsumerQueue<LogEntry> entryQueue = new AsyncConsumerQueue<LogEntry>();
			internal Stream writeStream;
			internal ILogStorage.ILogFile logFile;

			public LogQueue(Stream writeStream, ILogStorage.ILogFile logFile) {
				this.writeStream = writeStream;
				this.logFile = logFile;
			}
		}

		private async Task<CertificateStore> loadAuthorizedRecipientCertificatesAsync(IRecipientCertificatesClient client) {
			var store = new CertificateStore(recipientCertificateValidator, NullLogger<CertificateStore>.Instance);
			await client.LoadRecipientCertificatesAsync(appName, appAPIToken, store);
			return store;
		}

		private async Task writePendingLogsAsync() {
			logger.LogDebug("Started log writer to asynchronously flush log entries to disk.");
			var arrOpen = Encoding.UTF8.GetBytes(jsonSerializerOptions.WriteIndented ? ("[" + Environment.NewLine) : "[");
			var arrClose = Encoding.UTF8.GetBytes(jsonSerializerOptions.WriteIndented ? (Environment.NewLine + "]") : "]");
			var delim = Encoding.UTF8.GetBytes(jsonSerializerOptions.WriteIndented ? ("," + Environment.NewLine) : ",");
			await foreach (var logQueue in pendingLogQueues.DequeueAllAsync()) {
				logger.LogDebug("Starting to write entries for data log file {logFile}...", logQueue.logFile.ID);
				var stream = logQueue.writeStream;
				try {
					bool first = true;
					await stream.WriteAsync(arrOpen.AsMemory());
					await foreach (var logEntry in logQueue.entryQueue.DequeueAllAsync()) {
						if (!first) {
							await stream.WriteAsync(delim.AsMemory());
						}
						else {
							first = false;
						}
						await JsonSerializer.SerializeAsync(stream, logEntry, jsonSerializerOptions);
						await stream.FlushAsync();
					}
					await stream.WriteAsync(arrClose.AsMemory());
					await stream.FlushAsync();
				}
				finally { // Need to do this instead of a using block to allow ILogStorage implementations to remove the file behind stream from their 'open for writing'-list in a thread-safe way.
					lock (lockObject) {
						stream.Dispose();
					}
				}
				logger.LogDebug("Finished writing entries for data log file {logFile}, queueing it for upload.", logQueue.logFile.ID);
				uploadQueue.Enqueue(logQueue.logFile);
				startFileUploadingIfNotRunning();
			}
			uploadQueue.Finish();
			logger.LogDebug("Ending log writer because the pending log queue is finished.");
		}

		private void ensureLogWritingActive() {
			lock (lockObject) { // Ensure that only one log writer is active
				if (logWriter is null || logWriter.IsCompleted) {
					// Enforce that the log writer runs on some threadpool thread to avoid putting additional load on app thread.
					logWriter = Task.Run(async () => await writePendingLogsAsync().ConfigureAwait(false));
				}
			}
		}

		private async Task<AuthorizationToken?> loginAsync(bool expired = false) {
			Guid? userIDOpt;
			string? usernameOpt;
			string? userSecret;
			AuthorizationToken? authToken;
			lock (lockObject) {
				userIDOpt = rootDataStore.UserID;
				usernameOpt = rootDataStore.Username;
				userSecret = rootDataStore.UserSecret;
				authToken = this.authToken;
			}
			// Can't login without credentials, the user needs to be registered first.
			if ((userIDOpt == null && usernameOpt == null) || userSecret is null) return null;
			// We have loginData already and we weren't called because of expired loginData, return the already present ones.
			if (authToken != null && !expired) return authToken;
			logger.LogInformation("Logging in user {userId} ...", userIDOpt?.ToString() ?? usernameOpt);
			var tcs = new TaskCompletionSource<AuthorizationToken>();
			mainSyncContext.Post(async s => {
				try {
					LoginRequestDTO loginDTO;
					if (userIDOpt != null) {
						loginDTO = new IdBasedLoginRequestDTO(appName, appAPIToken, userIDOpt.Value, userSecret);
					}
					else if (usernameOpt != null) {
						loginDTO = new UsernameBasedLoginRequestDTO(appName, appAPIToken, usernameOpt, userSecret);
					}
					else {
						throw new Exception("UserId and Username are both missing although one of them was present before switching to main context.");
					}
					Validator.ValidateObject(loginDTO, new ValidationContext(loginDTO), true);
					tcs.SetResult(await userRegistrationClient.LoginUserAsync(loginDTO));
				}
				catch (Exception ex) {
					tcs.SetException(ex);
				}
			}, null);
			try {
				authToken = await tcs.Task;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Login for user {userId} failed with exception.", userIDOpt?.ToString() ?? usernameOpt);
				throw;
			}
			lock (lockObject) {
				this.authToken = authToken;
			}
			logger.LogInformation("Login was successful.");
			return authToken;
		}

		private async Task uploadFilesAsync() {
			if (!logCollectorClient.IsActive) return;
			try {
				var authToken = await loginAsync();
			}
			catch (LoginFailedException ex) {
				logger.LogError(ex, "The login attempt failed due to incorrect credentials.");
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "The login attempt failed due to an error. Exiting the upload process ...");
				return;
			}
			if (authToken == null) {
				logger.LogError("The registered login credentails are missing. This is unexpected at this point. Exiting the upload process ...");
				return;
			}
			logger.LogDebug("Started log uploader to asynchronously upload finished data logs to the backend.");
			var completedLogFiles = new HashSet<Guid>();
			var recipientCertificates = await loadAuthorizedRecipientCertificatesAsync(logCollectorClient);
			var certList = recipientCertificates.ListKnownKeyIdsAndPublicKeys().ToList();
			if (!certList.Any()) {
				logger.LogError("Can't upload log files because no authorized recipients were found.");
				return;
			}
			var keyEncryptor = new KeyEncryptor(certList, randomGenerator, cryptoConfig.AllowSharedMessageKeyPair);
			await foreach (var logFile in uploadQueue.DequeueAllAsync()) {
				// If we already completed this file, it has been added to the queue twice,
				// e.g. once by the writer worker and once by startUploadingExistingLogs.
				// Since we removed the file after successfully uploading it, lets not try again, only to fail with a missing file exception.
				if (completedLogFiles.Contains(logFile.ID)) continue;
				try {
					await attemptToUploadFileAsync((AuthorizationToken)authToken, logFile, keyEncryptor);
				}
				catch (LoginRequiredException) {
					logger.LogInformation("Uploading data log {logId} failed because the backend told us that we need to login first. " +
						"The most likely reason is that our session token expired. Obtaining a new session token by logging in again, after which we will retry the upload ...", logFile.ID);
					try {
						authToken = await loginAsync(true);
					}
					catch (LoginFailedException ex) {
						logger.LogError(ex, "The login attempt failed due to incorrect credentials.");
						throw;
					}
					catch (Exception ex) {
						logger.LogError(ex, "The login attempt failed due to an error. Exiting the upload process ...");
						return;
					}
					if (authToken == null) {
						logger.LogError("The registered login credentails are missing. This is unexpected at this point. Exiting the upload process ...");
						return;
					}
					try {
						await attemptToUploadFileAsync((AuthorizationToken)authToken, logFile, keyEncryptor);
					}
					catch (LoginRequiredException ex) {
						logger.LogError(ex, "The upload for data log {logId} failed again after obtaining a fresh session token. " +
							"There seems to be a permission problem in the backend. Exiting the upload process ...", logFile.ID);
						return;
					}
				}
				completedLogFiles.Add(logFile.ID);
			}

			async Task attemptToUploadFileAsync(AuthorizationToken authToken, ILogStorage.ILogFile logFile, KeyEncryptor keyEncryptor) {
				bool removing = false;
				try {
					logger.LogDebug("Uploading data log file {logFile}...", logFile.ID);
					Guid logFileID;
					DateTime logFileCreationTime;
					DateTime logFileEndTime;
					string logFileSuffix;
					LogContentEncoding logFileEncoding;
					Stream contentStream = Stream.Null;
					try {
						lock (lockObject) { // Access to the log file object needs to be done under lock.
							logFileID = logFile.ID;
							logFileCreationTime = logFile.CreationTime;
							logFileEndTime = logFile.EndTime;
							logFileSuffix = logFile.Suffix;
							logFileEncoding = logFile.Encoding;
							contentStream = logFile.OpenReadRaw();
						}
						var dataEncryptor = new DataEncryptor(randomGenerator, numberOfStreams: 1);
						var encryptionStream = dataEncryptor.OpenEncryptionReadStream(contentStream, 0, leaveOpen: false);
						var encryptionInfo = dataEncryptor.GenerateEncryptionInfo(keyEncryptor);
						var metadataDTO = new LogMetadataDTO(logFileID, logFileCreationTime, logFileEndTime, logFileSuffix, logFileEncoding, encryptionInfo);
						Validator.ValidateObject(metadataDTO, new ValidationContext(metadataDTO), true);
						await logCollectorClient.UploadLogFileAsync(appName, appAPIToken, authToken, metadataDTO, encryptionStream);
					}
					finally {
						await contentStream.DisposeAsync();
					}
					removing = true;
					lock (lockObject) { // ILogStorage implementations may need to do this under lock.
						logFile.Remove();
					}
					logger.LogDebug("Successfully uploaded data log file {logFile}.", logFile.ID);
				}
				catch (LoginRequiredException) {
					throw;
				}
				catch (UnauthorizedException ex) {
					logger.LogError(ex, "The upload failed due to an authorization error.");
				}
				catch (FileTooLargeException) {
					// TODO: Find a better way to handle log files that are too large to upload.
					// Leaving the file in storage whould imply that it is retried later, which would waste user's bandwidth only to fail again, unless the server-side limit was increased.
					// Maybe, we could store it locally, in a separate folder (or similar) for potential manual troubleshooting.
					logger.LogError("Uploading data log {logId} failed because it was too large, it will be removed, because retrying a too large file would waste bandwidth, just to fail again.", logFile.ID);
					logFile.Remove();
				}
#if NET5_0_OR_GREATER
				catch (HttpRequestException ex) when (ex.StatusCode is not null) {
					logger.LogError("Uploading data log {logId} failed with status code {statusCode}. It will be retried at next startup or on explicit retry.", logFile.ID, ex.StatusCode);
				}
#endif
				catch (HttpRequestException ex) {
					logger.LogError("Uploading data log {logId} failed with message \"{message}\". It will be retried at next startup or explicit retry.", logFile.ID, ex.Message);
				}
				catch (Exception ex) when (!removing) {
					logger.LogError(ex, "Uploading data log {logId} failed with an unexpected exception. It will be retried at next startup or explicit retry.", logFile.ID);
				}
				catch (Exception ex) {
					logger.LogError(ex, "Removing data log {logId} failed with an unexpected exception.", logFile.ID);
				}
			}
		}

		private void startFileUploadingIfNotRunning() {
			if (!logCollectorClient.IsActive) return;
			if (!IsRegistered()) return; // IsRegistered does it's own locking
			lock (lockObject) { // Ensure that only one log uploader is active
				if (logUploader is null || logUploader.IsCompleted) {
					// Enforce that the uploader runs on some threadpool thread to avoid putting additional load on app thread.
					logUploader = Task.Run(async () => await uploadFilesAsync().ConfigureAwait(false));
				}
			}
		}

		private void startUploadingExistingLogs() {
			if (!logCollectorClient.IsActive) return;
			if (!IsRegistered()) return;
			List<ILogStorage.ILogFile> existingCompleteLogs;
			lock (lockObject) {
				existingCompleteLogs = logStorage.EnumerateFinishedLogs().ToList();
			}
			if (existingCompleteLogs.Count == 0) return;
			logger.LogDebug("Queueing existing data log files for upload...");
			foreach (var logFile in existingCompleteLogs) {
				uploadQueue.Enqueue(logFile);
			}
			startFileUploadingIfNotRunning();
		}

		private async Task<(byte[] EncryptedUserProperties, EncryptionInfo EncryptionInfo)> encryptUserProperties(
				Dictionary<string, object?> properties, KeyEncryptor keyEncryptor, CancellationToken ct = default) {
			var dataEncryptor = new DataEncryptor(randomGenerator, 1);
			using var encryptedPropsBuffer = new MemoryStream();
			await using (var encryptionStream = dataEncryptor.OpenEncryptionWriteStream(encryptedPropsBuffer, 0, leaveOpen: true)) {
				var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
					WriteIndented = true,
					DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
				};
				await using var compressionStream = new GZipStream(encryptionStream, CompressionLevel.Optimal);
				options.Converters.Add(new ObjectDictionaryJsonConverter());
				await JsonSerializer.SerializeAsync(compressionStream, properties, options, ct);
			}
			return (encryptedPropsBuffer.ToArray(), dataEncryptor.GenerateEncryptionInfo(keyEncryptor));
		}
	}
}
