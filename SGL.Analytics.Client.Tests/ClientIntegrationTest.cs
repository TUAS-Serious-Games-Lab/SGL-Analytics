using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Client.Tests {
	[Collection("Mock Web Server")]
	public class ClientIntegrationTest : IDisposable {
		private const string appName = "SGLAnalyticsClientIntegrationTest";
		private const string appAPIToken = "FakeApiToken";
		private ITestOutputHelper output;
		private ILoggerFactory loggerFactory;
		private MockServerFixture serverFixture;
		private DirectoryLogStorage storage;
		private FileRootDataStore rootDS;
		private LogCollectorRestClient client;
		private SGLAnalytics analytics;
		private bool finished = false;

		public ClientIntegrationTest(ITestOutputHelper output, MockServerFixture serverFixture) {
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output));
			this.serverFixture = serverFixture;

			rootDS = new FileRootDataStore(appName);
			rootDS.UserID = Guid.NewGuid();
			rootDS.SaveAsync().Wait();
			storage = new DirectoryLogStorage(Path.Combine(rootDS.DataDirectory, "DataLogs"));
			client = new LogCollectorRestClient(new Uri(serverFixture.Server.Urls.First()));
			analytics = new SGLAnalytics(appName, appAPIToken, rootDS, storage, client, loggerFactory.CreateLogger<SGLAnalytics>());
		}

		public class SimpleTestEvent {
			public string Name { get; set; } = "";
		}

		[Fact]
		public async Task LogEventsAreRecordedAndUploadedAsLogFilesWithCorrectContent() {
			var guidMatcher = new RegexMatcher(@"[a-fA-F0-9]{8}[-]([a-fA-F0-9]{4}[-]){3}[a-fA-F0-9]{12}");
			serverFixture.Server.Given(Request.Create().WithPath("/api/AnalyticsLog").UsingPost()
						.WithHeader("AppName", new WildcardMatcher("*"))
						.WithHeader("App-API-Token", new WildcardMatcher("*"))
						.WithHeader("UserId", guidMatcher)
						.WithHeader("LogFileId", guidMatcher))
					.RespondWith(Response.Create().WithStatusCode(System.Net.HttpStatusCode.NoContent));

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
			finished = true;

			static void readAndAssertSimpleTestEvent(ref JsonElement.ArrayEnumerator arrEnumerator, string expChannel, string expName) {
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

			static void readAndAssertSimpleSnapshot(ref JsonElement.ArrayEnumerator arrEnumerator, string expChannel, int expObjectId, string expPayload) {
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

			var successfulRequests = serverFixture.Server.LogEntries.Where(le => (int)(le.ResponseMessage.StatusCode) < 300).Select(le => le.RequestMessage);
			foreach (var req in successfulRequests) {
				using (var stream = new GZipStream(new MemoryStream(req.BodyAsBytes), CompressionMode.Decompress)) {
					output.WriteLine("");
					output.WriteLine($"{req.Headers["LogFileId"].Single()}");
					output.WriteLogContents(stream);
				}
			}
			var requestsEnumerator = successfulRequests.GetEnumerator();

			Assert.True(requestsEnumerator.MoveNext());
			await using (var stream = new GZipStream(new MemoryStream(requestsEnumerator.Current.BodyAsBytes), CompressionMode.Decompress)) {
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
			Assert.True(requestsEnumerator.MoveNext());
			await using (var stream = new GZipStream(new MemoryStream(requestsEnumerator.Current.BodyAsBytes), CompressionMode.Decompress)) {
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
			Assert.True(requestsEnumerator.MoveNext());
			await using (var stream = new GZipStream(new MemoryStream(requestsEnumerator.Current.BodyAsBytes), CompressionMode.Decompress)) {
				using (var jsonDoc = await JsonDocument.ParseAsync(stream)) {
					var arrEnumerator = jsonDoc.RootElement.EnumerateArray().GetEnumerator();
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test J");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test K");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 1, "Snap F");
				}
			}
			Assert.False(requestsEnumerator.MoveNext());

		}

		public void Dispose() {
			if (!finished) analytics.FinishAsync().Wait();
			storage.Archiving = false;
			foreach (var log in storage.EnumerateLogs()) {
				log.Remove();
			}
			serverFixture.Reset();
		}
	}
}
