using SGL.Analytics.TestUtilities;
using SGL.Analytics.Utilities;
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
		private JsonSerializerOptions options = new JsonSerializerOptions() { WriteIndented = true };

		public LogEntrySerializationUnitTest(ITestOutputHelper output) {
			this.output = output;
		}

		[Fact]
		public async Task SerializedEventLogEntryIsDeserializable() {
			using (MemoryStream stream = new()) {
				var entryOrig = new LogEntry(LogEntry.EntryMetadata.NewEventEntry("TestChannel", DateTime.Now, "TestEvent"), new Dictionary<string, object?>() { ["TestA"] = 12345, ["TestB"] = "Hello World!" });
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
				var deserializedPayload = entryDeserialized?.Payload as JsonElement?;
				JsonElement testA = new JsonElement();
				Assert.True(deserializedPayload?.TryGetProperty("TestA", out testA));
				Assert.Equal(12345, testA.GetInt32());
				JsonElement testB = new JsonElement();
				Assert.True(deserializedPayload?.TryGetProperty("TestB", out testB));
				Assert.Equal("Hello World!", testB.GetString());
			}
		}
		[Fact]
		public async Task SerializedSnapshotLogEntryIsDeserializable() {
			using (MemoryStream stream = new()) {
				var entryOrig = new LogEntry(LogEntry.EntryMetadata.NewSnapshotEntry("TestChannel", DateTime.Now, "TestObject"), new Dictionary<string, object?>() { ["TestA"] = 12345, ["TestB"] = "Hello World!" });
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
				var deserializedPayload = entryDeserialized?.Payload as JsonElement?;
				JsonElement testA = new JsonElement();
				Assert.True(deserializedPayload?.TryGetProperty("TestA", out testA));
				Assert.Equal(12345, testA.GetInt32());
				JsonElement testB = new JsonElement();
				Assert.True(deserializedPayload?.TryGetProperty("TestB", out testB));
				Assert.Equal("Hello World!", testB.GetString());
			}
		}
	}
}
