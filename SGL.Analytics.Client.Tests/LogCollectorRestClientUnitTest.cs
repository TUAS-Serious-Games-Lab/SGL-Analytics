using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace SGL.Analytics.Client.Tests {
	[Collection("Mock Web Server")]
	public class LogCollectorRestClientUnitTest : IDisposable {
		private MockServerFixture serverFixture;
		private LogCollectorRestClient client;
		private InMemoryLogStorage storage;

		public LogCollectorRestClientUnitTest(MockServerFixture serverFixture) {
			this.serverFixture = serverFixture;
			client = new LogCollectorRestClient(new Uri(serverFixture.Server.Urls.First()));
			storage = new InMemoryLogStorage();
		}

		public void Dispose() {
			serverFixture.Reset();
		}

		[Fact]
		public async Task UploadingALogProducesTheExpectedRequest() {
			var userId = Guid.NewGuid();
			var content = "This is a test!" + Environment.NewLine;
			ILogStorage.ILogFile logFile;
			using (var writer = new StreamWriter(storage.CreateLogFile(out logFile))) {
				writer.Write(content);
			}

			var guidMatcher = new RegexMatcher(@"[a-fA-F0-9]{8}[-]([a-fA-F0-9]{4}[-]){3}[a-fA-F0-9]{12}");
			serverFixture.Server.Given(Request.Create().WithPath("/api/AnalyticsLog").UsingPost()
						.WithHeader("AppName", new WildcardMatcher("*"))
						.WithHeader("App-API-Token", new ExactMatcher("*"))
						.WithHeader("UserId", guidMatcher)
						.WithHeader("LogFileId", guidMatcher))
					.RespondWith(Response.Create().WithStatusCode(System.Net.HttpStatusCode.NoContent));

			await client.UploadLogFileAsync("LogCollectorRestClientUnitTest", "FakeApiToken", userId, logFile);

			Assert.Single(serverFixture.Server.LogEntries);
			var logEntry = serverFixture.Server.LogEntries.Single();
			Assert.Equal(content, logEntry.RequestMessage.Body);
			var headers = logEntry.RequestMessage.Headers;
			Assert.Equal("LogCollectorRestClientUnitTest", headers["AppName"].Single());
			Assert.Equal("FakeApiToken", headers["App-API-Token"].Single());
			Assert.Equal(userId, Guid.Parse(headers["UserId"].Single()));
			Assert.Equal(logFile.ID, Guid.Parse(headers["LogFileId"].Single()));
			Assert.Equal(logFile.CreationTime, DateTime.Parse(headers["CreationTime"].Single()));
			Assert.Equal(logFile.EndTime, DateTime.Parse(headers["EndTime"].Single()));
		}
	}
}
