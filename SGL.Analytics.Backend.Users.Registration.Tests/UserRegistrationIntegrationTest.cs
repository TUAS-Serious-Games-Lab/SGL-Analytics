﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Backend.TestUtilities;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Users.Registration.Tests {
	public class UserRegistrationIntegrationTestFixture : DbWebAppIntegrationTestFixtureBase<UsersContext, Startup> {
		public readonly string AppName = "UserRegistrationIntegrationTest";
		public string AppApiToken { get; } = StringGenerator.GenerateRandomWord(32);
		public JwtOptions JwtOptions { get; } = new JwtOptions() {
			Audience = "UserRegistrationIntegrationTest",
			Issuer = "UserRegistrationIntegrationTest",
			SymmetricKey = "TestingS3cr3tTestingS3cr3t"
		};
		public Dictionary<string, string> Config { get; }

		public ITestOutputHelper? Output { get; set; } = null;
		public JwtTokenValidator TokenValidator { get; }

		public UserRegistrationIntegrationTestFixture() {
			Config = new() {
				["Jwt:Audience"] = JwtOptions.Audience,
				["Jwt:Issuer"] = JwtOptions.Issuer,
				["Jwt:SymmetricKey"] = JwtOptions.SymmetricKey,
				["Jwt:LoginService:FailureDelay"] = TimeSpan.FromMilliseconds(400).ToString(),
				["Logging:File:BaseDirectory"] = "logs/{ServiceName}",
				["Logging:File:Sinks:0:FilenameFormat"] = "{Time:yyyy-MM}/{Time:yyyy-MM-dd}_{ServiceName}.log",
				["Logging:File:Sinks:1:FilenameFormat"] = "{Time:yyyy-MM}/Categories/{Category}.log",
				["Logging:File:Sinks:2:FilenameFormat"] = "{Time:yyyy-MM}/Requests/{RequestId}.log",
				["Logging:File:Sinks:2:MessageFormat"] = "[{RequestPath}] [{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}",
				["Logging:File:Sinks:2:MessageFormatException"] = "[{RequestPath}] [{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}",
				["Logging:File:Sinks:3:FilenameFormat"] = "{Time:yyyy-MM}/users/{UserId}/{Time:yyyy-MM-dd}_{ServiceName}_{UserId}.log",
			};
			TokenValidator = new JwtTokenValidator(JwtOptions.Issuer, JwtOptions.Audience, JwtOptions.SymmetricKey);
		}

		protected override void SeedDatabase(UsersContext context) {
			var app = ApplicationWithUserProperties.Create(AppName, AppApiToken);
			app.AddProperty("Foo", UserPropertyType.String, true);
			app.AddProperty("Bar", UserPropertyType.String);
			context.Applications.Add(app);
			var app2 = ApplicationWithUserProperties.Create(AppName + "_2", AppApiToken + "_2");
			app2.AddProperty("Foo", UserPropertyType.String, true);
			app2.AddProperty("Bar", UserPropertyType.String);
			context.Applications.Add(app2);
			context.SaveChanges();
		}

		protected override IHostBuilder CreateHostBuilder() {
			return base.CreateHostBuilder().ConfigureAppConfiguration(config => config.AddInMemoryCollection(Config))
				.ConfigureLogging(logging => logging.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Trace));
		}
	}

	public class UserRegistrationIntegrationTest : IClassFixture<UserRegistrationIntegrationTestFixture> {
		private UserRegistrationIntegrationTestFixture fixture;
		private ITestOutputHelper output;
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

		public UserRegistrationIntegrationTest(UserRegistrationIntegrationTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			this.fixture.Output = output;
		}

		[Fact]
		public async Task ValidUserRegistrationWithUsernameIsSuccessfullyCompleted() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, "Testuser1",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
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
		public async Task ValidUserRegistrationWithoutUsernameIsSuccessfullyCompleted() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, null,
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
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
		public async Task UserRegistrationWithPresentButEmptyUsernameFailsWithBadRequestError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, "",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
				request.Content = content;
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				output.WriteStreamContents(response.Content.ReadAsStream());
			}
		}
		[Fact]
		public async Task UserRegistrationWithTooShortSecretFailsWithBadRequestError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, null,
				StringGenerator.GenerateRandomWord(7),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
				request.Content = content;
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				output.WriteStreamContents(response.Content.ReadAsStream());
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
				request.Content = content;
				request.Headers.Add("App-API-Token", "WrongToken");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user");
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
			var loginReqDTO = new IdBasedLoginRequestDTO(fixture.AppName, fixture.AppApiToken, userId, secret);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/login", loginReqDTO);
				response.EnsureSuccessStatusCode();
				Assert.Equal(HttpStatusCode.OK, response.StatusCode);
				var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>(jsonOptions);
				Assert.NotNull(result);
				var token = result!.Token;
				var (principal, validatedToken) = fixture.TokenValidator.Validate(token.Value);
				Assert.Equal(userId, principal.GetClaim<Guid>("userid", Guid.TryParse));
				Assert.Equal(fixture.AppName, principal.GetClaim("appname"));
			}
		}
		[Fact]
		public async Task LoginWithNonExistentUserIdFailsWithExpectedError() {
			var secret = StringGenerator.GenerateRandomWord(16);// Not cryptographic, but ok for test
			var userId = Guid.NewGuid();
			var loginReqDTO = new IdBasedLoginRequestDTO(fixture.AppName, fixture.AppApiToken, userId, secret);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
				Assert.Empty(response.Headers.WwwAuthenticate);
			}
		}
		[Fact]
		public async Task LoginWithIdAndIncorrectSecretFailsWithExpectedError() {
			var (userId, secret) = await createTestUserAsync("Testuser10");
			var loginReqDTO = new IdBasedLoginRequestDTO(fixture.AppName, fixture.AppApiToken, userId, "WrongSecret");
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
				Assert.Empty(response.Headers.WwwAuthenticate);
			}
		}
		[Fact]
		public async Task LoginWithNonExistentAppFailsWithExpectedError() {
			var (userId, secret) = await createTestUserAsync("Testuser11");
			var loginReqDTO = new IdBasedLoginRequestDTO("DoesNotExist", fixture.AppApiToken, userId, secret);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
				Assert.Empty(response.Headers.WwwAuthenticate);
			}
		}
		[Fact]
		public async Task LoginWithIncorrectAppApiTokenFailsWithExpectedError() {
			var (userId, secret) = await createTestUserAsync("Testuser12");
			var loginReqDTO = new IdBasedLoginRequestDTO(fixture.AppName, "WrongToken", userId, secret);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
				Assert.Empty(response.Headers.WwwAuthenticate);
			}
		}
		[Fact]
		public async Task LoginWithUnmatchingAppAndUserIdFailsWithExpectedError() {
			// Create user for UserRegistrationIntegrationTest
			var (userId, secret) = await createTestUserAsync("Testuser13");
			// But attempt to login with UserRegistrationIntegrationTest_2
			var loginReqDTO = new IdBasedLoginRequestDTO(fixture.AppName + "_2", fixture.AppApiToken + "_2", userId, secret);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
				Assert.Empty(response.Headers.WwwAuthenticate);
			}
		}

		[Fact]
		public async Task LoginWithUsernameAndCorrectPasswordSucceeds() {
			var (userId, secret) = await createTestUserAsync("Testuser14");
			var loginReqDTO = new UsernameBasedLoginRequestDTO(fixture.AppName, fixture.AppApiToken, "Testuser14", secret);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/login", loginReqDTO);
				response.EnsureSuccessStatusCode();
				Assert.Equal(HttpStatusCode.OK, response.StatusCode);
				var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>(jsonOptions);
				Assert.NotNull(result);
				var token = result!.Token;
				var (principal, validatedToken) = fixture.TokenValidator.Validate(token.Value);
				Assert.Equal(userId, principal.GetClaim<Guid>("userid", Guid.TryParse));
				Assert.Equal(fixture.AppName, principal.GetClaim("appname"));
			}
		}
		[Fact]
		public async Task LoginWithUsernameAndIncorrectPasswordFailsWithExpectedError() {
			var (userId, secret) = await createTestUserAsync("Testuser15");
			var loginReqDTO = new UsernameBasedLoginRequestDTO(fixture.AppName, fixture.AppApiToken, "Testuser15", "WrongSecret");
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
				Assert.Empty(response.Headers.WwwAuthenticate);
			}
		}
		[Fact]
		public async Task LoginWithIncorrectUsernameFailsWithExpectedError() {
			var secret = StringGenerator.GenerateRandomWord(16);// Not cryptographic, but ok for test
			var loginReqDTO = new UsernameBasedLoginRequestDTO(fixture.AppName, fixture.AppApiToken, "DoesNotExist", secret);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
				Assert.Empty(response.Headers.WwwAuthenticate);
			}
		}
	}
}
