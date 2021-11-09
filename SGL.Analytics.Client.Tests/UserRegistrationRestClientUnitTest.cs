using SGL.Analytics.DTO;
using SGL.Utilities.TestUtilities.XUnit;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Client.Tests {
	[Collection("Mock Web Server")]
	public class UserRegistrationRestClientUnitTest : IDisposable {
		private MockServerFixture serverFixture;
		private UserRegistrationRestClient client;
		private ITestOutputHelper output;
		private const string appApiToken = "FakeApiToken";

		public UserRegistrationRestClientUnitTest(MockServerFixture serverFixture, ITestOutputHelper output) {
			this.serverFixture = serverFixture;
			this.output = output;
			client = new UserRegistrationRestClient(new Uri(serverFixture.Server.Urls.First()));
		}

		public void Dispose() {
			serverFixture.Reset();
		}

		[Fact]
		public async Task PerformingAUserRegistrationProducesTheExpectedRequest() {
			Guid userId = Guid.NewGuid();
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/user").UsingPost()
					.WithHeader("App-API-Token", new ExactMatcher(appApiToken))
					.WithHeader("Content-Type", new ExactMatcher("application/json"))
					.WithBody(b => b.DetectedBodyType == WireMock.Types.BodyType.Json))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Created)
					.WithBodyAsJson(new UserRegistrationResultDTO(userId), true));

			var registration = new UserRegistrationDTO("UserRegistrationRestClientUnitTest", "Testuser", "Passw0rd",
				new Dictionary<string, object?>() { ["Number"] = 12345, ["Label"] = "Test" });
			var result = await client.RegisterUserAsync(registration, appApiToken);

			Assert.Single(serverFixture.Server.LogEntries);
			var logEntry = serverFixture.Server.LogEntries.Single();
			using (MemoryStream bodyStream = new MemoryStream(logEntry.RequestMessage.BodyAsBytes)) {
				var requestBodyObj = await JsonSerializer.DeserializeAsync<UserRegistrationDTO>(bodyStream, new JsonSerializerOptions(JsonSerializerDefaults.Web));
				Assert.NotNull(requestBodyObj);
				Assert.Equal(registration.AppName, requestBodyObj?.AppName);
				Assert.Equal(registration.Username, requestBodyObj?.Username);
				Assert.All(registration.StudySpecificProperties, kvp => Assert.Equal(kvp.Value, Assert.Contains(kvp.Key, requestBodyObj?.StudySpecificProperties as IDictionary<string, object?>)));
				bodyStream.Position = 0;
				output.WriteStreamContents(bodyStream);
			}
			Assert.Equal(userId, result.UserId);
		}

		[Fact]
		public async Task HttpErrorInUserRegistrationIsCorrectlyReportedAsException() {
			Guid userId = Guid.NewGuid();
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/user").UsingPost()
					.WithHeader("App-API-Token", new WildcardMatcher("*")))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

			var registration = new UserRegistrationDTO("UserRegistrationRestClientUnitTest", "Testuser", "Passw0rd",
				new Dictionary<string, object?>() { ["Number"] = 12345, ["Label"] = "Test" });

			var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.RegisterUserAsync(registration, appApiToken));
			Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
		}

		[Fact]
		public async Task ConflictInUserRegistrationIsCorrectlyReportedAsUsernameAlreadyTakenException() {
			Guid userId = Guid.NewGuid();
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/user").UsingPost()
					.WithHeader("App-API-Token", new WildcardMatcher("*")))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Conflict)
					.WithBody("The requested username is already taken."));

			var registration = new UserRegistrationDTO("UserRegistrationRestClientUnitTest", "Testuser", "Passw0rd",
				new Dictionary<string, object?>() { ["Number"] = 12345, ["Label"] = "Test" });

			var ex = await Assert.ThrowsAsync<UsernameAlreadyTakenException>(() => client.RegisterUserAsync(registration, appApiToken));
			Assert.Equal(registration.Username, ex.Username);
		}
	}
}
