using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

		public ClientMainUnitTest(ITestOutputHelper output) {
			analytics = new SGLAnalytics("SGLAnalyticsUnitTests", "FakeApiKey", ds, storage);
			this.output = output;
		}

		public void Dispose() {
			storage.Dispose();
		}

		[Fact]
		public void EachStartNewLogCreatesLogFile() {
			Assert.Empty(storage.EnumerateLogs());
			analytics.StartNewLog();
			Assert.Single(storage.EnumerateLogs());
			analytics.StartNewLog();
			Assert.Equal(2, storage.EnumerateLogs().Count());
			analytics.StartNewLog();
			Assert.Equal(3, storage.EnumerateLogs().Count());
			analytics.StartNewLog();
			Assert.Equal(4, storage.EnumerateLogs().Count());
		}

		[Fact]
		public async Task AllLogsAreClosedAfterFinish() {
			analytics.StartNewLog();
			analytics.StartNewLog();
			analytics.StartNewLog();
			analytics.StartNewLog();
			analytics.StartNewLog();
			await analytics.FinishAsync();
			Assert.All(storage.EnumerateLogs().Cast<InMemoryLogStorage.LogFile>(), log => Assert.True(log.WriteClosed));
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

			output.WriteLogContents(storage.EnumerateLogs().Single());
			await using (var stream = storage.EnumerateLogs().Single().OpenRead()) {
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

			output.WriteLogContents(storage.EnumerateLogs().Single());
			await using (var stream = storage.EnumerateLogs().Single().OpenRead()) {
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

			output.WriteLogContents(storage.EnumerateLogs().Single());
			await using (var stream = storage.EnumerateLogs().Single().OpenRead()) {
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

			output.WriteLogContents(storage.EnumerateLogs().Single());
			await using (var stream = storage.EnumerateLogs().Single().OpenRead()) {
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

			output.WriteLogContents(storage.EnumerateLogs().Single());
			await using (var stream = storage.EnumerateLogs().Single().OpenRead()) {
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
	}
}
