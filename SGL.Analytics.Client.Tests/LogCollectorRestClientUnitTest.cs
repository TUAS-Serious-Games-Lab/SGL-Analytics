using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.EndToEnd;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
			client = new LogCollectorRestClient(serverFixture.Server.CreateClient());
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
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/log/v2").UsingPost()
						.WithHeader("App-API-Token", new ExactMatcher(appAPIToken))
						.WithHeader("Content-Type", new ContentTypeMatcher("multipart/form-data"))
						.WithHeader("Authorization", new ExactMatcher("Bearer OK")))
					.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.NoContent));

			var metadataDTO = new LogMetadataDTO(logFile.ID, logFile.CreationTime, logFile.EndTime, logFile.Suffix, logFile.Encoding, EncryptionInfo.CreateUnencrypted());
			await using var stream = logFile.OpenReadRaw();
			await client.UploadLogFileAsync("LogCollectorRestClientUnitTest", appAPIToken, new AuthorizationToken("OK"), metadataDTO, stream);

			Assert.Single(serverFixture.Server.LogEntries);
			var logEntry = serverFixture.Server.LogEntries.Single();
			var headers = logEntry.RequestMessage.Headers;
			Assert.Equal(appAPIToken, headers?["App-API-Token"]?.Single());
			var contentParts = MultipartBodySplitter.SplitMultipartBody(logEntry.RequestMessage.BodyAsBytes, MultipartBodySplitter.GetBoundaryFromContentType(logEntry.RequestMessage?.Headers?["Content-Type"].First())).ToList();
			Assert.Equal("application/json", contentParts[0].SectionHeaders["Content-Type"]);
			Assert.Equal("form-data; name=metadata", contentParts[0].SectionHeaders["Content-Disposition"]);
			using var metadataStream = new MemoryStream(contentParts[0].Content, writable: false);
			var metadata = await JsonSerializer.DeserializeAsync<LogMetadataDTO>(metadataStream, JsonOptions.RestOptions);

			Assert.Equal("application/octet-stream", contentParts[1].SectionHeaders["Content-Type"]);
			Assert.Equal("form-data; name=content", contentParts[1].SectionHeaders["Content-Disposition"]);
			Assert.Equal(content, Encoding.UTF8.GetString(contentParts[1].Content));

			Assert.Equal(logFile.ID, metadata?.LogFileId);
			Assert.Equal(logFile.CreationTime, metadata?.CreationTime);
			Assert.Equal(logFile.EndTime, metadata?.EndTime);
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
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/log/v2").UsingPost()
						.WithHeader("App-API-Token", new WildcardMatcher("*"))
						.WithHeader("Content-Type", new ContentTypeMatcher("multipart/form-data"))
						.WithHeader("Authorization", new ExactMatcher("Bearer OK")))
					.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

			var metadataDTO = new LogMetadataDTO(logFile.ID, logFile.CreationTime, logFile.EndTime, logFile.Suffix, logFile.Encoding, EncryptionInfo.CreateUnencrypted());
			await using var stream = logFile.OpenReadRaw();
			var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.UploadLogFileAsync("LogCollectorRestClientUnitTest", appAPIToken, new AuthorizationToken("OK"), metadataDTO, stream));
			Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
		}
	}
}
