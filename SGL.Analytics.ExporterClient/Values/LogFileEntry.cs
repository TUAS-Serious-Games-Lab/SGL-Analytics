using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient.Values {
	/// <summary>
	/// Encapsulates the data of an entry in an analytics log file.
	/// </summary>
	public class LogFileEntry {
		/// <summary>
		/// The channel tag passed to the <c>Record</c> method.
		/// </summary>
		public string Channel { get; }
		/// <summary>
		/// The client-side timestamp when this log entry was recorded.
		/// </summary>
		public DateTime TimeStamp { get; }
		/// <summary>
		/// The payload data for the entry, containing the snapshot payload or event object passed to the <c>Record</c> methods mapped as a dictionary.
		/// </summary>
		public IReadOnlyDictionary<string, object?> Payload { get; }

		internal LogFileEntry(string channel, DateTime timeStamp, IReadOnlyDictionary<string, object?> payload) {
			Channel = channel;
			TimeStamp = timeStamp;
			Payload = payload;
		}
	}

	/// <summary>
	/// Encapsulates the data for an event-typed entry in an analytics log file.
	/// </summary>
	public sealed class EventLogFileEntry : LogFileEntry {
		/// <summary>
		/// The event type passed to the <c>RecordEvent</c> method.
		/// </summary>
		public string EventType { get; }

		internal EventLogFileEntry(string channel, DateTime timeStamp, IReadOnlyDictionary<string, object?> payload, string eventType) : base(channel, timeStamp, payload) {
			EventType = eventType;
		}
	}

	/// <summary>
	/// Encapsulates the data for an snapshot-typed entry in an analytics log file.
	/// </summary>
	public sealed class SnapshotLogFileEntry : LogFileEntry {
		/// <summary>
		/// The object id passed to the <c>RecordSnapshot</c> method to identify of which object's state the snapshot was made.
		/// </summary>
		public object? ObjectId { get; }
		internal SnapshotLogFileEntry(string channel, DateTime timeStamp, IReadOnlyDictionary<string, object?> payload, object? objectId) : base(channel, timeStamp, payload) {
			ObjectId = objectId;
		}
	}
}
