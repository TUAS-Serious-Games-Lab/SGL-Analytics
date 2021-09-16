using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Security;
using SGL.Analytics.Backend.TestUtilities;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.Utilities;
using SGL.Analytics.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using SGL.Analytics.DTO;
using System.Net.Http.Json;
using System.Net.Http;
using System.Net;

namespace SGL.Analytics.Backend.Users.Registration.Tests {
	public class UserRegistrationIntegrationTestFixture : DbWebAppIntegrationTestFixtureBase<UsersContext, Startup> {
		public readonly string AppName = "UserRegistrationIntegrationTest";
		public string AppApiToken { get; } = StringGenerator.GenerateRandomWord(32);
		public JwtOptions JwtOptions { get; } = new JwtOptions() {
			Audience = "UserRegistrationIntegrationTest",
			Issuer = "UserRegistrationIntegrationTest",
			SymmetricKey = "TestingS3cr3tTestingS3cr3t"
		};
		public Dictionary<string, string> JwtConfig { get; }

		public ITestOutputHelper? Output { get; set; } = null;
		public JwtTokenValidator TokenValidator { get; }

		public UserRegistrationIntegrationTestFixture() {
			JwtConfig = new() {
				["Jwt:Audience"] = JwtOptions.Audience,
				["Jwt:Issuer"] = JwtOptions.Issuer,
				["Jwt:SymmetricKey"] = JwtOptions.SymmetricKey,
			};
			TokenValidator = new JwtTokenValidator(JwtOptions.Issuer, JwtOptions.Audience, JwtOptions.SymmetricKey);
		}

		protected override void SeedDatabase(UsersContext context) {
			var app = ApplicationWithUserProperties.Create(AppName, AppApiToken);
			app.AddProperty("Foo", UserPropertyType.String, true);
			app.AddProperty("Bar", UserPropertyType.String);
			context.Applications.Add(app);
			context.SaveChanges();
		}

		protected override IHostBuilder CreateHostBuilder() {
			return base.CreateHostBuilder().ConfigureAppConfiguration(config => config.AddInMemoryCollection(JwtConfig))
				.ConfigureLogging(logging => logging.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Trace));
		}
	}

	public class UserRegistrationIntegrationTest : IClassFixture<UserRegistrationIntegrationTestFixture> {
		private UserRegistrationIntegrationTestFixture fixture;
		private ITestOutputHelper output;

		public UserRegistrationIntegrationTest(UserRegistrationIntegrationTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			this.fixture.Output = output;
		}

		[Fact]
		public async Task ValidUserRegistrationIsSuccessfullyCompleted() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, "Testuser1",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsUser");
				request.Content = content;
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();
				Assert.Equal(HttpStatusCode.Created, response.StatusCode);
				var result = await response.Content.ReadFromJsonAsync<UserRegistrationResultDTO>();
				Assert.NotNull(result);
				Assert.NotEqual(Guid.Empty, result!.UserId);
			}
		}
		[Fact]
		public async Task UserRegistrationWithNonExistentAppFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO("DoesNotExist", "Testuser2",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsUser");
				request.Content = content;
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
			}
		}
		[Fact]
		public async Task UserRegistrationWithIncorrectAppApiTokenFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, "Testuser3",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsUser");
				request.Content = content;
				request.Headers.Add("App-API-Token", "Wrong");
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
			}
		}
		[Fact]
		public async Task UserRegistrationWithUsernameAlreadyInUseFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, "Testuser4",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsUser");
				request.Content = content;
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();
				Assert.Equal(HttpStatusCode.Created, response.StatusCode);
				var result = await response.Content.ReadFromJsonAsync<UserRegistrationResultDTO>();
				Assert.NotNull(result);
				Assert.NotEqual(Guid.Empty, result!.UserId);
			}
			// Attempt to register same username again...
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsUser");
				request.Content = content;
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
			}
		}
		[Fact]
		public async Task UserRegistrationWithUnknownPropertyFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Baz"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, "Testuser5",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsUser");
				request.Content = content;
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			}
		}
		[Fact]
		public async Task UserRegistrationWithMissingRequiredPropertyFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, "Testuser6",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsUser");
				request.Content = content;
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			}
		}
		[Fact]
		public async Task UserRegistrationWithPropertyOfWrongTypeFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = 42 };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, "Testuser7",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsUser");
				request.Content = content;
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			}
		}

		private async Task<(Guid userId, string secret)> createTestUserAsync(string username) {
			var secret = StringGenerator.GenerateRandomWord(16);// Not cryptographic, but ok for test
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, username, secret, props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsUser");
				request.Content = content;
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();
				var result = await response.Content.ReadFromJsonAsync<UserRegistrationResultDTO>();
				return (result?.UserId ?? throw new Exception("Failed to create test user."), secret);
			}
		}
		[Fact]
		public async Task ValidUserCanSuccessfullyLoginWithCorrectCredentials() {
			var (userId, secret) = await createTestUserAsync("Testuser8");
			var loginReqDTO = new LoginRequestDTO(fixture.AppName, fixture.AppApiToken, userId, secret);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/AnalyticsUser/login", loginReqDTO);
				response.EnsureSuccessStatusCode();
				Assert.Equal(HttpStatusCode.OK, response.StatusCode);
				var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>();
				Assert.NotNull(result);
				var token = result!.BearerToken;
				var (principal, validatedToken) = fixture.TokenValidator.Validate(token);
				Assert.True(Guid.TryParse(Assert.Single(principal.Claims, c => c.Type.Equals("userid", StringComparison.OrdinalIgnoreCase)).Value, out var tokenUserId));
				Assert.Equal(userId, tokenUserId);
			}
		}
	}
}
