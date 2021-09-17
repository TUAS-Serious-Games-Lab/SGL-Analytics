using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Analytics.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	[AttributeUsage(AttributeTargets.Class)]
	public class EventTypeAttribute : Attribute {
		public string EventTypeName { get; private set; }
		public EventTypeAttribute(string eventTypeName) {
			EventTypeName = eventTypeName;
		}
	}

	public partial class SGLAnalytics {
		public SGLAnalytics(string appName, string appAPIToken, IRootDataStore? rootDataStore = null, ILogStorage? logStorage = null, ILogCollectorClient? logCollectorClient = null, IUserRegistrationClient? userRegistrationClient = null, ILogger<SGLAnalytics>? diagnosticsLogger = null) {
			// Capture the SynchronizationContext of the 'main' thread, so we can perform tasks that need to run there by Post()ing to the context.
			mainSyncContext = SynchronizationContext.Current ?? new SynchronizationContext();
			this.appName = appName;
			this.appAPIToken = appAPIToken;
			if (diagnosticsLogger is null) diagnosticsLogger = NullLogger<SGLAnalytics>.Instance;
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
