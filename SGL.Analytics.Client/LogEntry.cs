using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.Analytics.Client {
	[JsonConverter(typeof(LogEntryJsonConverter))]
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

	public class LogEntryJsonConverter : JsonConverter<LogEntry> {
		public override LogEntry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			if (reader.TokenType != JsonTokenType.StartObject) {
				throw new JsonException();
			}
			string? channel = null;
			DateTime? timeStamp = null;
			LogEntry.LogEntryType? entryType = null;
			string? eventType = null;
			object? objectID = null;
			object? payload = null;
			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndObject) {
					break;
				}

				if (reader.TokenType != JsonTokenType.PropertyName) {
					throw new JsonException();
				}

				string propertyName = reader.GetString() ?? "";

				switch (propertyName.ToLower()) {
					case "channel":
						reader.Read();
						channel = reader.GetString();
						break;
					case "timestamp":
						timeStamp = JsonSerializer.Deserialize<DateTime>(ref reader, options);
						break;
					case "entrytype":
						reader.Read();
						if (Enum.TryParse(reader.GetString(), ignoreCase: true, out LogEntry.LogEntryType entryTypeParsed)) {
							entryType = entryTypeParsed;
						}
						else {
							throw new JsonException($"The property '{propertyName}' doesn't have a valid value.");
						}
						break;
					case "eventtype":
						reader.Read();
						eventType = reader.GetString();
						break;
					case "objectid":
						objectID = JsonSerializer.Deserialize<object>(ref reader, options);
						break;
					case "payload":
						payload = JsonSerializer.Deserialize<object>(ref reader, options);
						break;
					default:
						throw new NotSupportedException($"Invalid LogEntry property '{propertyName}'.");
				}
			}
			if (channel is null) throw new NotSupportedException("LogEntry is missing Channel property.");
			if (timeStamp is null) throw new NotSupportedException("LogEntry is missing TimeStamp property.");
			if (payload is null) throw new NotSupportedException("LogEntry is missing Payload property.");
			switch (entryType) {
				case null:
					throw new NotSupportedException("LogEntry is missing EntryType property.");
				case LogEntry.LogEntryType.Event:
					if (eventType is null) throw new NotSupportedException("LogEntry with EntryType = Event is missing EventType property.");
					if (objectID is not null) throw new NotSupportedException("LogEntry with EntryType = Event does not support ObjectID property.");
					return new LogEntry(LogEntry.EntryMetadata.NewEventEntry(channel, timeStamp.Value, eventType), payload);
				case LogEntry.LogEntryType.Snapshot:
					if (objectID is null) throw new NotSupportedException("LogEntry with EntryType = Snapshot is missing ObjectID property.");
					if (eventType is not null) throw new NotSupportedException("LogEntry with EntryType = Snapshot does not support EventType property.");
					return new LogEntry(LogEntry.EntryMetadata.NewSnapshotEntry(channel, timeStamp.Value, objectID), payload);
				default:
					throw new NotSupportedException("Unsupported EntryType.");
			}
		}

		public override void Write(Utf8JsonWriter writer, LogEntry value, JsonSerializerOptions options) {
			writer.WriteStartObject();
			writeProperty(writer, options, "Channel");
			writer.WriteStringValue(value.Metadata.Channel);
			writeProperty(writer, options, "TimeStamp");
			JsonSerializer.Serialize(writer, value.Metadata.TimeStamp, options);
			writeProperty(writer, options, "EntryType");
			writer.WriteStringValue(value.Metadata.EntryType.ToString());
			switch (value.Metadata.EntryType) {
				case LogEntry.LogEntryType.Event:
					writeProperty(writer, options, "EventType");
					writer.WriteStringValue(value.Metadata.EventType);
					break;
				case LogEntry.LogEntryType.Snapshot:
					writeProperty(writer, options, "ObjectID");
					JsonSerializer.Serialize(writer, value.Metadata.ObjectID, options);
					break;
				default:
					throw new NotSupportedException();
			}
			writeProperty(writer, options, "Payload");
			JsonSerializer.Serialize(writer, value.Payload, options);
			writer.WriteEndObject();

			static void writeProperty(Utf8JsonWriter writer, JsonSerializerOptions options, string propertyName) {
				writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName);
			}
		}
	}

}
