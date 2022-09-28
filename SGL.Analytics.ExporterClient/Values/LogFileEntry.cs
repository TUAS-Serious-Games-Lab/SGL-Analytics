using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient.Values {
	public class LogFileEntry {
		public string Channel { get; }
		public DateTime TimeStamp { get; }
		public IReadOnlyDictionary<string, object?> Payload { get; }

		internal LogFileEntry(string channel, DateTime timeStamp, IReadOnlyDictionary<string, object?> payload) {
			Channel = channel;
			TimeStamp = timeStamp;
			Payload = payload;
		}
	}

	public sealed class EventLogFileEntry : LogFileEntry {
		public string EventType { get; }

		internal EventLogFileEntry(string channel, DateTime timeStamp, IReadOnlyDictionary<string, object?> payload, string eventType) : base(channel, timeStamp, payload) {
			EventType = eventType;
		}
	}

	public sealed class SnapshotLogFileEntry : LogFileEntry {
		public object? ObjectId { get; }
		internal SnapshotLogFileEntry(string channel, DateTime timeStamp, IReadOnlyDictionary<string, object?> payload, object? objectId) : base(channel, timeStamp, payload) {
			ObjectId = objectId;
		}
	}
}
