using Microsoft.Extensions.Logging;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
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
		private LogCollectorRestClient logCollectorClient;
		private UserRegistrationRestClient userRegClient;
		private SGLAnalytics analytics;
		private bool finished = false;

		public ClientIntegrationTest(ITestOutputHelper output, MockServerFixture serverFixture) {
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			this.serverFixture = serverFixture;

			File.Delete(new FileRootDataStore(appName).StorageFilePath);
			rootDS = new FileRootDataStore(appName);
			storage = new DirectoryLogStorage(Path.Combine(rootDS.DataDirectory, "DataLogs"));
			storage.Archiving = false;
			foreach (var log in storage.EnumerateLogs()) {
				log.Remove();
			}
			logCollectorClient = new LogCollectorRestClient(new Uri(serverFixture.Server.Urls.First()));
			userRegClient = new UserRegistrationRestClient(new Uri(serverFixture.Server.Urls.First()));
			analytics = new SGLAnalytics(appName, appAPIToken,
				rootDataStore: rootDS,
				logStorage: storage,
				logCollectorClient: logCollectorClient,
				userRegistrationClient: userRegClient,
				diagnosticsLogger: loggerFactory.CreateLogger<SGLAnalytics>());
		}

		public class SimpleTestEvent {
			public string Name { get; set; } = "";
		}

		public class TestUserData : BaseUserData {
			public TestUserData(string? username) : base(username) { }
			public string Label { get; set; } = "";
			public DateTime RegistrationTime { get; set; } = DateTime.Now;
			public int SomeNumber { get; set; } = 0;
		}

		[Theory]
		[InlineData("Testuser")]
		[InlineData(null)]
		public async Task LogEventsAreRecordedAndUploadedAsLogFilesWithCorrectContentAfterRegistration(string? username) {
			var guidMatcher = new RegexMatcher(@"[a-fA-F0-9]{8}[-]([a-fA-F0-9]{4}[-]){3}[a-fA-F0-9]{12}");
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/log").UsingPost()
						.WithHeader("App-API-Token", new ExactMatcher(appAPIToken))
						.WithHeader("LogFileId", guidMatcher)
						.WithHeader("Authorization", new ExactMatcher("Bearer OK")))
					.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.NoContent));
			Guid userId = Guid.NewGuid();
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/user").UsingPost()
					.WithHeader("App-API-Token", new ExactMatcher(appAPIToken))
					.WithHeader("Content-Type", new ExactMatcher("application/json"))
					.WithBody(b => b.DetectedBodyType == WireMock.Types.BodyType.Json))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Created)
					.WithBodyAsJson(new UserRegistrationResultDTO(userId), true));
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/user/login").UsingPost()
					.WithHeader("Content-Type", new ExactMatcher("application/json"))
					.WithBody(b => b.DetectedBodyType == WireMock.Types.BodyType.Json))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK)
					.WithBodyAsJson(new LoginResponseDTO(new AuthorizationToken("OK"))));

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

			var user = new TestUserData(username) { Label = "This is a test!", SomeNumber = 42 };
			await analytics.RegisterAsync(user);

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

			var successfulRegRequests = serverFixture.Server.LogEntries
				.Where(le => (int)(le.ResponseMessage.StatusCode) < 300 && le.RequestMessage.Path == "/api/analytics/user")
				.Select(le => le.RequestMessage);
			Assert.Single(successfulRegRequests);
			await using (var stream = new MemoryStream(successfulRegRequests.Single().BodyAsBytes)) {
				var userReg = await JsonSerializer.DeserializeAsync<UserRegistrationDTO>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web));
				Assert.Equal(user.Username, userReg?.Username);
				var studyAttr = userReg?.StudySpecificProperties as IDictionary<string, object?>;
				Assert.Equal(user.Label, Assert.Contains("Label", studyAttr));
				Assert.Equal(user.RegistrationTime, Assert.Contains("RegistrationTime", studyAttr));
				Assert.Equal(user.SomeNumber, Assert.Contains("SomeNumber", studyAttr));
				stream.Position = 0;
				output.WriteLine("");
				output.WriteLine("Registration:");
				output.WriteStreamContents(stream);
			}

			var successfulLogRequests = serverFixture.Server.LogEntries
				.Where(le => (int)(le.ResponseMessage.StatusCode) < 300 && le.RequestMessage.Path == "/api/analytics/log")
				.Select(le => le.RequestMessage);
			foreach (var req in successfulLogRequests) {
				using (var stream = new GZipStream(new MemoryStream(req.BodyAsBytes), CompressionMode.Decompress)) {
					output.WriteLine("");
					output.WriteLine($"{req.Headers?["LogFileId"]?.Single() ?? "null"}");
					output.WriteStreamContents(stream);
				}
			}
			var requestsEnumerator = successfulLogRequests.GetEnumerator();

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
			File.Delete(rootDS.StorageFilePath);
			serverFixture.Reset();
		}
	}
}
