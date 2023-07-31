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
	public partial class SglAnalytics : IAsyncDisposable {
		/// <summary>
		/// Instantiates a client facade object using the given app credentials and http client, configured by the given <paramref name="configuration"/> function.
		/// </summary>
		/// <param name="appName">The technical name of the application for which analytics logs are recorded. This is used for identifying the application in the backend and the application must be registered there for log collection and user registration to work properly.</param>
		/// <param name="appAPIToken">The API token assigned to the application in the backend. This is used as an additional security layer in the communication with the backend.</param>
		/// <param name="httpClient">
		/// The <see cref="HttpClient"/> that the client object shall use to communicate with the backend.
		/// The <see cref="HttpClient.BaseAddress"/> is must be set to the base adress of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		/// <param name="configuration">A configurator function that performs fluent-style configuration for the client object.</param>
		public SglAnalytics(string appName, string appAPIToken, HttpClient httpClient, Action<ISglAnalyticsConfigurator> configuration) {
			this.appName = appName;
			this.appAPIToken = appAPIToken;
			this.httpClient = httpClient;
			configuration(configurator);
			mainSyncContext = configurator.SynchronizationContextGetter();
			dataDirectory = configurator.DataDirectorySource(new SglAnalyticsConfiguratorDataDirectorySourceArguments(appName));
			Directory.CreateDirectory(dataDirectory);
			var loggerFactoryBootstrapArgs = new SglAnalyticsConfiguratorFactoryArguments(appName, appAPIToken, httpClient, dataDirectory,
				NullLoggerFactory.Instance, randomGenerator, configurator.CustomArgumentFactories);
			LoggerFactory = configurator.LoggerFactory.Factory(loggerFactoryBootstrapArgs);
			var factoryArgs = new SglAnalyticsConfiguratorFactoryArguments(appName, appAPIToken, httpClient, dataDirectory, LoggerFactory,
				randomGenerator, configurator.CustomArgumentFactories);
			logger = LoggerFactory.CreateLogger<SglAnalytics>();
			cryptoConfig = configurator.CryptoConfig();
			recipientCertificateValidator = configurator.RecipientCertificateValidatorFactory.Factory(factoryArgs);
			rootDataStore = configurator.RootDataStoreFactory.Factory(factoryArgs);
			logStorage = configurator.LogStorageFactory.Factory(factoryArgs);
			userRegistrationClient = configurator.UserRegistrationClientFactory.Factory(factoryArgs);
			logCollectorClient = configurator.LogCollectorClientFactory.Factory(factoryArgs);
			logCollectorClient.AuthorizationExpired += async (s, e, ct) => {
				await loginAsync(true);
			};
			if (IsRegistered()) {
				startUploadingExistingLogs();
			}
		}

		/// <summary>
		/// Gets the technical name of the application that uses this SGL Analytics instance, as specified in the constructor.
		/// </summary>
		public string AppName { get => appName; }

		private ILoggerFactory LoggerFactory { get; }

		/// <summary>
		/// The id of the registered user, or null if not registered.
		/// </summary>
		public Guid? UserID => rootDataStore.UserID;

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
		public async Task RegisterAsync(BaseUserData userData) { // TODO: Rework into private RegisterImplAsync, taking the secret to use as an argument. Current call sites should become RegisterUserWithDeviceToken.
			try {
				if (IsRegistered()) {
					throw new InvalidOperationException("User is already registered.");
				}
				logger.LogInformation("Starting user registration process...");
				var (unencryptedUserPropDict, encryptedUserProps, userPropsEncryptionInfo) = await getUserProperties(userData);
				var secret = SecretGenerator.Instance.GenerateSecret(configurator.LegthOfGeneratedUserSecrets);
				var userDTO = new UserRegistrationDTO(appName, userData.Username, secret, unencryptedUserPropDict, encryptedUserProps, userPropsEncryptionInfo);
				Validator.ValidateObject(userDTO, new ValidationContext(userDTO), true);
				var regResult = await userRegistrationClient.RegisterUserAsync(userDTO);
				logger.LogInformation("Registration with backend succeeded. Got user id {userId}. Proceeding to store user id locally...", regResult.UserId);
				await storeCredentials(userData, secret, regResult);
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
			catch (HttpApiResponseException ex) {
				logger.LogError(ex, "Registration failed due to error from server.");
				throw;
			}
			catch (HttpApiRequestFailedException ex) {
				logger.LogError(ex, "Registration failed due to communication problem with the backend server.");
				throw;
			}
			catch (ValidationException ex) {
				logger.LogError(ex, "Registration failed due to violating validation constraints.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "Registration failed due to unexpected error.");
				throw;
			}
		}

		public async Task RegisterUserWithPasswordAsync(BaseUserData userData, string password, bool rememberCredentials = false, CancellationToken ct = default) {
			// TODO:
			// - maybe: check password complexity
			// - submit registration request
			// - if failed, report reason by exception (username taken, network failure, ...)
			// - if rememberCredentials set, store username, userid, password in root data store
			// - login with newly registered credentials to obtain session token
			// - transfer session token from user client to logs client
			// - hold on to re-login delegate for token refreshing, capturing needed credentials
			// (Some of these will be done in RegisterImplAsync)
			throw new NotImplementedException();
		}
		public async Task RegisterUserWithDeviceSecret(BaseUserData userData, CancellationToken ct = default) {
			// TODO:
			// - generate random secret
			// - submit registration request
			// - if failed, report reason by exception (network failure, ...)
			// - store userid, secret in root data store
			// - login with newly registered credentials to obtain session token
			// - transfer session token from user client to logs client
			// - hold on to re-login delegate for token refreshing, capturing needed credentials
			// (Some of these will be done in RegisterImplAsync)
			throw new NotImplementedException();
		}
		public async Task<LoginAttemptResult> TryLoginWithStoredCredentialsAsync(CancellationToken ct = default) {
			// TODO:
			// - if no credentials present in root data store, return CredentialsNotAvailable
			// - retrieve credentials and send login request
			// - if failed, return Failed or NetworkProblem depending on reason
			// - transfer session token from user client to logs client
			// - hold on to re-login delegate for token refreshing, capturing needed credentials
			throw new NotImplementedException();
		}
		public async Task<LoginAttemptResult> TryLoginWithPasswordAsync(string loginName, string password, bool rememberCredentials = false, CancellationToken ct = default) {
			// TODO:
			// - send login request using provided credentials
			// - if failed, return Failed or NetworkProblem depending on reason
			// - transfer session token from user client to logs client
			// - hold on to re-login delegate for token refreshing, capturing needed credentials
			throw new NotImplementedException();
		}
		public async Task UseOfflineModeAsync(CancellationToken ct = default) {
			// TODO:
			// - if there is a stored user id, load it and store local logs under that identifier
			// - otherwise store local logs under anonymous identifier, can be linked to user later using InheritAnonymousLogsAsync
			// - enable local log writing
			// - disable background upload, don't even attempt to authenticate
			throw new NotImplementedException();
		}
		public async Task DeactivateAsync(CancellationToken ct = default) {
			// TODO:
			// - ensure finished
			// - disable log writing
			// - disable background upload
			// - don't try to authenticate
			// - set a flag that makes StartNewLog, FinishAsync, StartRetryUploads, Record* No-Ops
			throw new NotImplementedException();
		}

		public async Task<IList<(Guid Id, DateTime Start, DateTime End)>> CheckForAnonymousLogsAsync(CancellationToken ct = default) {
			// TODO: Check if there are local, not yet uploaded logs for anonymous identifier (Guid.Empty), return list of ids and time ranges.
			// App can call this and ask user if they are theirs and upon confirmation (or selection), call InheritAnonymousLogsAsync to take ownership.
			throw new NotImplementedException();
		}
		public async Task InheritAnonymousLogsAsync(IEnumerable<Guid> logIds, CancellationToken ct = default) {
			// TODO: Retrieve logs from log storage and add them to upload queue of authenticated user.
			// (Fail if no user session active)
			throw new NotImplementedException();
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
