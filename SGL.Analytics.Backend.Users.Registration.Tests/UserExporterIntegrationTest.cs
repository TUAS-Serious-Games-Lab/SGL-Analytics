using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.ExporterClient;
using SGL.Utilities;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Backend.TestUtilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Users.Registration.Tests {
	public class UserExporterIntegrationTestFixture : DbWebAppIntegrationTestFixtureBase<UsersContext, Startup> {
		public readonly string AppName = "UserExporterIntegrationTest";
		public string AppApiToken { get; } = StringGenerator.GenerateRandomWord(32);
		public JwtOptions JwtOptions { get; } = new JwtOptions() {
			Audience = "UserExporterIntegrationTest",
			Issuer = "UserExporterIntegrationTest",
			SymmetricKey = "TestingS3cr3tTestingS3cr3tTestingS3cr3t"
		};
		public Dictionary<string, string> Config { get; }

		public ITestOutputHelper? Output { get; set; } = null;
		public JwtTokenValidator TokenValidator { get; }

		public RandomGenerator Random { get; } = new RandomGenerator();
		public PublicKey SignerPublicKey => signerKeyPair.Public;
		private KeyPair signerKeyPair;
		public List<Certificate> Certificates = new List<Certificate>();
		public DistinguishedName SignerIdentity { get; } = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test Signer") });

		public KeyPair RecipientKeyPair = null!;
		public KeyPair ExporterKeyPair = null!;
		public Certificate ExporterCertificate = null!;

		public byte[] User1Props = null!;
		public byte[] User2Props = null!;
		public byte[] User3Props = null!;

		public Guid User1Id;
		public Guid User2Id;
		public Guid User3Id;
		public Guid OtherAppUserId;

		public UserExporterIntegrationTestFixture() {
			Config = new() {
				["Jwt:Audience"] = JwtOptions.Audience,
				["Jwt:Issuer"] = JwtOptions.Issuer,
				["Jwt:SymmetricKey"] = JwtOptions.SymmetricKey,
				["Jwt:LoginService:FailureDelay"] = TimeSpan.FromMilliseconds(400).ToString(),
				["Logging:File:BaseDirectory"] = "logs/SGL.Analytics.UserExporter",
				["Logging:File:Sinks:0:FilenameFormat"] = "{Time:yyyy-MM}/{Time:yyyy-MM-dd}_{ServiceName}.log",
				["Logging:File:Sinks:1:FilenameFormat"] = "{Time:yyyy-MM}/Categories/{Category}.log",
				["Logging:File:Sinks:2:FilenameFormat"] = "{Time:yyyy-MM}/Requests/{RequestId}.log",
				["Logging:File:Sinks:2:MessageFormat"] = "[{RequestPath}] [{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}",
				["Logging:File:Sinks:2:MessageFormatException"] = "[{RequestPath}] [{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}",
				["Logging:File:Sinks:3:FilenameFormat"] = "{Time:yyyy-MM}/users/{UserId}/{Time:yyyy-MM-dd}_{ServiceName}_{UserId}.log",
				["Logging:LogLevel:Default"] = "Debug",
				["Logging:LogLevel:Microsoft"] = "Information",
			};
			TokenValidator = new JwtTokenValidator(JwtOptions.Issuer, JwtOptions.Audience, JwtOptions.SymmetricKey);

			signerKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
		}

		private string createCertificatePem(string cn, List<Certificate>? certificateList, out KeyPair keyPair) {
			keyPair = KeyPair.GenerateEllipticCurves(Random, 521);
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
			app.AddRecipient("test recipient 1", createCertificatePem("Test 1", Certificates, out RecipientKeyPair));
			app.AddRecipient("test recipient 2", createCertificatePem("Test 2", Certificates, out _));
			app.AddRecipient("test recipient 3", createCertificatePem("Test 3", Certificates, out _));
			app.AddAuthorizedExporter("test exporter 1", createKeyAuthCertificatePem("Exporter 1", out ExporterKeyPair, out ExporterCertificate));
			context.Applications.Add(app);
			var app2 = ApplicationWithUserProperties.Create(AppName + "_2", AppApiToken + "_2");
			app2.AddProperty("Foo", UserPropertyType.String, true);
			app2.AddProperty("Bar", UserPropertyType.String);
			app2.AddRecipient("other test recipient 1", createCertificatePem("Other Test 1", null, out _));
			app2.AddRecipient("other test recipient 2", createCertificatePem("Other Test 2", null, out _));
			context.Applications.Add(app2);

			var user1 = createUserRegistration(app, out User1Id, out User1Props, "test user 1");
			var user2 = createUserRegistration(app, out User2Id, out User2Props, "test user 2", "testuser2");
			var user3 = createUserRegistration(app, out User3Id, out User3Props, "test user 3");
			context.UserRegistrations.Add(user1);
			context.UserRegistrations.Add(user2);
			context.UserRegistrations.Add(user3);
			var otherAppUser = createUserRegistration(app2, out OtherAppUserId, out _, "otherAppUser");
			context.UserRegistrations.Add(otherAppUser);
			context.SaveChanges();
		}

		private UserRegistration createUserRegistration(ApplicationWithUserProperties app, out Guid id, out byte[] userProps, string fooProp, string? username = null) {
			var userPw = StringGenerator.GenerateRandomWord(16);
			userProps = Random.GetBytes(512);
			var keyEncryptor = new KeyEncryptor(app.DataRecipients.Select(dr => dr.Certificate.PublicKey), Random, true);
			var dataEncryptor = new DataEncryptor(Random, 1);
			var encryptedUserProps = dataEncryptor.EncryptData(userProps, 0);
			UserRegistration user;
			if (username != null) {
				user = UserRegistration.Create(app, username, SecretHashing.CreateHashedSecret(userPw), encryptedUserProps, dataEncryptor.GenerateEncryptionInfo(keyEncryptor));
			}
			else {
				user = UserRegistration.Create(app, SecretHashing.CreateHashedSecret(userPw), encryptedUserProps, dataEncryptor.GenerateEncryptionInfo(keyEncryptor));
			}
			user.SetAppSpecificProperty("Foo", fooProp);
			id = user.Id;
			return user;
		}

		protected override IHostBuilder CreateHostBuilder() {
			return base.CreateHostBuilder().ConfigureAppConfiguration(config => config.AddInMemoryCollection(Config))
				.ConfigureLogging(logging => logging.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Trace));
		}
	}

	public class UserExporterIntegrationTest : IClassFixture<UserExporterIntegrationTestFixture> {
		private UserExporterIntegrationTestFixture fixture;
		private ITestOutputHelper output;

		public UserExporterIntegrationTest(UserExporterIntegrationTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			this.fixture.Output = output;
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

		[Fact]
		public async Task GetUserIdListReturnsIdsOfAllUsersOfCurrentAppAndOnlyThose() {
			using (var httpClient = fixture.CreateClient()) {
				var authenticator = new ExporterKeyPairAuthenticator(httpClient, fixture.ExporterKeyPair, fixture.Services.GetRequiredService<ILogger<ExporterKeyPairAuthenticator>>(), fixture.Random);
				var authData = await authenticator.AuthenticateAsync(fixture.AppName);
				var (principal, validatedToken) = fixture.TokenValidator.Validate(authData.Token.Value);
				var exporterClient = new UserExporterApiClient(httpClient, authData);
				var userIds = await exporterClient.GetUserIdListAsync();
				Assert.Contains(fixture.User1Id, userIds);
				Assert.Contains(fixture.User2Id, userIds);
				Assert.Contains(fixture.User3Id, userIds);
				Assert.DoesNotContain(fixture.OtherAppUserId, userIds);
			}
		}
		[Fact]
		public async Task GetMetadataForAllUsersReturnsMetadataOfAllUsersOfCurrentAppAndOnlyThose() {
			using (var httpClient = fixture.CreateClient()) {
				var authenticator = new ExporterKeyPairAuthenticator(httpClient, fixture.ExporterKeyPair, fixture.Services.GetRequiredService<ILogger<ExporterKeyPairAuthenticator>>(), fixture.Random);
				var authData = await authenticator.AuthenticateAsync(fixture.AppName);
				var (principal, validatedToken) = fixture.TokenValidator.Validate(authData.Token.Value);
				var exporterClient = new UserExporterApiClient(httpClient, authData);
				var users = await exporterClient.GetMetadataForAllUsersAsync();
				Assert.Contains(users, user => user.UserId == fixture.User1Id && (user.StudySpecificProperties["Foo"]?.Equals("test user 1") ?? false));
				Assert.Contains(users, user => user.UserId == fixture.User2Id && (user.StudySpecificProperties["Foo"]?.Equals("test user 2") ?? false) && user.Username == "testuser2");
				Assert.Contains(users, user => user.UserId == fixture.User3Id && (user.StudySpecificProperties["Foo"]?.Equals("test user 3") ?? false));
				Assert.DoesNotContain(users, user => user.UserId == fixture.OtherAppUserId);
			}
		}
		[Fact]
		public async Task GetMetadataForAllUsersProvidesCorrectEncryptionInfosForTheGivenRecipientKeyId() {
			using (var httpClient = fixture.CreateClient()) {
				var authenticator = new ExporterKeyPairAuthenticator(httpClient, fixture.ExporterKeyPair, fixture.Services.GetRequiredService<ILogger<ExporterKeyPairAuthenticator>>(), fixture.Random);
				var authData = await authenticator.AuthenticateAsync(fixture.AppName);
				var (principal, validatedToken) = fixture.TokenValidator.Validate(authData.Token.Value);
				var exporterClient = new UserExporterApiClient(httpClient, authData);
				KeyId recipientKeyId = fixture.RecipientKeyPair.Public.CalculateId();
				var users = await exporterClient.GetMetadataForAllUsersAsync(recipientKeyId);
				var user1 = users.Single(u => u.UserId == fixture.User1Id);
				var user2 = users.Single(u => u.UserId == fixture.User2Id);
				var user3 = users.Single(u => u.UserId == fixture.User3Id);
				var keyDecryptor = new KeyDecryptor(fixture.RecipientKeyPair);
				Assert.NotNull(user1.EncryptedProperties);
				Assert.NotNull(user2.EncryptedProperties);
				Assert.NotNull(user3.EncryptedProperties);
				Assert.NotNull(user1.PropertyEncryptionInfo);
				Assert.NotNull(user2.PropertyEncryptionInfo);
				Assert.NotNull(user3.PropertyEncryptionInfo);
				var user1Decryptor = DataDecryptor.FromEncryptionInfo(user1.PropertyEncryptionInfo, keyDecryptor);
				var user2Decryptor = DataDecryptor.FromEncryptionInfo(user2.PropertyEncryptionInfo, keyDecryptor);
				var user3Decryptor = DataDecryptor.FromEncryptionInfo(user3.PropertyEncryptionInfo, keyDecryptor);
				Assert.NotNull(user1Decryptor);
				Assert.NotNull(user2Decryptor);
				Assert.NotNull(user3Decryptor);
				var user1Props = user1Decryptor.DecryptData(user1.EncryptedProperties, 0);
				var user2Props = user2Decryptor.DecryptData(user2.EncryptedProperties, 0);
				var user3Props = user3Decryptor.DecryptData(user3.EncryptedProperties, 0);
				Assert.Equal(fixture.User1Props, user1Props);
				Assert.Equal(fixture.User2Props, user2Props);
				Assert.Equal(fixture.User3Props, user3Props);
			}
		}
		[Fact]
		public async Task GetMetadataForAllUsersProvidesEncryptionInfosOnlyForTheGivenRecipientKeyId() {
			using (var httpClient = fixture.CreateClient()) {
				var authenticator = new ExporterKeyPairAuthenticator(httpClient, fixture.ExporterKeyPair, fixture.Services.GetRequiredService<ILogger<ExporterKeyPairAuthenticator>>(), fixture.Random);
				var authData = await authenticator.AuthenticateAsync(fixture.AppName);
				var (principal, validatedToken) = fixture.TokenValidator.Validate(authData.Token.Value);
				var exporterClient = new UserExporterApiClient(httpClient, authData);
				KeyId recipientKeyId = fixture.RecipientKeyPair.Public.CalculateId();
				var users = await exporterClient.GetMetadataForAllUsersAsync(recipientKeyId);
				Assert.All(users, user => {
					Assert.NotNull(user.PropertyEncryptionInfo);
					var dk = Assert.Single(user.PropertyEncryptionInfo.DataKeys);
					Assert.Equal(recipientKeyId, dk.Key);
				});
			}
		}
		[Fact]
		public async Task GetUserMetadataByIdReturnsMetadataOfRequestedUser() {
			using (var httpClient = fixture.CreateClient()) {
				var authenticator = new ExporterKeyPairAuthenticator(httpClient, fixture.ExporterKeyPair, fixture.Services.GetRequiredService<ILogger<ExporterKeyPairAuthenticator>>(), fixture.Random);
				var authData = await authenticator.AuthenticateAsync(fixture.AppName);
				var (principal, validatedToken) = fixture.TokenValidator.Validate(authData.Token.Value);
				var exporterClient = new UserExporterApiClient(httpClient, authData);
				var user = await exporterClient.GetUserMetadataByIdAsync(fixture.User2Id);
				Assert.Equal(fixture.User2Id, user.UserId);
				Assert.Equal("testuser2", user.Username);
				Assert.Equal("test user 2", user.StudySpecificProperties["Foo"]);
			}
		}
		[Fact]
		public async Task GetUserMetadataByIdDoesNotReturnDataForUsersOfOtherApps() {
			using (var httpClient = fixture.CreateClient()) {
				var authenticator = new ExporterKeyPairAuthenticator(httpClient, fixture.ExporterKeyPair, fixture.Services.GetRequiredService<ILogger<ExporterKeyPairAuthenticator>>(), fixture.Random);
				var authData = await authenticator.AuthenticateAsync(fixture.AppName);
				var (principal, validatedToken) = fixture.TokenValidator.Validate(authData.Token.Value);
				var exporterClient = new UserExporterApiClient(httpClient, authData);
				await Assert.ThrowsAnyAsync<Exception>(() => exporterClient.GetUserMetadataByIdAsync(fixture.OtherAppUserId));
			}
		}
		[Fact]
		public async Task GetUserMetadataByIdProvidesCorrectEncryptionInfoForTheGivenRecipientKeyId() {
			using (var httpClient = fixture.CreateClient()) {
				var authenticator = new ExporterKeyPairAuthenticator(httpClient, fixture.ExporterKeyPair, fixture.Services.GetRequiredService<ILogger<ExporterKeyPairAuthenticator>>(), fixture.Random);
				var authData = await authenticator.AuthenticateAsync(fixture.AppName);
				var (principal, validatedToken) = fixture.TokenValidator.Validate(authData.Token.Value);
				var exporterClient = new UserExporterApiClient(httpClient, authData);
				KeyId recipientKeyId = fixture.RecipientKeyPair.Public.CalculateId();
				var user2 = await exporterClient.GetUserMetadataByIdAsync(fixture.User2Id, recipientKeyId);
				var keyDecryptor = new KeyDecryptor(fixture.RecipientKeyPair);
				Assert.Equal(fixture.User2Id, user2.UserId);
				Assert.NotNull(user2.EncryptedProperties);
				Assert.NotNull(user2.PropertyEncryptionInfo);
				var user2Decryptor = DataDecryptor.FromEncryptionInfo(user2.PropertyEncryptionInfo, keyDecryptor);
				Assert.NotNull(user2Decryptor);
				var user2Props = user2Decryptor.DecryptData(user2.EncryptedProperties, 0);
				Assert.Equal(fixture.User2Props, user2Props);
			}
		}
		[Fact]
		public async Task GetUserMetadataByIdProvidesEncryptionInfosOnlyForTheGivenRecipientKeyId() {
			using (var httpClient = fixture.CreateClient()) {
				var authenticator = new ExporterKeyPairAuthenticator(httpClient, fixture.ExporterKeyPair, fixture.Services.GetRequiredService<ILogger<ExporterKeyPairAuthenticator>>(), fixture.Random);
				var authData = await authenticator.AuthenticateAsync(fixture.AppName);
				var (principal, validatedToken) = fixture.TokenValidator.Validate(authData.Token.Value);
				var exporterClient = new UserExporterApiClient(httpClient, authData);
				KeyId recipientKeyId = fixture.RecipientKeyPair.Public.CalculateId();
				var user2 = await exporterClient.GetUserMetadataByIdAsync(fixture.User2Id, recipientKeyId);
				Assert.Equal(fixture.User2Id, user2.UserId);
				Assert.NotNull(user2.PropertyEncryptionInfo);
				var dk = Assert.Single(user2.PropertyEncryptionInfo.DataKeys);
				Assert.Equal(recipientKeyId, dk.Key);
			}
		}
	}
}
