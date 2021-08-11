using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	[AttributeUsage(AttributeTargets.Class)]
	public class EventTypeAttribute : Attribute {
		public string EventTypeName { get; private set; }
		EventTypeAttribute(string eventTypeName) {
			EventTypeName = eventTypeName;
		}
	}

	public class SGLAnalytics {
		private string appID;
		private string appAPIToken;
		private IRootDataStore rootDataStore;
		private ILogStorage logStorage;
		SGLAnalytics(string appID, string appAPIToken, IRootDataStore rootDataStore, ILogStorage logStorage) {
			this.rootDataStore = rootDataStore;
			this.logStorage = logStorage;
		}

		SGLAnalytics(string appID, string appAPIToken) : this(appID, appAPIToken, new FileRootDataStore(), new DirectoryLogStorage()) { }
		SGLAnalytics(string appID, string appAPIToken, IRootDataStore rootDataStore) : this(appID, appAPIToken, rootDataStore, new DirectoryLogStorage()) { }
		SGLAnalytics(string appID, string appAPIToken, ILogStorage logStorage) : this(appID, appAPIToken, new FileRootDataStore(), logStorage) { }

		public string AppID { get => appID; }

		/// <summary>
		/// Checks if the user registration for this client was already done.
		/// If this returns false, call RegisterAsync and ensure the registration before relying on logs being uploaded.
		/// When logs are recorded on an unregistered client, they are stored locally and are not uploaded until the registration is completed and a user id is obtained.
		/// </summary>
		/// <returns>true if the client is already registered, false if the registration is not yet done.</returns>
		public bool IsRegistered() {
			return rootDataStore.UserID is not null;
		}

		/// <summary>
		/// Registers the user with the given data in the backend database, obtains a user id and stores it locally on the client using the configured rootDataStore for future use.
		/// </summary>
		/// <param name="userData">The user data for the registration, that is to be sent to the server.</param>
		/// <returns>A Task representing the registration operation. Wait for it's completion before relying on logs being uploaded. Logs recorded on a client that hasn't completed registration are stored only locally until the registration is complete and the user id required for the upload is obtained.</returns>
		public async Task RegisterAsync(UserData userData) {
			// TODO: Perform POST to Backend
			// TODO: Store returned UserID in rootDataStore.UserID
			await rootDataStore.SaveAsync();
		}

		/// <summary>
		/// Ends the current analytics log file if there is one, and begin a new log file to which subsequent Record-operations write their data.
		/// Call this when starting a new session, e.g. a new game playthrough or a more short-term game session.
		/// </summary>
		public void StartNewLog() {
			// TODO: Create new queue object and add it to front of queue of pending queues.
			// TODO: If not already running, spwan background worker thread for log flushing.
			//		Note: Actual log file is created by background thread asynchronously when it reaches the new queue object.
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
		/// </remarks>
		public async Task FinishAsync() {
			// TODO: Flush pending queues to files.
			// TODO: Attempt to upload pending files.
			// TODO: Update pending logs list if needed.
			// TODO: Archive / delete sucessfully uploaded files.
		}

		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. The object needs to be clonable to obtain an unshared copy because the log recording to disk is done asynchronously and the object content otherwise might have changed when it is read leater. If the object is created specifically for this call, or will not be modified after the call, call RecordEventUnshared instead to avoid this copy.</param>
		/// <remarks>The recorded entry has a field containing the event type as a string. If the dynamic type of eventObject has an <c>[EventType(name)]</c> attribute (<see cref="EventTypeAttribute"/>), the name given there ist used. Otherwise the name of the class itself is used.</remarks>
		public void RecordEvent(string channel, ICloneable eventObject) {
			// TODO: Deep-Copy eventObject and pass copy to RecordEventUnshared
		}
		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. As the log recording to disk is done asynchronously, the ownership of the given object is transferred to the analytics client and must not be changed by the caller afterwards. The easiest way to ensure this is by creating the event object inside the call and not holding other references to it.</param>
		/// <remarks>The recorded entry has a field containing the event type as a string. If the dynamic type of eventObject has an <c>[EventType(name)]</c> attribute (<see cref="EventTypeAttribute"/>), the name given there ist used. Otherwise the name of the class itself is used.</remarks>
		public void RecordEventUnshared(string channel, object eventObject) {
			// TODO: Wrap eventObject in a LogEntry object that associates it with metadata (channel, timestamp, type ...) and insert into current log queue
		}
		/// <summary>
		/// Record the given snapshot data for an application object to the current analytics log file, tagged with the given channel for categorization, with the id of the object, and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="objectId">An ID of the snapshotted object.</param>
		/// <param name="snapshotPayloadData">An object encapsulating the snapshotted object state to write to the log in JSON form. As the log recording to disk is done asynchronously, the ownership of the given object is transferred to the analytics client and must not be changed by the caller afterwards. The easiest way to ensure this is by creating the snapshot state object inside the call and not holding other references to it.</param>
		public void RecordSnapshotUnshared(string channel, object objectId, object snapshotPayloadData) {
			// TODO: Wrap snapshotPayloadData in a LogEntry object that associates it with metadata (channel, objectId, timestamp, type ...) and insert into current log queue
		}
	}
}
