using Microsoft.Extensions.Logging;
using SGL.Analytics.DTO;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public partial class SGLAnalytics {
		private readonly object lockObject = new object();
		private string appName;
		private string appAPIToken;
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

		private Task? logUploader = null;

		private ILogger<SGLAnalytics> logger;

		private class EnumNamingPolicy : JsonNamingPolicy {
			public override string ConvertName(string name) => name;
		}

		private readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions() {
			WriteIndented = true,
			Converters = { new JsonStringEnumConverter(new EnumNamingPolicy()) }
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
			logger.LogInformation("Logging in user {userId} ...", userIDOpt);
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
				logger.LogError(ex, "Login for user {userId} failed with exception.", userIDOpt);
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
			await foreach (var logFile in uploadQueue.DequeueAllAsync()) {
				// If we already completed this file, it has been added to the queue twice,
				// e.g. once by the writer worker and once by startUploadingExistingLogs.
				// Since we removed the file after successfully uploading it, lets not try again, only to fail with a missing file exception.
				if (completedLogFiles.Contains(logFile.ID)) continue;
				try {
					await attemptToUploadFileAsync((AuthorizationToken)authToken, logFile);
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
						await attemptToUploadFileAsync((AuthorizationToken)authToken, logFile);
					}
					catch (LoginRequiredException ex) {
						logger.LogError(ex, "The upload for data log {logId} failed again after obtaining a fresh session token. " +
							"There seems to be a permission problem in the backend. Exiting the upload process ...", logFile.ID);
						return;
					}
				}
				completedLogFiles.Add(logFile.ID);
			}

			async Task attemptToUploadFileAsync(AuthorizationToken authToken, ILogStorage.ILogFile logFile) {
				bool removing = false;
				try {
					logger.LogDebug("Uploading data log file {logFile}...", logFile.ID);
					Task uploadTask;
					lock (lockObject) { // At the beginning, of the upload task, the stream for the log file needs to be aquired under lock.
						uploadTask = logCollectorClient.UploadLogFileAsync(appName, appAPIToken, authToken, logFile);
					}
					await uploadTask;
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
				catch (HttpRequestException ex) when (ex.StatusCode is not null) {
					logger.LogError("Uploading data log {logId} failed with status code {statusCode}. It will be retried at next startup or on explicit retry.", logFile.ID, ex.StatusCode);
				}
				catch (HttpRequestException ex) {
					logger.LogError("Uploading data log {logId} failed with message \"{message}\". It will be retried at next startup on explicit retry.", logFile.ID, ex.Message);
				}
				catch (Exception ex) when (!removing) {
					logger.LogError(ex, "Uploading data log {logId} failed with an unexpected exception. It will be retried at next startup on explicit retry.", logFile.ID);
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
	}
}
