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
			public object? ObjectID { get; private set; } = null;

			// TODO: Prevent the serializer from outputting both, EventType and ObjectID, while only one can ever be active at a time.

			private EntryMetadata(string channel, DateTime timeStamp, LogEntryType entryType) {
				Channel = channel;
				TimeStamp = timeStamp;
				EntryType = entryType;
			}

			public static EntryMetadata NewSnapshotEntry(string channel, DateTime timeStamp, object objectId) {
				EntryMetadata em = new EntryMetadata(channel, timeStamp, LogEntryType.Snapshot);
				em.ObjectID = objectId;
				return em;
			}
			public static EntryMetadata NewEventEntry(string channel, DateTime timeStamp, string eventType) {
				EntryMetadata em = new EntryMetadata(channel, timeStamp, LogEntryType.Event);
				em.EventType = eventType;
				return em;
			}
		}

		public EntryMetadata Metadata { get; private set; }
		public object Payload { get; private set; }

		public LogEntry(EntryMetadata metadata, object payload) {
			Metadata = metadata;
			Payload = payload;
		}
	}
}
