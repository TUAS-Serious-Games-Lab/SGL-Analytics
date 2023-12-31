using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.Analytics {
	[JsonConverter(typeof(LogEntryJsonConverter))]
	internal class LogEntry {
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public enum LogEntryType {
			Event, Snapshot
		}
		public class EntryMetadata {
			public string Channel { get; private set; }
			public DateTimeOffset TimeStamp { get; private set; }
			public LogEntryType EntryType { get; private set; }
			public string? EventType { get; private set; } = null;
			public object? ObjectID { get; private set; } = null;
			private EntryMetadata(string channel, DateTimeOffset timeStamp, LogEntryType entryType) {
				Channel = channel;
				TimeStamp = timeStamp;
				EntryType = entryType;
			}

			internal static EntryMetadata NewSnapshotEntry(string channel, DateTimeOffset timeStamp, object objectId) {
				EntryMetadata em = new EntryMetadata(channel, timeStamp, LogEntryType.Snapshot);
				em.ObjectID = objectId;
				return em;
			}
			internal static EntryMetadata NewEventEntry(string channel, DateTimeOffset timeStamp, string eventType) {
				EntryMetadata em = new EntryMetadata(channel, timeStamp, LogEntryType.Event);
				em.EventType = eventType;
				return em;
			}
		}

		public EntryMetadata Metadata { get; private set; }
		public object Payload { get; private set; }

		internal LogEntry(EntryMetadata metadata, object payload) {
			Metadata = metadata;
			Payload = payload;
		}
	}

	internal class LogEntryJsonConverter : JsonConverter<LogEntry> {
		private static ObjectDictionaryValueJsonConverter valueConverter = new ObjectDictionaryValueJsonConverter();
		public override LogEntry? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			if (reader.TokenType != JsonTokenType.StartObject) {
				throw new JsonException();
			}
			string? channel = null;
			DateTimeOffset? timeStamp = null;
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
						timeStamp = JsonSerializer.Deserialize<DateTimeOffset>(ref reader, options);
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
						reader.Read();
						objectID = valueConverter.Read(ref reader, typeof(object), options);
						break;
					case "payload":
						reader.Read();
						payload = valueConverter.Read(ref reader, typeof(object), options);
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
					if (eventType == null) throw new NotSupportedException("LogEntry with EntryType = Event is missing EventType property.");
					if (objectID != null) throw new NotSupportedException("LogEntry with EntryType = Event does not support ObjectID property.");
					return new LogEntry(LogEntry.EntryMetadata.NewEventEntry(channel, timeStamp.Value, eventType), payload);
				case LogEntry.LogEntryType.Snapshot:
					if (objectID == null) throw new NotSupportedException("LogEntry with EntryType = Snapshot is missing ObjectID property.");
					if (eventType != null) throw new NotSupportedException("LogEntry with EntryType = Snapshot does not support EventType property.");
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
