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

		public bool IsRegistered() {
			return rootDataStore.UserID is not null;
		}

		public async Task RegisterAsync(UserData userData) {
			// TODO: Perform POST to Backend
			// TODO: Store returned UserID in rootDataStore.UserID
			await rootDataStore.SaveAsync();
		}

		public void StartNewLog() {
			// TODO: Create new queue object and add it to front of queue of pending queues.
			// TODO: If not already running, spwan background worker thread for log flushing.
			//		Note: Actual log file is created by background thread asynchronously when it reaches the new queue object.
		}

		public async Task FinishAsync() {
			// TODO: Flush pending queues to files.
			// TODO: Attempt to upload pending files.
			// TODO: Update pending logs list if needed.
			// TODO: Archive / delete sucessfully uploaded files.
		}

		public void RecordEvent(string channel, ICloneable eventObject) {
			// TODO: Deep-Copy eventObject and pass copy to RecordEventUnshared
		}
		public void RecordEventUnshared(string channel, object eventObject) {
			// TODO: Wrap eventObject in a LogEntry object that associates it with metadata (channel, timestamp, type ...) and insert into current log queue
		}

		public void RecordSnapshotUnshared(string channel, object objectId, JsonElement snapshotPayloadData) {
			// TODO: Wrap snapshotPayloadData in a LogEntry object that associates it with metadata (channel, objectId, timestamp, type ...) and insert into current log queue
		}
	}
}
