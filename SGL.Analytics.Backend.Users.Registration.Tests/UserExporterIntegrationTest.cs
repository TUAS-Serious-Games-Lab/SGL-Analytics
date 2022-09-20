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

	public class UserExporterIntegrationTest : IClassFixture<UserExporterIntegrationTestFixture> {
		private UserExporterIntegrationTestFixture fixture;
		private ITestOutputHelper output;
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

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
	}
}
