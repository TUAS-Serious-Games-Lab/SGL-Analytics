using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public class LogEntry {
		public enum LogEntryType {
			Event, Snapshot
		}
		public class EntryMetadata {
			public string Channel { get; private set; }
			public DateTime TimeStamp { get; private set; }
			public LogEntryType EntryType { get; private set; }
			public string? EventType { get; private set; } = null;

			public EntryMetadata(string channel, DateTime timeStamp) {
				Channel = channel;
				TimeStamp = timeStamp;
				EntryType = LogEntryType.Snapshot;
			}
			public EntryMetadata(string channel, DateTime timeStamp, string eventType) {
				Channel = channel;
				TimeStamp = timeStamp;
				EntryType = LogEntryType.Event;
				EventType = eventType;
			}
		}

		public EntryMetadata Metadata { get; private set; }
		public object Payload { get; private set; }

	}
}
