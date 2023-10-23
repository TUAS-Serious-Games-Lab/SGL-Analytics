using SGL.Analytics.DTO;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Client.Tests {
	public class LogEntrySerializationUnitTest {
		private ITestOutputHelper output;
		private JsonSerializerOptions options = new JsonSerializerOptions(JsonOptions.LogEntryOptions);

		public LogEntrySerializationUnitTest(ITestOutputHelper output) {
			this.output = output;
		}

		[Fact]
		public async Task SerializedEventLogEntryIsDeserializable() {
			using (MemoryStream stream = new()) {
				var entryOrig = new LogEntry(LogEntry.EntryMetadata.NewEventEntry("TestChannel", DateTimeOffset.Now, "TestEvent"), new Dictionary<string, object?>() { ["TestA"] = 12345, ["TestB"] = "Hello World!" });
				await JsonSerializer.SerializeAsync(stream, entryOrig, options);
				stream.Position = 0;
				output.WriteStreamContents(stream);
				stream.Position = 0;
				var entryDeserialized = await JsonSerializer.DeserializeAsync<LogEntry>(stream, options);
				Assert.NotNull(entryDeserialized);
				Assert.Equal(entryOrig.Metadata.Channel, entryDeserialized?.Metadata?.Channel);
				Assert.Equal(entryOrig.Metadata.TimeStamp, entryDeserialized?.Metadata?.TimeStamp);
				Assert.Equal(entryOrig.Metadata.EntryType, entryDeserialized?.Metadata?.EntryType);
				Assert.Equal(entryOrig.Metadata.ObjectID, (entryDeserialized?.Metadata?.ObjectID as JsonElement?)?.GetString());
				var deserializedPayload = entryDeserialized?.Payload as IReadOnlyDictionary<string, object?>;
				Assert.NotNull(deserializedPayload);
				Assert.Equal(12345, Assert.Contains("TestA", deserializedPayload));
				Assert.Equal("Hello World!", Assert.Contains("TestB", deserializedPayload));
			}
		}
		[Fact]
		public async Task SerializedSnapshotLogEntryIsDeserializable() {
			using (MemoryStream stream = new()) {
				var entryOrig = new LogEntry(LogEntry.EntryMetadata.NewSnapshotEntry("TestChannel", DateTimeOffset.Now, "TestObject"), new Dictionary<string, object?>() { ["TestA"] = 12345, ["TestB"] = "Hello World!" });
				await JsonSerializer.SerializeAsync(stream, entryOrig, options);
				stream.Position = 0;
				output.WriteStreamContents(stream);
				stream.Position = 0;
				var entryDeserialized = await JsonSerializer.DeserializeAsync<LogEntry>(stream, options);
				Assert.NotNull(entryDeserialized);
				Assert.Equal(entryOrig.Metadata.Channel, entryDeserialized?.Metadata?.Channel);
				Assert.Equal(entryOrig.Metadata.TimeStamp, entryDeserialized?.Metadata?.TimeStamp);
				Assert.Equal(entryOrig.Metadata.EntryType, entryDeserialized?.Metadata?.EntryType);
				Assert.Equal(entryOrig.Metadata.ObjectID, entryDeserialized?.Metadata?.ObjectID as string);
				var deserializedPayload = entryDeserialized?.Payload as IReadOnlyDictionary<string, object?>;
				Assert.NotNull(deserializedPayload);
				Assert.Equal(12345, Assert.Contains("TestA", deserializedPayload));
				Assert.Equal("Hello World!", Assert.Contains("TestB", deserializedPayload));
			}
		}
	}
}
