using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
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
		private string appName;
		private string appAPIToken;
		private IRootDataStore rootDataStore;
		private ILogStorage logStorage;

		private LogQueue? currentLogQueue;
		private AsyncConsumerQueue<LogQueue> pendingLogQueues = new AsyncConsumerQueue<LogQueue>();
		private Task? logWriter = null;
		private AsyncConsumerQueue<ILogStorage.ILogFile> uploadQueue = new AsyncConsumerQueue<ILogStorage.ILogFile>();

		private Task? logUploader = null;

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
			await foreach (var logQueue in pendingLogQueues.DequeueAllAsync()) {
				await using (var stream = logQueue.writeStream) {
					await foreach (var logEntry in logQueue.entryQueue.DequeueAllAsync()) {
						await JsonSerializer.SerializeAsync(stream, logEntry);
					}
				}
				uploadQueue.Enqueue(logQueue.logFile);
				// TODO: Start Uploading if not already running
			}
			uploadQueue.Finish();
		}

		private void ensureLogWritingActive() {
			if (logWriter is null) {
				// Enforce that the log writer runs on some threadpool thread to avoid putting additional load on app thread.
				logWriter = Task.Run(async () => await writePendingLogsAsync().ConfigureAwait(false));
			}
		}

		public SGLAnalytics(string appName, string appAPIToken, IRootDataStore rootDataStore, ILogStorage logStorage) {
			this.appName = appName;
			this.appAPIToken = appAPIToken;
			this.rootDataStore = rootDataStore;
			this.logStorage = logStorage;
		}

		public SGLAnalytics(string appName, string appAPIToken) : this(appName, appAPIToken, new FileRootDataStore(appName)) { }
		public SGLAnalytics(string appName, string appAPIToken, IRootDataStore rootDataStore) : this(appName, appAPIToken, rootDataStore, new DirectoryLogStorage(Path.Combine(rootDataStore.DataDirectory, "DataLogs"))) { }
		public SGLAnalytics(string appName, string appAPIToken, ILogStorage logStorage) : this(appName, appAPIToken, new FileRootDataStore(appName), logStorage) { }

		public string AppName { get => appName; }

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
			// TODO: Ensure thread-safety of rootDataStore (Upload worker might access UserID while it is being set from here)
			await rootDataStore.SaveAsync();
		}

		/// <summary>
		/// Ends the current analytics log file if there is one, and begin a new log file to which subsequent Record-operations write their data.
		/// Call this when starting a new session, e.g. a new game playthrough or a more short-term game session.
		/// </summary>
		public void StartNewLog() {
			var oldLogQueue = currentLogQueue;
			currentLogQueue = new LogQueue(logStorage.CreateLogFile(out var logFile), logFile);
			pendingLogQueues.Enqueue(currentLogQueue);
			oldLogQueue?.entryQueue?.Finish();
			ensureLogWritingActive();
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
			pendingLogQueues.Finish();
			if (logWriter is not null) {
				await logWriter;
			}
			else {
				uploadQueue.Finish();
			}
			if (logUploader is not null) {
				await logUploader;
			}
			// TODO: Do we need an else here? (Maybe start a new upload attempt and await it?)

			// TODO: Archive / delete sucessfully uploaded files (in logUploader).
		}

		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. The object needs to be clonable to obtain an unshared copy because the log recording to disk is done asynchronously and the object content otherwise might have changed when it is read leater. If the object is created specifically for this call, or will not be modified after the call, call RecordEventUnshared instead to avoid this copy.</param>
		/// <remarks>The recorded entry has a field containing the event type as a string. If the dynamic type of eventObject has an <c>[EventType(name)]</c> attribute (<see cref="EventTypeAttribute"/>), the name given there ist used. Otherwise the name of the class itself is used.</remarks>
		public void RecordEvent(string channel, ICloneable eventObject) {
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			RecordEventUnshared(channel, eventObject.Clone());
		}
		/// <summary>
		/// Record the given event object to the current analytics log file, tagged with the given channel for categorization and with the current time according to the client's local clock.
		/// </summary>
		/// <param name="channel">A channel name that is used to categorize analytics log entries into multiple logical data streams.</param>
		/// <param name="eventObject">The event payload data to write to the log in JSON form. As the log recording to disk is done asynchronously, the ownership of the given object is transferred to the analytics client and must not be changed by the caller afterwards. The easiest way to ensure this is by creating the event object inside the call and not holding other references to it.</param>
		/// <remarks>The recorded entry has a field containing the event type as a string. If the dynamic type of eventObject has an <c>[EventType(name)]</c> attribute (<see cref="EventTypeAttribute"/>), the name given there ist used. Otherwise the name of the class itself is used.</remarks>
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
		public void RecordSnapshotUnshared(string channel, object objectId, object snapshotPayloadData) {
			if (currentLogQueue is null) { throw new InvalidOperationException("Can't record entries to current event log, because no log was started. Call StartNewLog() before attempting to record entries."); }
			currentLogQueue.entryQueue.Enqueue(new LogEntry(LogEntry.EntryMetadata.NewSnapshotEntry(channel, DateTime.Now, objectId), snapshotPayloadData));
		}
	}
}
