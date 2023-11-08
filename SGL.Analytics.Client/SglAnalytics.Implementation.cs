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
		private Guid? loggedInUserId;
		private AuthorizationData? sessionAuthorization = null;
		private HttpClient httpClient;
		private SglAnalyticsConfigurator configurator = new SglAnalyticsConfigurator();
		private ICertificateValidator recipientCertificateValidator;
		private RandomGenerator randomGenerator = new RandomGenerator();
		private IRootDataStore rootDataStore;
		private ILogStorage anonymousLogStorage;
		private ILogStorage userLogStorage = null!;
		private ILogStorage currentLogStorage = null!;
		private ILogCollectorClient logCollectorClient;
		private IUserRegistrationClient userRegistrationClient;

		private LogQueue? currentLogQueue;
		private AsyncConsumerQueue<LogQueue> pendingLogQueues = new AsyncConsumerQueue<LogQueue>();
		private bool disableLogWriting = false;
		private Task? logWriter = null;
		private AsyncConsumerQueue<ILogStorage.ILogFile> uploadQueue = new AsyncConsumerQueue<ILogStorage.ILogFile>();

		private Func<CancellationToken, Task<LoginResponseDTO>> refreshLoginDelegate =
			ct => Task.FromException<LoginResponseDTO>(new InvalidOperationException("Attempt to refresh login without previous login."));
		private SynchronizationContext mainSyncContext;
		private CancellationTokenSource cts = new CancellationTokenSource();
		private string dataDirectory;
		private bool disableLogUploading = false;
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
			if (configurator.AnonymousLogStorageFactory.Dispose) await disposeIfDisposable(anonymousLogStorage);
			if (configurator.UserLogStorageFactory.Dispose) await disposeIfDisposable(userLogStorage);
			if (configurator.RootDataStoreFactory.Dispose) await disposeIfDisposable(rootDataStore);
			configurator.CustomArgumentFactories.Dispose();
			cts.Cancel();
			cts.Dispose();
			if (configurator.LoggerFactory.Dispose) await disposeIfDisposable(LoggerFactory);
			CurrentClientMode = SglAnalyticsClientMode.Disposed;
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

		private readonly JsonSerializerOptions logJsonOptions = new JsonSerializerOptions(JsonOptions.LogEntryOptions);
		private readonly JsonSerializerOptions userPropertiesJsonOptions = new JsonSerializerOptions(JsonOptions.UserPropertiesOptions);

		private AuthorizationData? SessionAuthorization {
			get {
				lock (lockObject) {
					return sessionAuthorization;
				}
			}
			set {
				lock (lockObject) {
					sessionAuthorization = value;
				}
			}
		}
		private bool SessionAuthorizationValid {
			get {
				lock (lockObject) {
					return sessionAuthorization.HasValue && sessionAuthorization.Value.Valid;
				}
			}
		}

		private class LogQueue {
			internal AsyncConsumerQueue<LogEntry> entryQueue = new AsyncConsumerQueue<LogEntry>();
			internal Stream writeStream;
			internal ILogStorage.ILogFile logFile;

			public LogQueue(Stream writeStream, ILogStorage.ILogFile logFile) {
				this.writeStream = writeStream;
				this.logFile = logFile;
			}
		}

		private async Task<(Dictionary<string, object?> unencryptedUserPropDict, byte[]? encryptedUserProps, EncryptionInfo? userPropsEncryptionInfo)> getUserProperties(BaseUserData userData) {
			var ct = cts.Token;
			var (unencryptedUserPropDict, encryptedUserPropDict) = userData.BuildUserProperties();
			byte[]? encryptedUserProps = null;
			EncryptionInfo? userPropsEncryptionInfo = null;
			if (encryptedUserPropDict.Any()) {
				var recipientCertificates = await loadAuthorizedRecipientCertificatesAsync(userRegistrationClient, ct);
				var certList = recipientCertificates.ListKnownKeyIdsAndPublicKeys().ToList();
				if (!certList.Any()) {
					const string msg = "Can't send registration because no authorized recipients for study-specific properties were found.";
					logger.LogError(msg);
					throw new InvalidOperationException(msg);
				}
				var keyEncryptor = new KeyEncryptor(certList, randomGenerator, cryptoConfig.AllowSharedMessageKeyPair);
				(encryptedUserProps, userPropsEncryptionInfo) = await encryptUserProperties(encryptedUserPropDict, keyEncryptor, ct);
			}
			return (unencryptedUserPropDict, encryptedUserProps, userPropsEncryptionInfo);
		}

		private async Task storeCredentialsAsync(string? username, string secret, Guid? userId) {
			lock (lockObject) {
				rootDataStore.UserID = userId;
				rootDataStore.UserSecret = secret;
				if (username != null) {
					rootDataStore.Username = username;
				}
			}
			await rootDataStore.SaveAsync();
		}

		private async Task<CertificateStore> loadAuthorizedRecipientCertificatesAsync(IRecipientCertificatesClient client, CancellationToken ct = default) {
			var store = new CertificateStore(recipientCertificateValidator, LoggerFactory.CreateLogger<CertificateStore>(), (cert, logger) => {
				if (!cert.AllowedKeyUsages.HasValue) {
					logger.LogError("Recipient certificate with SubjectDN={subjDN} and KeyId={keyId} doesn't have allowed key usages specified. " +
						"Valid certifiactes for end-to-end encryption must have a key usage extension including key encipherment. " +
						"The recipient is therefore rejected.", cert.SubjectDN, cert.PublicKey.CalculateId());
					return false;
				}
				else if (!cert.AllowedKeyUsages.Value.HasFlag(KeyUsages.KeyEncipherment)) {
					logger.LogError("Recipient certificate with SubjectDN={subjDN} and KeyId={keyId} has allowed key usaged specified " +
						"but they don't include the key encipherment usage required for end-to-end encryption. " +
						"The recipient is therefore rejected.", cert.SubjectDN, cert.PublicKey.CalculateId());
					return false;
				}
				return true;
			});
			await client.LoadRecipientCertificatesAsync(store, ct);
			return store;
		}

		private async Task writePendingLogsAsync() {
			logger.LogDebug("Started log writer to asynchronously flush log entries to disk.");
			var arrOpen = Encoding.UTF8.GetBytes(logJsonOptions.WriteIndented ? ("[" + Environment.NewLine) : "[");
			var arrClose = Encoding.UTF8.GetBytes(logJsonOptions.WriteIndented ? (Environment.NewLine + "]") : "]");
			var delim = Encoding.UTF8.GetBytes(logJsonOptions.WriteIndented ? ("," + Environment.NewLine) : ",");
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
						try {
							await JsonSerializer.SerializeAsync(stream, logEntry, logJsonOptions);
						}
						catch (Exception ex) {
							logger.LogError(ex, "Couldn't write log entry to stream due to exception while serializing.");
						}
						await stream.FlushAsync();
					}
					await stream.WriteAsync(arrClose.AsMemory());
					await stream.FlushAsync();
				}
				catch (Exception ex) {
					logger.LogError(ex, "Unrecoverable error while writing entries to log file {id}.", logQueue.logFile.ID);
					try {
						await stream.FlushAsync();
					}
					catch { }
				}
				finally { // Need to do this instead of a using block to allow ILogStorage implementations to remove the file behind stream from their 'open for writing'-list in a thread-safe way.
					lock (lockObject) {
						stream.Dispose();
					}
				}
				await currentLogStorage.FinishLogFileAsync(logQueue.logFile);
				lock (lockObject) {
					if (disableLogUploading) {
						logger.LogDebug("Finished writing entries for data log file {logFile}, but skipping upload, because uploading is disabled.", logQueue.logFile.ID);
						continue;
					}
				}
				logger.LogDebug("Finished writing entries for data log file {logFile}, queueing it for upload.", logQueue.logFile.ID);
				uploadQueue.Enqueue(logQueue.logFile);
				startFileUploadingIfNotRunning();
			}
			uploadQueue.Finish();
			logger.LogDebug("Ending log writer because the pending log queue is finished.");
		}

		private void startLogWritingIfNotRunning() {
			lock (lockObject) { // Ensure that only one log writer is active
				if (disableLogWriting) {
					return;
				}
				if (logWriter is null || logWriter.IsCompleted) {
					// Enforce that the log writer runs on some threadpool thread to avoid putting additional load on app thread.
					logWriter = Task.Run(async () => await writePendingLogsAsync().ConfigureAwait(false));
				}
			}
		}

		private async Task<LoginResponseDTO> loginAsync(LoginRequestDTO loginDTO, CancellationToken ct = default) {
			try {
				if (string.IsNullOrEmpty(loginDTO.UserSecret)) {
					throw new ArgumentNullException(nameof(loginDTO.UserSecret));
				}
				switch (loginDTO) {
					case IdBasedLoginRequestDTO:
						break;
					case UsernameBasedLoginRequestDTO usernameBasedLoginRequestDTO:
						if (string.IsNullOrEmpty(usernameBasedLoginRequestDTO.Username)) {
							throw new ArgumentNullException(nameof(BaseUserData.Username));
						}
						break;
					default:
						throw new ArgumentException("Unsupported login DTO object type.", nameof(loginDTO));
				}
				logger.LogInformation("Logging in user {userIdent} ...", loginDTO.GetUserIdentifier());
				Validator.ValidateObject(loginDTO, new ValidationContext(loginDTO), true);
				var response = await userRegistrationClient.LoginUserAsync(loginDTO, ct);
				logger.LogInformation("Login was successful.");
				return response;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Login for user {userIdent} failed with exception.", loginDTO.GetUserIdentifier());
				throw;
			}
		}

		private (Guid? UserId, string? Username, string? UserSecret) readStoredCredentials() {
			Guid? userIDOpt;
			string? usernameOpt;
			string? userSecret;
			lock (lockObject) {
				userIDOpt = rootDataStore.UserID;
				usernameOpt = rootDataStore.Username;
				userSecret = rootDataStore.UserSecret;
			}
			return (userIDOpt, usernameOpt, userSecret);
		}

		private async Task uploadFilesAsync() {
			var ct = cts.Token;
			if (!logCollectorClient.IsActive) return;
			try {
				if (!SessionAuthorizationValid) {
					await refreshLoginDelegate(ct);
				}
			}
			catch (LoginFailedException ex) {
				logger.LogError(ex, "The login attempt failed due to incorrect credentials.");
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "The login attempt failed due to an error. Exiting the upload process ...");
				return;
			}
			if (!SessionAuthorizationValid) {
				logger.LogError("No valid authorization token is present. This is unexpected at this point. Exiting the upload process ...");
				return;
			}
			logger.LogDebug("Started log uploader to asynchronously upload finished data logs to the backend.");
			var completedLogFiles = new HashSet<Guid>();
			var recipientCertificates = await loadAuthorizedRecipientCertificatesAsync(logCollectorClient, ct);
			var certList = recipientCertificates.ListKnownKeyIdsAndPublicKeys().ToList();
			if (!certList.Any()) {
				logger.LogError("Can't upload log files because no authorized recipients were found.");
				return;
			}
			var keyEncryptor = new KeyEncryptor(certList, randomGenerator, cryptoConfig.AllowSharedMessageKeyPair);
			await foreach (var logFile in uploadQueue.DequeueAllAsync(ct)) {
				// If we already completed this file, it has been added to the queue twice,
				// e.g. once by the writer worker and once by startUploadingExistingLogs.
				// Since we removed the file after successfully uploading it, lets not try again, only to fail with a missing file exception.
				if (completedLogFiles.Contains(logFile.ID)) continue;
				try {
					await attemptToUploadFileAsync(logFile, keyEncryptor, ct);
				}
				catch (LoginRequiredException) {
					logger.LogInformation("Uploading data log {logId} failed because the backend told us that we need to login first. " +
						"The most likely reason is that our session token expired. Obtaining a new session token by logging in again, after which we will retry the upload ...", logFile.ID);
					try {
						await refreshLoginDelegate(ct);
					}
					catch (LoginFailedException ex) {
						logger.LogError(ex, "The login attempt failed due to incorrect credentials.");
						throw;
					}
					catch (Exception ex) {
						logger.LogError(ex, "The login attempt failed due to an error. Exiting the upload process ...");
						return;
					}
					if (!SessionAuthorizationValid) {
						logger.LogError("No valid authorization token is present. This is unexpected at this point. Exiting the upload process ...");
						return;
					}
					try {
						await attemptToUploadFileAsync(logFile, keyEncryptor, ct);
					}
					catch (LoginRequiredException ex) {
						logger.LogError(ex, "The upload for data log {logId} failed again after obtaining a fresh session token. " +
							"There seems to be a permission problem in the backend. Exiting the upload process ...", logFile.ID);
						return;
					}
				}
				completedLogFiles.Add(logFile.ID);
			}

			async Task attemptToUploadFileAsync(ILogStorage.ILogFile logFile, KeyEncryptor keyEncryptor, CancellationToken ct = default) {
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
							contentStream = logFile.OpenReadEncoded();
						}
						var dataEncryptor = new DataEncryptor(randomGenerator, numberOfStreams: 1);
						var encryptionStream = dataEncryptor.OpenEncryptionReadStream(contentStream, 0, leaveOpen: false);
						var encryptionInfo = dataEncryptor.GenerateEncryptionInfo(keyEncryptor);
						var metadataDTO = new LogMetadataDTO(logFileID, logFileCreationTime, logFileEndTime, logFileSuffix, logFileEncoding, encryptionInfo);
						Validator.ValidateObject(metadataDTO, new ValidationContext(metadataDTO), true);
						await logCollectorClient.UploadLogFileAsync(metadataDTO, encryptionStream, ct);
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
			lock (lockObject) { // Ensure that only one log uploader is active
				if (disableLogUploading) return;
				if (logUploader is null || logUploader.IsCompleted) {
					// Enforce that the uploader runs on some threadpool thread to avoid putting additional load on app thread.
					logUploader = Task.Run(async () => await uploadFilesAsync().ConfigureAwait(false));
				}
			}
		}

		private void startUploadingExistingLogs() {
			if (!logCollectorClient.IsActive) return;
			IList<ILogStorage.ILogFile> existingCompleteLogs;
			lock (lockObject) {
				if (disableLogUploading) return;
				existingCompleteLogs = currentLogStorage.ListLogs();
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
				await using var compressionStream = new GZipStream(encryptionStream, CompressionLevel.Optimal, leaveOpen: true);
				await JsonSerializer.SerializeAsync(compressionStream, properties, userPropertiesJsonOptions, ct);
			}
			return (encryptedPropsBuffer.ToArray(), dataEncryptor.GenerateEncryptionInfo(keyEncryptor));
		}

		private async Task performRecoveryForUnfinishedLogs(ILogStorage storage, CancellationToken ct = default) {
			try {
				IList<ILogStorage.ILogFile> unfinishedLogs;
				lock (lockObject) {
					unfinishedLogs = storage.ListUnfinishedLogsForRecovery();
				}
				if (unfinishedLogs.Count == 0) {
					return;
				}
				logger.LogInformation("Performing recovery for perviously unfinished logs...");
				foreach (var log in unfinishedLogs) {
					ct.ThrowIfCancellationRequested();
					logger.LogDebug("Finishing log file {logId} ...", log.ID);
					await storage.FinishLogFileAsync(log, ct);
				}
				logger.LogInformation("... recovery procedure complete.");
			}
			catch (OperationCanceledException) {
				logger.LogInformation("... recovery operation cancelled.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "Recovery operation failed.");
			}
		}
	}
}
