using Microsoft.Extensions.Logging;
using SGL.Analytics.DTO;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	[AttributeUsage(AttributeTargets.Class)]
	public class EventTypeAttribute : Attribute {
		public string EventTypeName { get; private set; }
		public EventTypeAttribute(string eventTypeName) {
			EventTypeName = eventTypeName;
		}
	}

	public class SGLAnalytics {
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

		private LoginResponseDTO? loginData;
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

		private class AsyncConsumerQueue<T> {
			private Channel<T> channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false });
			public void Enqueue(T item) {
				if (!channel.Writer.TryWrite(item)) {
					throw new InvalidOperationException("Can't enqueue to this queue object because it is already finished.");
				}
			}
			public IAsyncEnumerable<T> DequeueAllAsync() {
				return channel.Reader.ReadAllAsync();
			}
			public void Finish() {
				channel.Writer.Complete();
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
				if (logWriter is null) {
					// Enforce that the log writer runs on some threadpool thread to avoid putting additional load on app thread.
					logWriter = Task.Run(async () => await writePendingLogsAsync().ConfigureAwait(false));
				}
			}
		}

		private async Task<(Guid? userId, LoginResponseDTO? loginData)> ensureLogedInAsync(bool expired = false) {
			Guid? userIDOpt;
			string? userSecret;
			LoginResponseDTO? loginData;
			lock (lockObject) {
				userIDOpt = rootDataStore.UserID;
				userSecret = rootDataStore.UserSecret;
				loginData = this.loginData;
			}
			// Can't login without credentials, the user needs to be registered first.
			if (userIDOpt is null || userSecret is null) return (null, null);
			// We have loginData already and we weren't called because of expired loginData, return the already present ones.
			if (loginData is not null && !expired) return (userIDOpt, loginData);
			logger.LogInformation("Logging in user {userId} ...", userIDOpt);
			var tcs = new TaskCompletionSource<LoginResponseDTO>();
			mainSyncContext.Post(async s => {
				try {
					tcs.SetResult(await userRegistrationClient.LoginUserAsync(new LoginRequestDTO(appName, appAPIToken, userIDOpt.Value, userSecret)));
				}
				catch (Exception ex) {
					tcs.SetException(ex);
				}
			}, null);
			try {
				loginData = await tcs.Task;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Login for user {userId} failed with exception.", userIDOpt);
				throw;
			}
			lock (lockObject) {
				this.loginData = loginData;
			}
			logger.LogInformation("Login was successful.");
			return (userIDOpt, loginData);
		}

		private async Task uploadFilesAsync() {
			if (!logCollectorClient.IsActive) return;
			(Guid? userIDOpt, LoginResponseDTO? loginData) = await ensureLogedInAsync();
			if (userIDOpt is null || loginData is null) return;
			logger.LogDebug("Started log uploader to asynchronously upload finished data logs to the backend.");
			var userID = (Guid)userIDOpt;
			await foreach (var logFile in uploadQueue.DequeueAllAsync()) {
				try {
					await attemptToUploadFileAsync(loginData, userID, logFile);
				}
				catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) {
					logger.LogWarning("Uploading data log {logId} failed with 'Unauthorized' error. " +
						"The most likely reason is that the session token expired. Obtaining a new session token by logging in again, retrying the upload afterwards...", logFile.ID);
					try {
						(userIDOpt, loginData) = await ensureLogedInAsync();
					}
					catch (Exception ex2) {
						logger.LogError(ex2, "The login attempt failed. Exiting the upload process ...");
						return;
					}
					if (userIDOpt is null || loginData is null) {
						logger.LogError("The registered login credentails are missing. This is unexpected. Exiting the upload process ...");
						return;
					}
					try {
						await attemptToUploadFileAsync(loginData, userID, logFile);
					}
					catch (HttpRequestException ex3) when (ex.StatusCode == HttpStatusCode.Unauthorized) {
						logger.LogError(ex3, "The upload for data log {logId} failed again after obtaining a fresh session token. " +
							"There seems to be a permission problem in the backend. Exiting the upload process ...", logFile.ID);
						return;
					}
				}
			}

			async Task attemptToUploadFileAsync(LoginResponseDTO loginData, Guid userID, ILogStorage.ILogFile logFile) {
				bool removing = false;
				try {
					logger.LogDebug("Uploading data log file {logFile}...", logFile.ID);
					Task uploadTask;
					lock (lockObject) { // At the beginning, of the upload task, the stream for the log file needs to be aquired under lock.
						uploadTask = logCollectorClient.UploadLogFileAsync(appName, appAPIToken, userID, loginData, logFile);
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
					logger.LogError("Uploading data log {logId} failed with status code {statusCode}. It will be retried at next startup.", logFile.ID, ex.StatusCode);
				}
				catch (HttpRequestException ex) {
					logger.LogError("Uploading data log {logId} failed with message \"{message}\". It will be retried at next startup.", logFile.ID, ex.Message);
				}
				catch (Exception ex) when (!removing) {
					logger.LogError("Uploading data log {logId} failed with an unexpected exception with message \"{message}\". It will be retried at next startup.", logFile.ID, ex.Message);
				}
				catch (Exception ex) {
					logger.LogError("Removing data log {logId} failed with an unexpected exception with message \"{message}\".", logFile.ID, ex.Message);
				}
			}
		}

		private void startFileUploadingIfNotRunning() {
			if (!logCollectorClient.IsActive) return;
			if (!IsRegistered()) return; // IsRegistered does it's own locking
			lock (lockObject) { // Ensure that only one log uploader is active
				if (logUploader is null) {
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

		private class NoOpLogger : ILogger<SGLAnalytics> {
			private class DummyScope : IDisposable {
				public void Dispose() { }
			}
			public IDisposable BeginScope<TState>(TState state) {
				return new DummyScope();
			}

			public bool IsEnabled(LogLevel logLevel) {
				return false;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
			}
		}

		public SGLAnalytics(string appName, string appAPIToken, IRootDataStore? rootDataStore = null, ILogStorage? logStorage = null, ILogCollectorClient? logCollectorClient = null, IUserRegistrationClient? userRegistrationClient = null, ILogger<SGLAnalytics>? diagnosticsLogger = null) {
			// Capture the SynchronizationContext of the 'main' thread, so we can perform tasks that need to run there by Post()ing to the context.
			mainSyncContext = SynchronizationContext.Current ?? new SynchronizationContext();
			this.appName = appName;
			this.appAPIToken = appAPIToken;
			if (diagnosticsLogger is null) diagnosticsLogger = new NoOpLogger();
			logger = diagnosticsLogger;
			if (rootDataStore is null) rootDataStore = new FileRootDataStore(appName);
			this.rootDataStore = rootDataStore;
			if (logStorage is null) logStorage = new DirectoryLogStorage(Path.Combine(rootDataStore.DataDirectory, "DataLogs"));
			this.logStorage = logStorage;
			if (logCollectorClient is null) logCollectorClient = new LogCollectorRestClient();
			this.logCollectorClient = logCollectorClient;
			if (userRegistrationClient is null) userRegistrationClient = new UserRegistrationRestClient();
			this.userRegistrationClient = userRegistrationClient;
			if (IsRegistered()) {
				startUploadingExistingLogs();
			}
		}

		public int UserRegistrationSecretLength { get; set; } = 16;
		public string AppName { get => appName; }

		/// <summary>
		/// Checks if the user registration for this client was already done.
		/// If this returns false, call RegisterAsync and ensure the registration before relying on logs being uploaded.
		/// When logs are recorded on an unregistered client, they are stored locally and are not uploaded until the registration is completed and a user id is obtained.
		/// </summary>
		/// <returns>true if the client is already registered, false if the registration is not yet done.</returns>
		public bool IsRegistered() {
			lock (lockObject) {
				return rootDataStore.UserID is not null;
			}
		}

		/// <summary>
		/// Registers the user with the given data in the backend database, obtains a user id and stores it locally on the client using the configured rootDataStore for future use.
		/// </summary>
		/// <param name="userData">The user data for the registration, that is to be sent to the server.</param>
		/// <returns>A Task representing the registration operation. Wait for it's completion before relying on logs being uploaded. Logs recorded on a client that hasn't completed registration are stored only locally until the registration is complete and the user id required for the upload is obtained.</returns>
		/// <remarks>
		/// Other state-changing operations (<c>StartNewLog</c>, <c>RegisterAsync</c>, <c>FinishAsync</c>, or the <c>Record</c>... operations) on the current object must not be called, between start and completion of this operation.
		/// </remarks>
		public async Task RegisterAsync(BaseUserData userData) {
			try {
				if (IsRegistered()) {
					throw new InvalidOperationException("User is already registered.");
				}
				logger.LogInformation("Starting user registration process...");
				var secret = SecretGenerator.Instance.GenerateSecret(UserRegistrationSecretLength);
				var userDTO = userData.MakeDTO(appName, secret);
				var regResult = await userRegistrationClient.RegisterUserAsync(userDTO, appAPIToken);
				logger.LogInformation("Registration with backend succeeded. Got user id {userId}. Proceeding to store user id locally...", regResult.UserId);
				lock (lockObject) {
					rootDataStore.UserID = regResult.UserId;
					rootDataStore.UserSecret = secret;
				}
				await rootDataStore.SaveAsync();
				logger.LogInformation("Successfully registered user.");
				startUploadingExistingLogs();
			}
			catch (UserRegistrationResponseException ex) {
				logger.LogError("Registration failed due to error with the registration response.", ex);
				throw;
			}
			catch (HttpRequestException ex) when (ex.StatusCode is not null) {
				logger.LogError("Registration failed due to error from server.", ex);
				throw;
			}
			catch (HttpRequestException ex) {
				logger.LogError("Registration failed due to communication problem with the backend server.", ex);
				throw;
			}
			catch (Exception ex) {
				logger.LogError("Registration failed due to unexpected error.", ex);
				throw;
			}
		}

		/// <summary>
		/// Ends the current analytics log file if there is one, and begin a new log file to which subsequent Record-operations write their data.
		/// Call this when starting a new session, e.g. a new game playthrough or a more short-term game session.
		/// </summary>
		/// <remarks>
		/// Other state-changing operations (<c>StartNewLog</c>, <c>RegisterAsync</c>, <c>FinishAsync</c>, or the <c>Record</c>... operations) on the current object must not be called concurrently with this.
		/// </remarks>
		public Guid StartNewLog() {
			LogQueue? oldLogQueue;
			LogQueue? newLogQueue;
			Guid logId;
			lock (lockObject) {
				oldLogQueue = currentLogQueue;
				currentLogQueue = newLogQueue = new LogQueue(logStorage.CreateLogFile(out var logFile), logFile);
				logId = logFile.ID;
			}
			pendingLogQueues.Enqueue(newLogQueue);
			oldLogQueue?.entryQueue?.Finish();
			if (oldLogQueue is null) {
				logger.LogInformation("Started new data log file {newId}.", logId);
			}
			else {
				logger.LogInformation("Started new data log file {newId} and finished old data log file {oldId}.", logId, oldLogQueue.logFile.ID);
			}
			ensureLogWritingActive();
			return logId;
		}

		/// <summary>
		/// This method needs to be called before the exiting the application, waiting for the returned Task object, to ensure all log entries are written to disk and to attempt to upload the pending log files.
		/// </summary>
		/// <returns>A Task object that represents the asynchronous finishing operations.</returns>
		/// <remarks>
		/// Uploading may fail for various reasons:
		/// <list type="bullet">
		///		<item>The client is not yet fully registered and has thus not obtained a valid user id yet. In this case, the upload is not attempted in the first place and this method only flushed in-memory queues to the log files. Those are only kept locally.</item>
		///		<item>The client has no connection to the internet. The upload will be retried later, when the application is used again.</item>
		///		<item>The backend server is not operating correctly. The upload will be retried later, when the application is used again.</item>
		///		<item>The server rejects the upload due to an invalid user id or application id. In case of a transient configuration error, the upload will be retried later, when the application is used again. The server should also log this problem for investigation.</item>
		///		<item>The server rejects the upload due to exceeding the maximum file size. In this case, the file is moved to a special directory for further investigation to not waste the users bandwidth with further attempts. The server should also log this problem to indicate, that an application generates larger than expected log files.</item>
		/// </list>
		///
		/// Other state-changing operations (<c>StartNewLog</c>, <c>RegisterAsync</c>, <c>FinishAsync</c>, or the <c>Record</c>... operations) on the current object must not be called, between start and completion of this operation.
		/// </remarks>
		public async Task FinishAsync() {
			logger.LogDebug("Finishing asynchronous data log writing and uploading...");
			Task? logWriter;
			lock (lockObject) {
				logWriter = this.logWriter;
			}

			currentLogQueue?.entryQueue?.Finish();
			pendingLogQueues.Finish();

			if (logWriter is not null) {
				await logWriter;
			}
			else {
				uploadQueue.Finish();
			}
			Task? logUploader;
			lock (lockObject) {
				logUploader = this.logUploader;
			}
			if (logUploader is not null) {
				await logUploader;
			}
			// At this point, logWriter and logUploader are completed or were never started.
			// We can therefore restore the initial state before the first StartNewLog call safely without lock-based coordination.
			this.logWriter = null;
			this.logUploader = null;
			currentLogQueue = null;
			// As a completed channel can not be reopened, we need to replace the queue object (containing the channel) itself.
			pendingLogQueues = new AsyncConsumerQueue<LogQueue>();
			uploadQueue = new AsyncConsumerQueue<ILogStorage.ILogFile>();
			logger.LogInformation("Finished asynchronous data log writing and uploading.");
		}

		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. The object needs to be clonable to obtain an unshared copy because the log recording to disk is done asynchronously and the object content otherwise might have changed when it is read leater. If the object is created specifically for this call, or will not be modified after the call, call RecordEventUnshared instead to avoid this copy.</param>
		/// <remarks>
		/// The recorded entry has a field containing the event type as a string. If the dynamic type of eventObject has an <c>[EventType(name)]</c> attribute (<see cref="EventTypeAttribute"/>), the name given there ist used. Otherwise the name of the class itself is used.
		///
		/// This operation can be invoked concurrently with other <c>Record</c>... methods, but NOT concurrently with <c>StartNewLog</c> and <c>FinishAsync</c>.
		/// </remarks>
		public void RecordEvent(string channel, ICloneable eventObject) {
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			RecordEventUnshared(channel, eventObject.Clone());
		}
		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. As the log recording to disk is done asynchronously, the ownership of the given object is transferred to the analytics client and must not be changed by the caller afterwards. The easiest way to ensure this is by creating the event object inside the call and not holding other references to it.</param>
		/// <remarks>
		/// The recorded entry has a field containing the event type as a string. If the dynamic type of eventObject has an <c>[EventType(name)]</c> attribute (<see cref="EventTypeAttribute"/>), the name given there ist used. Otherwise the name of the class itself is used.
		///
		/// This operation can be invoked concurrently with other <c>Record</c>... methods, but NOT concurrently with <c>StartNewLog</c> and <c>FinishAsync</c>.
		/// </remarks>
		public void RecordEventUnshared(string channel, object eventObject) {
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			var eventType = eventObject.GetType();
			var attributes = eventType.GetCustomAttributes(typeof(EventTypeAttribute), false);
			var eventTypeName = attributes.Cast<EventTypeAttribute>().SingleOrDefault()?.EventTypeName ?? eventType.Name;
			currentLogQueue.entryQueue.Enqueue(new LogEntry(LogEntry.EntryMetadata.NewEventEntry(channel, DateTime.Now, eventTypeName), eventObject));
		}
		/// <summary>
		/// Record the given snapshot data for an application object to the current analytics log file, tagged with the given channel for categorization, with the id of the object, and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="objectId">An ID of the snapshotted object.</param>
		/// <param name="snapshotPayloadData">An object encapsulating the snapshotted object state to write to the log in JSON form. As the log recording to disk is done asynchronously, the ownership of the given object is transferred to the analytics client and must not be changed by the caller afterwards. The easiest way to ensure this is by creating the snapshot state object inside the call and not holding other references to it.</param>
		/// <remarks>This operation can be invoked concurrently with other <c>Record</c>... methods, but NOT concurrently with <c>StartNewLog</c> and <c>FinishAsync</c>.</remarks>
		public void RecordSnapshotUnshared(string channel, object objectId, object snapshotPayloadData) {
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			currentLogQueue.entryQueue.Enqueue(new LogEntry(LogEntry.EntryMetadata.NewSnapshotEntry(channel, DateTime.Now, objectId), snapshotPayloadData));
		}
	}
}
