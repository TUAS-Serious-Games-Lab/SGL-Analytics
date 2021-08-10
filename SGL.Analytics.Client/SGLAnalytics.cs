using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public class SGLAnalytics {
		private string appID;
		private string appAPIToken;
		private IRootDataStore rootDataStore;
		private ILogStorage logStorage;
		SGLAnalytics(string appID, string appAPIToken, IRootDataStore rootDataStore, ILogStorage logStorage) {
			this.rootDataStore = rootDataStore;
			this.logStorage = logStorage;
		}

		public string AppID { get => appID; }

		/// <summary>
		/// Checks if the user registration for this client was already done.
		/// If this returns false, call RegisterAsync and ensure the registration is complete before creating logs or issuing Record-operations.
		/// </summary>
		/// <returns>true is the client is already registered, false if the registration is not yet done.</returns>
		public bool IsRegistered() {
			return rootDataStore.UserID is not null;
		}

		/// <summary>
		/// Registers the user with the given data in the backend database, obtains a user id and stores it locally on the client using the configured rootDataStore for future use.
		/// </summary>
		/// <param name="userData">The user data for the registration, that is to be sent to the server.</param>
		/// <returns>A Task representing the registration operation. Wait for it's completion before creating log files or issuing Record-operations.</returns>
		public async Task RegisterAsync(UserData userData) {
			// TODO: Perform POST to Backend
			// TODO: Store returned UserID in rootDataStore.UserID
			await rootDataStore.SaveAsync();
		}

		/// <summary>
		/// Ends the current analytics log file if there is one, and begin a new log file to which subsequent Record-operations write their data.
		/// Call this when starting a new session, e.g. a new game playthrough.
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
		public void RecordEvent(string channel, ICloneable eventObject) {
			// TODO: Deep-Copy eventObject and pass copy to RecordEventUnshared
		}
		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. As the log recording to disk is done asynchronously, the ownership of the given object is transferred to the analytics client and must not be changed by the caller afterwards. The easiest way to ensure this is by creating the event object inside the call and not holding other references to it.</param>
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
