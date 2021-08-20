using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Client.Tests {
	public class ClientMainUnitTest : IDisposable {

		private InMemoryLogStorage storage = new InMemoryLogStorage();
		private FakeRootDataStore ds = new FakeRootDataStore();
		private SGLAnalytics analytics;
		private ITestOutputHelper output;
		private ILoggerFactory loggerFactory;

		public ClientMainUnitTest(ITestOutputHelper output) {
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			analytics = new SGLAnalytics("SGLAnalyticsUnitTests", "FakeApiKey", ds, storage, logCollectorClient: null, diagnosticsLogger: loggerFactory.CreateLogger<SGLAnalytics>());
			this.output = output;
		}

		public void Dispose() {
			storage.Dispose();
		}

		[Fact]
		public async Task EachStartNewLogCreatesLogFile() {
			Assert.Empty(storage.EnumerateLogs());
			analytics.StartNewLog();
			Assert.Single(storage.EnumerateLogs());
			analytics.StartNewLog();
			Assert.Equal(2, storage.EnumerateLogs().Count());
			analytics.StartNewLog();
			Assert.Equal(3, storage.EnumerateLogs().Count());
			analytics.StartNewLog();
			Assert.Equal(4, storage.EnumerateLogs().Count());
			await analytics.FinishAsync(); // Cleanup background tasks.
		}

		[Fact]
		public async Task AllLogsAreClosedAfterFinish() {
			analytics.StartNewLog();
			analytics.StartNewLog();
			analytics.StartNewLog();
			analytics.StartNewLog();
			analytics.StartNewLog();
			await analytics.FinishAsync();
			Assert.All(storage.EnumerateFinishedLogs().Cast<InMemoryLogStorage.LogFile>(), log => Assert.True(log.WriteClosed));
		}

		public class TestChildObject : ICloneable {
			public string X { get; set; } = "";
			public int Y { get; set; }

			public object Clone() {
				return new TestChildObject() { X = this.X, Y = this.Y };
			}
		}

		public class TestEvent {
			public string SomeString { get; set; } = "";
			public int SomeNumber { get; set; }
			public bool SomeBool { get; set; }
			public object[] SomeArray { get; set; } = new object[0];

		}

		public class ClonableTestEvent : TestEvent, ICloneable {

			public object Clone() {
				return new ClonableTestEvent() { SomeString = this.SomeString, SomeNumber = this.SomeNumber, SomeBool = this.SomeBool, SomeArray = this.SomeArray.Select(elem => (elem as ICloneable)?.Clone()).Cast<ICloneable>().ToArray() };
			}
		}

		[Fact]
		public async Task SharedEventWithoutCustomNameIsStoredAsExpected() {
			analytics.StartNewLog();
			analytics.RecordEvent("TestChannel", new ClonableTestEvent() { SomeNumber = 42, SomeString = "Hello World", SomeBool = true, SomeArray = new object[] { "This is a test!", new TestChildObject() { X = "Test Test Test", Y = 12345 } } });
			await analytics.FinishAsync();

			output.WriteLogContents(storage.EnumerateFinishedLogs().Single());
			await using (var stream = storage.EnumerateFinishedLogs().Single().OpenRead()) {
				var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);
				var entries = json.EnumerateArray();
				Assert.True(entries.MoveNext());
				var entry = entries.Current;

				Assert.True(entry.TryGetProperty("Channel", out var channel));
				Assert.Equal("TestChannel", channel.GetString());
				Assert.True(entry.TryGetProperty("TimeStamp", out var timestamp));
				Assert.True(timestamp.TryGetDateTime(out var timestampDT));
				Assert.True(entry.TryGetProperty("EntryType", out var entryType));
				Assert.Equal("Event", entryType.GetString());
				Assert.True(entry.TryGetProperty("EventType", out var eventType));
				Assert.Equal("ClonableTestEvent", eventType.GetString());

				Assert.True(entry.TryGetProperty("Payload", out var payload));
				Assert.True(payload.TryGetProperty("SomeString", out var someString));
				Assert.Equal("Hello World", someString.GetString());
				Assert.True(payload.TryGetProperty("SomeNumber", out var someNumber));
				Assert.True(someNumber.TryGetInt32(out var someNumberInt));
				Assert.Equal(42, someNumberInt);
				Assert.True(payload.TryGetProperty("SomeBool", out var someBool));
				Assert.True(someBool.GetBoolean());

				Assert.True(payload.TryGetProperty("SomeArray", out var someArrayJson));
				var someArray = someArrayJson.EnumerateArray();
				Assert.True(someArray.MoveNext());
				Assert.Equal("This is a test!", someArray.Current.GetString());
				Assert.True(someArray.MoveNext());
				Assert.True(someArray.Current.TryGetProperty("X", out var childX));
				Assert.Equal("Test Test Test", childX.GetString());
				Assert.True(someArray.Current.TryGetProperty("Y", out var childY));
				Assert.True(childY.TryGetInt32(out var childYInt));
				Assert.Equal(12345, childYInt);
				Assert.False(someArray.MoveNext());
			}
		}

		[Fact]
		public async Task UnsharedEventWithoutCustomNameIsStoredAsExpected() {
			analytics.StartNewLog();
			analytics.RecordEventUnshared("TestChannel", new TestEvent() { SomeNumber = 42, SomeString = "Hello World", SomeBool = true, SomeArray = new object[] { "This is a test!", new TestChildObject() { X = "Test Test Test", Y = 12345 }, 98765 } });
			await analytics.FinishAsync();

			output.WriteLogContents(storage.EnumerateFinishedLogs().Single());
			await using (var stream = storage.EnumerateFinishedLogs().Single().OpenRead()) {
				var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);
				var entries = json.EnumerateArray();
				Assert.True(entries.MoveNext());
				var entry = entries.Current;

				Assert.True(entry.TryGetProperty("Channel", out var channel));
				Assert.Equal("TestChannel", channel.GetString());
				Assert.True(entry.TryGetProperty("TimeStamp", out var timestamp));
				Assert.True(timestamp.TryGetDateTime(out var timestampDT));
				Assert.True(entry.TryGetProperty("EntryType", out var entryType));
				Assert.Equal("Event", entryType.GetString());
				Assert.True(entry.TryGetProperty("EventType", out var eventType));
				Assert.Equal("TestEvent", eventType.GetString());

				Assert.True(entry.TryGetProperty("Payload", out var payload));
				Assert.True(payload.TryGetProperty("SomeString", out var someString));
				Assert.Equal("Hello World", someString.GetString());
				Assert.True(payload.TryGetProperty("SomeNumber", out var someNumber));
				Assert.True(someNumber.TryGetInt32(out var someNumberInt));
				Assert.Equal(42, someNumberInt);
				Assert.True(payload.TryGetProperty("SomeBool", out var someBool));
				Assert.True(someBool.GetBoolean());

				Assert.True(payload.TryGetProperty("SomeArray", out var someArrayJson));
				var someArray = someArrayJson.EnumerateArray();
				Assert.True(someArray.MoveNext());
				Assert.Equal("This is a test!", someArray.Current.GetString());
				Assert.True(someArray.MoveNext());
				Assert.True(someArray.Current.TryGetProperty("X", out var childX));
				Assert.Equal("Test Test Test", childX.GetString());
				Assert.True(someArray.Current.TryGetProperty("Y", out var childY));
				Assert.True(childY.TryGetInt32(out var childYInt));
				Assert.Equal(12345, childYInt);
				Assert.True(someArray.MoveNext());
				Assert.True(someArray.Current.TryGetInt32(out var numberInArray));
				Assert.Equal(98765, numberInArray);
				Assert.False(someArray.MoveNext());
			}
		}
		[EventType("MyEvent")]
		public class TestEventB : TestEvent { }

		[Fact]
		public async Task UnsharedEventWithCustomNameIsStoredAsExpected() {
			analytics.StartNewLog();
			analytics.RecordEventUnshared("TestChannel", new TestEventB() { SomeNumber = 42, SomeString = "Hello World", SomeBool = true, SomeArray = new object[] { "This is a test!", new TestChildObject() { X = "Test Test Test", Y = 12345 }, 98765 } });
			await analytics.FinishAsync();

			output.WriteLogContents(storage.EnumerateFinishedLogs().Single());
			await using (var stream = storage.EnumerateFinishedLogs().Single().OpenRead()) {
				var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);
				var entries = json.EnumerateArray();
				Assert.True(entries.MoveNext());
				var entry = entries.Current;

				Assert.True(entry.TryGetProperty("Channel", out var channel));
				Assert.Equal("TestChannel", channel.GetString());
				Assert.True(entry.TryGetProperty("TimeStamp", out var timestamp));
				Assert.True(timestamp.TryGetDateTime(out var timestampDT));
				Assert.True(entry.TryGetProperty("EntryType", out var entryType));
				Assert.Equal("Event", entryType.GetString());
				Assert.True(entry.TryGetProperty("EventType", out var eventType));
				Assert.Equal("MyEvent", eventType.GetString());

				Assert.True(entry.TryGetProperty("Payload", out var payload));
				Assert.True(payload.TryGetProperty("SomeString", out var someString));
				Assert.Equal("Hello World", someString.GetString());
				Assert.True(payload.TryGetProperty("SomeNumber", out var someNumber));
				Assert.True(someNumber.TryGetInt32(out var someNumberInt));
				Assert.Equal(42, someNumberInt);
				Assert.True(payload.TryGetProperty("SomeBool", out var someBool));
				Assert.True(someBool.GetBoolean());

				Assert.True(payload.TryGetProperty("SomeArray", out var someArrayJson));
				var someArray = someArrayJson.EnumerateArray();
				Assert.True(someArray.MoveNext());
				Assert.Equal("This is a test!", someArray.Current.GetString());
				Assert.True(someArray.MoveNext());
				Assert.True(someArray.Current.TryGetProperty("X", out var childX));
				Assert.Equal("Test Test Test", childX.GetString());
				Assert.True(someArray.Current.TryGetProperty("Y", out var childY));
				Assert.True(childY.TryGetInt32(out var childYInt));
				Assert.Equal(12345, childYInt);
				Assert.True(someArray.MoveNext());
				Assert.True(someArray.Current.TryGetInt32(out var numberInArray));
				Assert.Equal(98765, numberInArray);
				Assert.False(someArray.MoveNext());
			}
		}

		[EventType("MyClonable")]
		public class ClonableTestEventB : TestEvent, ICloneable {
			public object Clone() {
				return new ClonableTestEventB() { SomeString = this.SomeString, SomeNumber = this.SomeNumber, SomeBool = this.SomeBool, SomeArray = this.SomeArray.Select(elem => (elem as ICloneable)?.Clone()).Cast<ICloneable>().ToArray() };
			}
		}

		[Fact]
		public async Task SharedEventWithCustomNameIsStoredAsExpected() {
			analytics.StartNewLog();
			analytics.RecordEvent("TestChannel", new ClonableTestEventB() { SomeNumber = 42, SomeString = "Hello World", SomeBool = true, SomeArray = new object[] { "This is a test!", new TestChildObject() { X = "Test Test Test", Y = 12345 } } });
			await analytics.FinishAsync();

			output.WriteLogContents(storage.EnumerateFinishedLogs().Single());
			await using (var stream = storage.EnumerateFinishedLogs().Single().OpenRead()) {
				var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);
				var entries = json.EnumerateArray();
				Assert.True(entries.MoveNext());
				var entry = entries.Current;

				Assert.True(entry.TryGetProperty("Channel", out var channel));
				Assert.Equal("TestChannel", channel.GetString());
				Assert.True(entry.TryGetProperty("TimeStamp", out var timestamp));
				Assert.True(timestamp.TryGetDateTime(out var timestampDT));
				Assert.True(entry.TryGetProperty("EntryType", out var entryType));
				Assert.Equal("Event", entryType.GetString());
				Assert.True(entry.TryGetProperty("EventType", out var eventType));
				Assert.Equal("MyClonable", eventType.GetString());

				Assert.True(entry.TryGetProperty("Payload", out var payload));
				Assert.True(payload.TryGetProperty("SomeString", out var someString));
				Assert.Equal("Hello World", someString.GetString());
				Assert.True(payload.TryGetProperty("SomeNumber", out var someNumber));
				Assert.True(someNumber.TryGetInt32(out var someNumberInt));
				Assert.Equal(42, someNumberInt);
				Assert.True(payload.TryGetProperty("SomeBool", out var someBool));
				Assert.True(someBool.GetBoolean());

				Assert.True(payload.TryGetProperty("SomeArray", out var someArrayJson));
				var someArray = someArrayJson.EnumerateArray();
				Assert.True(someArray.MoveNext());
				Assert.Equal("This is a test!", someArray.Current.GetString());
				Assert.True(someArray.MoveNext());
				Assert.True(someArray.Current.TryGetProperty("X", out var childX));
				Assert.Equal("Test Test Test", childX.GetString());
				Assert.True(someArray.Current.TryGetProperty("Y", out var childY));
				Assert.True(childY.TryGetInt32(out var childYInt));
				Assert.Equal(12345, childYInt);
				Assert.False(someArray.MoveNext());
			}
		}

		[Fact]
		public async Task SnapshotIsStoredAsExpected() {
			analytics.StartNewLog();
			var snap = new Dictionary<string, object?>();
			snap["TestNumber"] = 12345;
			snap["TestString"] = "Hello World";
			snap["TestBool"] = true;
			snap["TestArray"] = new object[] { 9876, false, "TestTestTest", new Dictionary<string, object?> { ["Foo"] = "Bar" } };
			snap["TestObject"] = new Dictionary<string, object?> { ["A"] = 456, ["B"] = "Baz", ["C"] = null };
			analytics.RecordSnapshotUnshared("TestChannel", 42, snap);
			await analytics.FinishAsync();

			output.WriteLogContents(storage.EnumerateFinishedLogs().Single());
			await using (var stream = storage.EnumerateFinishedLogs().Single().OpenRead()) {
				var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);
				var entries = json.EnumerateArray();
				Assert.True(entries.MoveNext());
				var entry = entries.Current;

				Assert.True(entry.TryGetProperty("Channel", out var channel));
				Assert.Equal("TestChannel", channel.GetString());
				Assert.True(entry.TryGetProperty("TimeStamp", out var timestamp));
				Assert.True(timestamp.TryGetDateTime(out var timestampDT));
				Assert.True(entry.TryGetProperty("EntryType", out var entryType));
				Assert.Equal("Snapshot", entryType.GetString());
				Assert.True(entry.TryGetProperty("ObjectID", out var objectId));
				Assert.True(objectId.TryGetInt32(out var objectIdInt));
				Assert.Equal(42, objectIdInt);

				Assert.True(entry.TryGetProperty("Payload", out var payload));
				Assert.True(payload.TryGetProperty("TestNumber", out var testNumber));
				Assert.True(testNumber.TryGetInt32(out var testNumberInt));
				Assert.Equal(12345, testNumberInt);
				Assert.True(payload.TryGetProperty("TestString", out var testString));
				Assert.Equal("Hello World", testString.GetString());
				Assert.True(payload.TryGetProperty("TestBool", out var testBool));
				Assert.True(testBool.GetBoolean());

				Assert.True(payload.TryGetProperty("TestArray", out var testArray));
				Assert.Equal(4, testArray.GetArrayLength());
				var testArr = testArray.EnumerateArray();

				Assert.True(testArr.MoveNext());
				Assert.True(testArr.Current.TryGetInt32(out var testArr0));
				Assert.Equal(9876, testArr0);

				Assert.True(testArr.MoveNext());
				Assert.False(testArr.Current.GetBoolean());

				Assert.True(testArr.MoveNext());
				Assert.Equal("TestTestTest", testArr.Current.GetString());

				Assert.True(testArr.MoveNext());
				Assert.True(testArr.Current.TryGetProperty("Foo", out var testArr3Foo));
				Assert.Equal("Bar", testArr3Foo.GetString());

				Assert.True(payload.TryGetProperty("TestObject", out var testObject));
				Assert.True(testObject.TryGetProperty("A", out var testObjectA));
				Assert.True(testObjectA.TryGetInt32(out var testObjectAInt));
				Assert.Equal(456, testObjectAInt);
				Assert.True(testObject.TryGetProperty("B", out var testObjectB));
				Assert.Equal("Baz", testObjectB.GetString());
				Assert.True(testObject.TryGetProperty("C", out var testObjectC));
				Assert.Equal(JsonValueKind.Null, testObjectC.ValueKind);
			}
		}

		public class SimpleTestEvent {
			public string Name { get; set; } = "";
		}

		private static void readAndAssertSimpleTestEvent(ref JsonElement.ArrayEnumerator arrEnumerator, string expChannel, string expName) {
			Assert.True(arrEnumerator.MoveNext());
			var entryElem = arrEnumerator.Current;
			Assert.True(entryElem.TryGetProperty("Channel", out var actChannel));
			Assert.Equal(expChannel, actChannel.GetString());
			Assert.True(entryElem.TryGetProperty("EntryType", out var actEntryType));
			Assert.Equal("Event", actEntryType.GetString());
			Assert.True(entryElem.TryGetProperty("EventType", out var actEventType));
			Assert.Equal("SimpleTestEvent", actEventType.GetString());
			Assert.True(entryElem.TryGetProperty("Payload", out var payload));
			Assert.True(payload.TryGetProperty("Name", out var actName));
			Assert.Equal(expName, actName.GetString());
		}

		private static void readAndAssertSimpleSnapshot(ref JsonElement.ArrayEnumerator arrEnumerator, string expChannel, int expObjectId, string expPayload) {
			Assert.True(arrEnumerator.MoveNext());
			var entryElem = arrEnumerator.Current;
			Assert.True(entryElem.TryGetProperty("Channel", out var actChannel));
			Assert.Equal(expChannel, actChannel.GetString());
			Assert.True(entryElem.TryGetProperty("EntryType", out var actEntryType));
			Assert.Equal("Snapshot", actEntryType.GetString());
			Assert.True(entryElem.TryGetProperty("ObjectID", out var actObjectIdElem));
			Assert.True(actObjectIdElem.TryGetInt32(out var actObjectId));
			Assert.Equal(expObjectId, actObjectId);
			Assert.True(entryElem.TryGetProperty("Payload", out var payload));
			Assert.Equal(expPayload, payload.GetString());
		}

		[Fact]
		public async Task RecordedEntriesAreWrittenToTheCorrectLogFile() {
			analytics.StartNewLog();
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test A" });
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test B" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test C" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap A");
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test D" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap B");
			analytics.RecordSnapshotUnshared("Channel 3", 2, "Snap C");

			analytics.StartNewLog();
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test E" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test F" });
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test G" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap D");
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test H" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test I" });
			analytics.RecordSnapshotUnshared("Channel 3", 2, "Snap E");

			analytics.StartNewLog();
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test J" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test K" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap F");

			await analytics.FinishAsync();

			foreach (var logFile in storage.EnumerateFinishedLogs()) {
				output.WriteLine("");
				output.WriteLine($"{logFile.ID}:");
				output.WriteLogContents(logFile);
			}

			var logs = storage.EnumerateFinishedLogs().GetEnumerator();
			Assert.True(logs.MoveNext());
			await using (var stream = logs.Current.OpenRead()) {
				using (var jsonDoc = await JsonDocument.ParseAsync(stream)) {
					var arrEnumerator = jsonDoc.RootElement.EnumerateArray().GetEnumerator();
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test A");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test B");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test C");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 1, "Snap A");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test D");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 1, "Snap B");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 2, "Snap C");
				}
			}
			Assert.True(logs.MoveNext());
			await using (var stream = logs.Current.OpenRead()) {
				using (var jsonDoc = await JsonDocument.ParseAsync(stream)) {
					var arrEnumerator = jsonDoc.RootElement.EnumerateArray().GetEnumerator();
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test E");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test F");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test G");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 1, "Snap D");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test H");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test I");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 2, "Snap E");
				}
			}
			Assert.True(logs.MoveNext());
			await using (var stream = logs.Current.OpenRead()) {
				using (var jsonDoc = await JsonDocument.ParseAsync(stream)) {
					var arrEnumerator = jsonDoc.RootElement.EnumerateArray().GetEnumerator();
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test J");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test K");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 1, "Snap F");
				}
			}
			Assert.False(logs.MoveNext());
		}
		[Fact]
		public async Task PreviousPendingLogFilesAreUploadedOnRegisteredStartup() {
			await analytics.FinishAsync(); // In this test, we will not use the analytics object provided from the test class constructor, so clean it up before we replace it shortly.

			List<ILogStorage.ILogFile> logs = new();
			for (int i = 0; i < 5; ++i) {
				using (var writer = new StreamWriter(storage.CreateLogFile(out var logFile))) {
					writer.WriteLine($"Dummy {i}");
					logs.Add(logFile);
				}
			}

			ds.UserID = Guid.NewGuid();
			var collectorClient = new FakeLogCollectorClient();
			analytics = new SGLAnalytics("SGLAnalyticsUnitTests", "FakeApiKey", ds, storage, collectorClient, loggerFactory.CreateLogger<SGLAnalytics>());
			await analytics.FinishAsync();

			Assert.Equal(5, collectorClient.UploadedLogFileIds.Count);
			foreach (var log in logs) {
				Assert.Contains(log.ID, collectorClient.UploadedLogFileIds);
			}
		}

		[Fact]
		public async Task FailedUploadsAreRetriedOnStartup() {
			await analytics.FinishAsync(); // In this test, we will not use the analytics object provided from the test class constructor, so clean it up before we replace it shortly.
			ds.UserID = Guid.NewGuid();
			var collectorClient = new FakeLogCollectorClient();
			collectorClient.StatusCode = HttpStatusCode.InternalServerError;
			analytics = new SGLAnalytics("SGLAnalyticsUnitTests", "FakeApiKey", ds, storage, collectorClient, loggerFactory.CreateLogger<SGLAnalytics>());
			List<Guid> logIds = new();
			logIds.Add(analytics.StartNewLog());
			logIds.Add(analytics.StartNewLog());
			logIds.Add(analytics.StartNewLog());
			await analytics.FinishAsync();
			Assert.Empty(collectorClient.UploadedLogFileIds);

			collectorClient.StatusCode = HttpStatusCode.NoContent;
			analytics = new SGLAnalytics("SGLAnalyticsUnitTests", "FakeApiKey", ds, storage, collectorClient, loggerFactory.CreateLogger<SGLAnalytics>());
			await analytics.FinishAsync();
			Assert.Equal(logIds, collectorClient.UploadedLogFileIds);
		}

		[Fact]
		public async Task OperationCanBeResumedAfterFinishCompleted() {
			await analytics.FinishAsync(); // In this test, we will not use the analytics object provided from the test class constructor, so clean it up before we replace it shortly.
			ds.UserID = Guid.NewGuid();
			var collectorClient = new FakeLogCollectorClient();
			analytics = new SGLAnalytics("SGLAnalyticsUnitTests", "FakeApiKey", ds, storage, collectorClient, loggerFactory.CreateLogger<SGLAnalytics>());
			List<Guid> logIds = new();

			logIds.Add(analytics.StartNewLog());
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test A" });
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test B" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test C" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap A");
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test D" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap B");
			analytics.RecordSnapshotUnshared("Channel 3", 2, "Snap C");

			await analytics.FinishAsync();

			// Atempt to resume operation:
			logIds.Add(analytics.StartNewLog());
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test E" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test F" });
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test G" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap D");
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test H" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test I" });
			analytics.RecordSnapshotUnshared("Channel 3", 2, "Snap E");

			logIds.Add(analytics.StartNewLog());
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test J" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test K" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap F");
			var lastLog = storage.EnumerateLogs().Last();

			await analytics.FinishAsync();

			Assert.Equal(logIds, collectorClient.UploadedLogFileIds);

			await using (var stream = lastLog.OpenRead()) {
				using (var jsonDoc = await JsonDocument.ParseAsync(stream)) {
					var arrEnumerator = jsonDoc.RootElement.EnumerateArray().GetEnumerator();
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test J");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test K");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 1, "Snap F");
				}
			}

		}
	}
}
