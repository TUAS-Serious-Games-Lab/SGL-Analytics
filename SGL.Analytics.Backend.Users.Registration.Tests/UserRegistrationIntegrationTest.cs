using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.X509;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.DTO;
using SGL.Analytics.ExporterClient;
using SGL.Utilities;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Backend.TestUtilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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

		public RandomGenerator Random { get; } = new RandomGenerator();
		public PublicKey SignerPublicKey => signerKeyPair.Public;
		private KeyPair signerKeyPair;
		public List<Certificate> Certificates = new List<Certificate>();
		public DistinguishedName SignerIdentity { get; } = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test Signer") });

		public KeyPair ExporterKeyPair = null!;
		public Certificate ExporterCertificate = null!;

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

			signerKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
		}

		private string createCertificatePem(string cn, List<Certificate>? certificateList) {
			var keyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			var identity = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", cn) });
			var certificate = Certificate.Generate(SignerIdentity, signerKeyPair.Private, identity, keyPair.Public, TimeSpan.FromHours(1), Random, 128, keyUsages: KeyUsages.KeyEncipherment);
			using var writer = new StringWriter();
			certificate.StoreToPem(writer);
			certificateList?.Add(certificate);
			return writer.ToString();
		}

		private string createKeyAuthCertificatePem(string cn, out KeyPair keyPair, out Certificate certificate) {
			keyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			var identity = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", cn) });
			certificate = Certificate.Generate(SignerIdentity, signerKeyPair.Private, identity, keyPair.Public, TimeSpan.FromHours(1), Random, 128, keyUsages: KeyUsages.DigitalSignature);
			using var writer = new StringWriter();
			certificate.StoreToPem(writer);
			return writer.ToString();
		}

		protected override void SeedDatabase(UsersContext context) {
			var app = ApplicationWithUserProperties.Create(AppName, AppApiToken);
			app.AddProperty("Foo", UserPropertyType.String, true);
			app.AddProperty("Bar", UserPropertyType.String);
			app.AddRecipient("test recipient 1", createCertificatePem("Test 1", Certificates));
			app.AddRecipient("test recipient 2", createCertificatePem("Test 2", Certificates));
			app.AddRecipient("test recipient 3", createCertificatePem("Test 3", Certificates));
			app.AddAuthorizedExporter("test exporter 1", createKeyAuthCertificatePem("Exporter 1", out ExporterKeyPair, out ExporterCertificate));
			context.Applications.Add(app);
			var app2 = ApplicationWithUserProperties.Create(AppName + "_2", AppApiToken + "_2");
			app2.AddProperty("Foo", UserPropertyType.String, true);
			app2.AddProperty("Bar", UserPropertyType.String);
			app2.AddRecipient("other test recipient 1", createCertificatePem("Other Test 1", null));
			app2.AddRecipient("other test recipient 2", createCertificatePem("Other Test 2", null));
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
		public async Task RecipientCertificateListContainsExpectedEntries() {
			using (var client = fixture.CreateClient()) {
				var request = new HttpRequestMessage(HttpMethod.Get, $"/api/analytics/user/v1/recipient-certificates?appName={fixture.AppName}");
				request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/x-pem-file"));
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
				response.EnsureSuccessStatusCode();
				using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
				var readCerts = Certificate.LoadAllFromPem(reader).ToList();
				Assert.Equal(3, readCerts.Count);
				Assert.All(readCerts, cert => Assert.Contains(cert, fixture.Certificates));
				Assert.All(fixture.Certificates, cert => Assert.Contains(cert, readCerts));
			}
		}

		[Fact]
		public async Task ValidUserRegistrationWithUsernameIsSuccessfullyCompleted() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(fixture.AppName, "Testuser1",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(userRegDTO);
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/user/v1");
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
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
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
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
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
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
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
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
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
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
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
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
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
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
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
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
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
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
				Assert.Empty(response.Headers.WwwAuthenticate);
			}
		}
		[Fact]
		public async Task LoginWithEmptyUsernameFailsWithExpectedError() {
			var (userId, secret) = await createTestUserAsync("Testuser16");
			var loginReqDTO = new UsernameBasedLoginRequestDTO(fixture.AppName, fixture.AppApiToken, "", secret);
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			}
		}
		[Fact]
		public async Task LoginWithUsernameAndTooShortSecretFailsWithExpectedError() {
			var (userId, secret) = await createTestUserAsync("Testuser17");
			var loginReqDTO = new UsernameBasedLoginRequestDTO(fixture.AppName, fixture.AppApiToken, "Testuser17", secret.Substring(0, 7));
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			}
		}
		[Fact]
		public async Task LoginWithUserIdAndTooShortSecretFailsWithExpectedError() {
			var (userId, secret) = await createTestUserAsync("Testuser18");
			var loginReqDTO = new IdBasedLoginRequestDTO(fixture.AppName, fixture.AppApiToken, userId, secret.Substring(0, 7));
			using (var client = fixture.CreateClient()) {
				var content = JsonContent.Create(loginReqDTO);
				var response = await client.PostAsJsonAsync("/api/analytics/user/v1/login", loginReqDTO);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			}
		}
		[Fact]
		public async Task KeyAuthWithValidKeyPairWorksCorrectly() {
			using (var client = fixture.CreateClient()) {
				var authenticator = new ExporterKeyPairAuthenticator(client, fixture.ExporterKeyPair, fixture.Services.GetRequiredService<ILogger<ExporterKeyPairAuthenticator>>(), fixture.Random);
				var authData = await authenticator.AuthenticateAsync(fixture.AppName);
				var (principal, validatedToken) = fixture.TokenValidator.Validate(authData.Token.Value);
				Assert.Equal(fixture.ExporterKeyPair.Public.CalculateId(), principal.GetClaim<KeyId>("keyid", KeyId.TryParse!));
				Assert.Equal(fixture.ExporterCertificate.SubjectDN.ToString(), principal.GetClaim("exporter-dn"));
				Assert.Equal(fixture.AppName, principal.GetClaim("appname"));
			}
		}
	}
}
