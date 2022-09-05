using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	/// <summary>
	/// Can be used to annotate types used as event representations for <see cref="SglAnalytics.RecordEvent(string, ICloneable)"/> or
	/// <see cref="SglAnalytics.RecordEventUnshared(string, object)"/> to use an event type name that differs from the types name.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class EventTypeAttribute : Attribute {
		/// <summary>
		/// The name to use in the analytics logs to represent the event type.
		/// </summary>
		public string EventTypeName { get; private set; }
		/// <summary>
		/// Instantiates the attribute.
		/// </summary>
		/// <param name="eventTypeName">The name to use in the analytics logs to represent the event type.</param>
		public EventTypeAttribute(string eventTypeName) {
			EventTypeName = eventTypeName;
		}
	}

	/// <summary>
	/// Acts as the central facade class for the functionality of SGL Analytics and coordinates its operation.
	/// It provides a simple to use mechanism to record analytics log files (containing different streams of events and object state snapshots).
	/// The writing of this files to disk is done asynchronously in the background to not slow down the application and
	/// the completed files are uploaded to a collector backend that catalogs them by application and user.
	/// The upload process also happens automatically in the background and retries failed uploads on startup or when <see cref="StartRetryUploads"/> is called.
	///
	/// The public methods allow registering the user, beginning a new analytics log file, recording events and snapshots into the current analytics log file,
	/// and finishing the analytics log operations by finishing the current file, waiting for it to be written and ensuring all pending uploads are complete.
	/// </summary>
	public partial class SglAnalytics {

		/// <summary>
		/// Acts as the default value for the <c>backendBaseUri</c> parameter of the constructor and can be set before instantiating the object.
		/// It defaults to localhost for testing. Thus, released applications need to either set this property before instantiating SGL Analytics or pass a <c>backendBaseUri</c> to the constructor.
		/// </summary>
		public static Uri DefaultBackendBaseUri { get; set; } = new Uri("https://localhost/");
		// TODO: Replace default URL with registered URL of Prod backend when available.

		/// <summary>
		/// Initializes the SGL Analytics service with the given configuration parameters.
		/// The parameters can be used to adapt SGL Analytics for the different applications and their environment, or may be used to replace some functionality with a dummy for testing purposes.
		/// If an parameter is passed as <see langword="null"/>, as is their syntactic default, it is internally set to a resonable default. These semantic defaults are documented with each optional parameter.
		/// </summary>
		/// <param name="appName">The technical name of the application for which analytics logs are recorded. This is used for identifying the application in the backend and the application must be registered there for log collection and user registration to work properly.</param>
		/// <param name="appAPIToken">The API token assigned to the application in the backend. This is used as an additional security layer in the communication with the backend.</param>
		/// <param name="recipientCertificateValidator">
		/// Validates the certificates of recipients to determine the authorized recipients for end-to-end encrypted data.
		/// </param>
		/// <param name="backendBaseUri">
		/// The base URI of the REST API backend. The API routes are prefixed with this and it needs to be an absolute URI to specify the domain name of the server.
		/// It defaults to the value specified in <see cref="DefaultBackendBaseUri"/>.
		/// </param>
		/// <param name="rootDataStore">
		/// Specifies the root data store implementation to use. This is used to store the user registration data (usually on the device).
		/// It defaults to a <see cref="FileRootDataStore"/> that stores the data in a JSON file under a subfolder, named after <paramref name="appName"/> in the user's application data folder
		/// as identified by <see cref="Environment.SpecialFolder.ApplicationData"/>.
		/// </param>
		/// <param name="logStorage">
		/// Specifies the local analytics log storage implementation to use. This is used to manage the locally stored analytics log files.
		/// It defaults to a <see cref="DirectoryLogStorage"/>, storing the logs as compressed files under a subfolder named <c>DataLogs</c> under the <see cref="IRootDataStore.DataDirectory"/>
		/// property of <paramref name="rootDataStore"/>, using the file timestamps to store the time metadata of the log, and deleting logs after they are uploaded (instead of archiving them).
		/// </param>
		/// <param name="logCollectorClient">
		/// Specifies the client implementation to use for the log collector backend.
		/// It defaults to using a <see cref="LogCollectorRestClient"/> that uses REST API calls to the backend specified by <paramref name="backendBaseUri"/>.
		/// </param>
		/// <param name="userRegistrationClient">
		/// Specifies the client implementation to use for the user registration backend.
		/// It defaults to using a <see cref="UserRegistrationRestClient"/> that uses REST API calls to the backend specified by <paramref name="backendBaseUri"/>.
		/// </param>
		/// <param name="diagnosticsLogger">
		/// Allows providing an <see cref="ILogger{SGLAnalytics}"/> to which SGL Analytics should log its internal diagnostic log events and possible errors.
		/// Note that this does not affect the analytics logs, which log data about the application, but it is used to log data about SGL Analytics itself.
		/// It defaults to <see cref="NullLogger{SGLAnalytics}.Instance"/> so that log messages are ignored.
		/// </param>
		public SglAnalytics(string appName, string appAPIToken, ICertificateValidator recipientCertificateValidator, Uri? backendBaseUri = null, IRootDataStore? rootDataStore = null, ILogStorage? logStorage = null, ILogCollectorClient? logCollectorClient = null, IUserRegistrationClient? userRegistrationClient = null, ILogger<SglAnalytics>? diagnosticsLogger = null) {
			// Capture the SynchronizationContext of the 'main' thread, so we can perform tasks that need to run there by Post()ing to the context.
			mainSyncContext = SynchronizationContext.Current ?? new SynchronizationContext();
			this.appName = appName;
			this.appAPIToken = appAPIToken;
			this.recipientCertificateValidator = recipientCertificateValidator;
			if (backendBaseUri is null) backendBaseUri = DefaultBackendBaseUri;
			if (diagnosticsLogger is null) diagnosticsLogger = NullLogger<SglAnalytics>.Instance;
			logger = diagnosticsLogger;
			if (rootDataStore is null) rootDataStore = new FileRootDataStore(appName);
			this.rootDataStore = rootDataStore;
			if (logStorage is null) logStorage = new DirectoryLogStorage(Path.Combine(rootDataStore.DataDirectory, "DataLogs"));
			this.logStorage = logStorage;
			if (logCollectorClient is null) logCollectorClient = new LogCollectorRestClient(backendBaseUri);
			this.logCollectorClient = logCollectorClient;
			if (userRegistrationClient is null) userRegistrationClient = new UserRegistrationRestClient(backendBaseUri);
			this.userRegistrationClient = userRegistrationClient;
			if (IsRegistered()) {
				startUploadingExistingLogs();
			}
		}

		/// <summary>
		/// Specifies the strength of the secret that is generated upon user registration.
		/// The secret is created by generating the given number of bytes and then base64-encoding them.
		/// Therefore the actual secret string will be longer due to encoding overhead.
		/// </summary>
		public int UserRegistrationSecretLength { get; set; } = 16;

		/// <summary>
		/// Gets the technical name of the application that uses this SGL Analytics instance, as specified in the constructor.
		/// </summary>
		public string AppName { get => appName; }

		/// <summary>
		/// Checks if the user registration for this client was already done.
		/// If this returns false, call RegisterAsync and ensure the registration before relying on logs being uploaded.
		/// When logs are recorded on an unregistered client, they are stored locally and are not uploaded until the registration is completed and a user id is obtained.
		/// </summary>
		/// <returns>true if the client is already registered, false if the registration is not yet done.</returns>
		public bool IsRegistered() {
			lock (lockObject) {
				return rootDataStore.UserID != null || rootDataStore.Username != null;
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
		/// <exception cref="UsernameAlreadyTakenException">If <paramref name="userData"/> had the optional <see cref="BaseUserData.Username"/> property set and the given username is already taken for this application. If this happens, the user needs to pick a different name.</exception>
		/// <exception cref="UserRegistrationResponseException">If the server didn't respond with the expected object in the expected format.</exception>
		/// <exception cref="HttpRequestException">Indicates either a network problem (if <see cref="HttpRequestException.StatusCode"/> is <see langword="null"/>) or a server-side error (if <see cref="HttpRequestException.StatusCode"/> has a value).</exception>
		public async Task RegisterAsync(BaseUserData userData) {
			try {
				if (IsRegistered()) {
					throw new InvalidOperationException("User is already registered.");
				}
				logger.LogInformation("Starting user registration process...");
				var secret = SecretGenerator.Instance.GenerateSecret(UserRegistrationSecretLength);
				var userDTO = userData.MakeDTO(appName, secret);
				var recipientCertificates = await loadAuthorizedRecipientCertificatesAsync(userRegistrationClient);
				var certList = recipientCertificates.ListKnownKeyIdsAndPublicKeys().ToList();
				if (!certList.Any() && userDTO.StudySpecificProperties.Any()) {
					const string msg = "Can't send registration because no authorized recipients for study-specific properties were found.";
					logger.LogError(msg);
					throw new InvalidOperationException(msg);
				}
				var keyEncryptor = new KeyEncryptor(certList, randomGenerator, allowSharedMessageKeyPair);
				Validator.ValidateObject(userDTO, new ValidationContext(userDTO), true);
				var regResult = await userRegistrationClient.RegisterUserAsync(userDTO, appAPIToken);
				logger.LogInformation("Registration with backend succeeded. Got user id {userId}. Proceeding to store user id locally...", regResult.UserId);
				lock (lockObject) {
					rootDataStore.UserID = regResult.UserId;
					rootDataStore.UserSecret = secret;
					if (userData.Username != null) {
						rootDataStore.Username = userData.Username;
					}
				}
				await rootDataStore.SaveAsync();
				logger.LogInformation("Successfully registered user.");
				startUploadingExistingLogs();
			}
			catch (UsernameAlreadyTakenException ex) {
				logger.LogError(ex, "Registration failed because the specified username is already in use.");
			}
			catch (UserRegistrationResponseException ex) {
				logger.LogError(ex, "Registration failed due to error with the registration response.");
				throw;
			}
#if NET5_0_OR_GREATER
			catch (HttpRequestException ex) when (ex.StatusCode is not null) {
				logger.LogError(ex, "Registration failed due to error from server.");
				throw;
			}
			catch (HttpRequestException ex) {
				logger.LogError(ex, "Registration failed due to communication problem with the backend server.");
				throw;
			}
#else
			catch (HttpRequestException ex) {
				logger.LogError(ex, "Registration failed due to a backend server error.");
				throw;
			}
#endif
			catch (ValidationException ex) {
				logger.LogError(ex, "Registration failed due to violating validation constraints.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "Registration failed due to unexpected error.");
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
		///		<item><description>The client is not yet fully registered and has thus not obtained a valid user id yet. In this case, the upload is not attempted in the first place and this method only flushed in-memory queues to the log files. Those are only kept locally.</description></item>
		///		<item><description>The client has no connection to the internet. The upload will be retried later, when the application is used again.</description></item>
		///		<item><description>The backend server is not operating correctly. The upload will be retried later, when the application is used again.</description></item>
		///		<item><description>The server rejects the upload due to an invalid user id or application id. In case of a transient configuration error, the upload will be retried later, when the application is used again. The server should also log this problem for investigation.</description></item>
		///		<item><description>The server rejects the upload due to exceeding the maximum file size. In this case, the file is moved to a special directory for further investigation to not waste the users bandwidth with further attempts. The server should also log this problem to indicate, that an application generates larger than expected log files.</description></item>
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
		/// Retries the upload of analytics log files that are stored locally because their upload previously failed, e.g. because no internet connectivity was available or a server error prevented the upload.
		/// </summary>
		/// <remarks>
		/// This operation only enqueues the existing files for upload in the background and starts the asynchronous upload worker process if the requirements are met, i.e. if the user is registered and there are files to upload.
		/// As the previously failed files are enqueued in the same queue as the freshly written ones from <see cref="StartNewLog"/>, there is no separate mechanism to wait for the completion of the upload of only the retried files.
		/// Instead, waiting for <see cref="FinishAsync"/> finishes the current log, eneuques it and then waits for all enqueued uploads to finish (or fail).
		/// </remarks>
		public void StartRetryUploads() {
			startUploadingExistingLogs();
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
		/// <param name="eventObject">The event payload data to write to the log in JSON form. The object needs to be clonable to obtain an unshared copy because the log recording to disk is done asynchronously and the object content otherwise might have changed when it is read leater. If the object is created specifically for this call, or will not be modified after the call, call RecordEventUnshared instead to avoid this copy.</param>
		/// <param name="eventType">The name to use for the event type field of the recorded log entry.</param>
		/// <remarks>
		/// The recorded entry has a field containing the event type as a string.
		/// This overload simply sets the value given in <paramref name="eventType"/> as the entry's event type field,
		/// which allows common types such as collection objects to be used for <paramref name="eventObject"/>,
		/// as they don't have an <see cref="EventTypeAttribute"/> and their type name is also not suitable for usage in the log entry.
		/// For custom event object types, it is usually recommended to use <see cref="RecordEvent(string, ICloneable)"/> instead.
		///
		/// This operation can be invoked concurrently with other <c>Record</c>... methods, but NOT concurrently with <c>StartNewLog</c> and <c>FinishAsync</c>.
		/// </remarks>
		public void RecordEvent(string channel, ICloneable eventObject, string eventType) {
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			RecordEventUnshared(channel, eventObject.Clone(), eventType);
		}

		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. As the log recording to disk is done asynchronously, the ownership of the given object is transferred to the analytics client and must not be changed by the caller afterwards. The easiest way to ensure this is by creating the event object inside the call and not holding other references to it.</param>
		/// <param name="eventType">The name to use for the event type field of the recorded log entry.</param>
		/// <remarks>
		/// The recorded entry has a field containing the event type as a string.
		/// This overload simply sets the value given in <paramref name="eventType"/> as the entry's event type field,
		/// which allows common types such as collection objects to be used for <paramref name="eventObject"/>,
		/// as they don't have an <see cref="EventTypeAttribute"/> and their type name is also not suitable for usage in the log entry.
		/// For custom event object types, it is usually recommended to use <see cref="RecordEventUnshared(string, object)"/> instead.
		///
		/// This operation can be invoked concurrently with other <c>Record</c>... methods, but NOT concurrently with <c>StartNewLog</c> and <c>FinishAsync</c>.
		/// </remarks>
		public void RecordEventUnshared(string channel, object eventObject, string eventType) {
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			currentLogQueue.entryQueue.Enqueue(new LogEntry(LogEntry.EntryMetadata.NewEventEntry(channel, DateTime.Now, eventType), eventObject));
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
			RecordEventUnshared(channel, eventObject, eventTypeName);
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
