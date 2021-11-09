using SGL.Analytics.DTO;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace SGL.Analytics.Client.Tests {
	[Collection("Mock Web Server")]
	public class LogCollectorRestClientUnitTest : IDisposable {
		private const string appAPIToken = "FakeApiToken";
		private MockServerFixture serverFixture;
		private LogCollectorRestClient client;
		private InMemoryLogStorage storage;

		public LogCollectorRestClientUnitTest(MockServerFixture serverFixture) {
			this.serverFixture = serverFixture;
			client = new LogCollectorRestClient(new Uri(serverFixture.Server.Urls.First()));
			storage = new InMemoryLogStorage();
		}

		public void Dispose() {
			storage.Dispose();
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
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/log").UsingPost()
						.WithHeader("App-API-Token", new ExactMatcher(appAPIToken))
						.WithHeader("LogFileId", guidMatcher)
						.WithHeader("Authorization", new ExactMatcher("Bearer OK")))
					.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.NoContent));

			await client.UploadLogFileAsync("LogCollectorRestClientUnitTest", appAPIToken, new AuthorizationToken("OK"), logFile);

			Assert.Single(serverFixture.Server.LogEntries);
			var logEntry = serverFixture.Server.LogEntries.Single();
			Assert.Equal(content, logEntry.RequestMessage.Body);
			var headers = logEntry.RequestMessage.Headers;
			Assert.Equal(appAPIToken, headers["App-API-Token"].Single());
			Assert.Equal(logFile.ID, Guid.Parse(headers["LogFileId"].Single()));
			Assert.Equal(logFile.CreationTime, DateTime.Parse(headers["CreationTime"].Single()));
			Assert.Equal(logFile.EndTime, DateTime.Parse(headers["EndTime"].Single()));
		}
		[Fact]
		public async Task ServerErrorsAreCorrectlyReportedByException() {
			var userId = Guid.NewGuid();
			var content = "This is a test!" + Environment.NewLine;
			ILogStorage.ILogFile logFile;
			using (var writer = new StreamWriter(storage.CreateLogFile(out logFile))) {
				writer.Write(content);
			}

			var guidMatcher = new RegexMatcher(@"[a-fA-F0-9]{8}[-]([a-fA-F0-9]{4}[-]){3}[a-fA-F0-9]{12}");
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/log").UsingPost()
						.WithHeader("App-API-Token", new WildcardMatcher("*"))
						.WithHeader("LogFileId", guidMatcher)
						.WithHeader("Authorization", new ExactMatcher("Bearer OK")))
					.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

			var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.UploadLogFileAsync("LogCollectorRestClientUnitTest", appAPIToken, new AuthorizationToken("OK"), logFile));
			Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
		}
	}
}
