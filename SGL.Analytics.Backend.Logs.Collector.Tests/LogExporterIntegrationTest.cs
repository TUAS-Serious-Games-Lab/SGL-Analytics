using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.Cms;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Analytics.DTO;
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Logs.Collector.Tests {
	public class LogExporterIntegrationTestFixture : DbWebAppIntegrationTestFixtureBase<LogsContext, Startup> {
		public readonly string AppName = "LogExporterIntegrationTestFixture";
		public string AppApiToken { get; } = StringGenerator.GenerateRandomWord(32);
		public JwtOptions JwtOptions { get; } = new JwtOptions() {
			Audience = "LogExporterIntegrationTestFixture",
			Issuer = "LogExporterIntegrationTestFixture",
			SymmetricKey = "TestingS3cr3tTestingS3cr3t"
		};
		public Dictionary<string, string> JwtConfig { get; }

		public ITestOutputHelper? Output { get; set; } = null;
		public JwtTokenGenerator TokenGenerator { get; }
		public RandomGenerator Random { get; } = new RandomGenerator();
		public PublicKey SignerPublicKey => signerKeyPair.Public;
		private KeyPair signerKeyPair;
		public List<Certificate> Certificates;
		public KeyPair RecipientKeyPair1 { get; }
		public KeyPair RecipientKeyPair2 { get; }
		public KeyPair RecipientKeyPair3 { get; }

		public DistinguishedName SignerIdentity { get; } = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test Signer") });
		public Certificate RecipientCert1 { get; }
		public Certificate RecipientCert2 { get; }
		public Certificate RecipientCert3 { get; }

		public Certificate ExporterCert { get; }

		public Guid User1Id;
		public Guid Log1Id;
		public byte[] Log1Content;
		public Guid Log2Id;
		public byte[] Log2Content;
		public Guid Log3Id;
		public byte[] Log3Content;

		public Guid User2Id;
		public Guid Log4Id;
		public byte[] Log4Content;
		public Guid Log5Id;
		public byte[] Log5Content;

		public Guid OtherAppLogId;

		public LogExporterIntegrationTestFixture() {
			JwtConfig = new() {
				["Jwt:Audience"] = JwtOptions.Audience,
				["Jwt:Issuer"] = JwtOptions.Issuer,
				["Jwt:SymmetricKey"] = JwtOptions.SymmetricKey,
				["Logging:File:BaseDirectory"] = "logs/SGL.Analytics.LogExporter",
				["Logging:File:Sinks:0:FilenameFormat"] = "{Time:yyyy-MM}/{Time:yyyy-MM-dd}_{ServiceName}.log",
				["Logging:File:Sinks:1:FilenameFormat"] = "{Time:yyyy-MM}/Categories/{Category}.log",
				["Logging:File:Sinks:2:FilenameFormat"] = "{Time:yyyy-MM}/Requests/{RequestId}.log",
				["Logging:File:Sinks:2:MessageFormat"] = "[{RequestPath}] [{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}",
				["Logging:File:Sinks:2:MessageFormatException"] = "[{RequestPath}] [{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}",
				["Logging:File:Sinks:3:FilenameFormat"] = "{Time:yyyy-MM}/users/{UserId}/{Time:yyyy-MM-dd}_{ServiceName}_{UserId}.log",
				["FileSystemLogRepository:StorageDirectory"] = Path.Combine(Environment.CurrentDirectory, "LogExporter_LogStorage")
			};
			TokenGenerator = new JwtTokenGenerator(JwtOptions.Issuer, JwtOptions.Audience, JwtOptions.SymmetricKey);

			signerKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			(RecipientCert1, RecipientKeyPair1) = createRecipient("Test 1");
			(RecipientCert2, RecipientKeyPair2) = createRecipient("Test 2");
			(RecipientCert3, RecipientKeyPair3) = createRecipient("Test 3");
			Certificates = new List<Certificate> { RecipientCert1, RecipientCert2, RecipientCert3 };
			var exporterKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			ExporterCert = Certificate.Generate(SignerIdentity, signerKeyPair.Private,
				new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", "Test Exporter") }),
				exporterKeyPair.Public, TimeSpan.FromHours(1), Random, 128, keyUsages: KeyUsages.DigitalSignature);
		}

		private string createCertificatePem(Certificate certificate) {
			using var writer = new StringWriter();
			certificate.StoreToPem(writer);
			return writer.ToString();
		}

		private (Certificate, KeyPair) createRecipient(string cn) {
			var keyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			var identity = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Utility"), new("ou", "Tests"), new("cn", cn) });
			var cert = Certificate.Generate(SignerIdentity, signerKeyPair.Private, identity, keyPair.Public, TimeSpan.FromHours(1), Random, 128);
			return (cert, keyPair);
		}

		private LogMetadata createLog(IServiceProvider serviceProvider, Domain.Entity.Application app, Guid userId, int size, out Guid id, out byte[] content) {
			var logRepo = serviceProvider.GetRequiredService<ILogFileRepository>();
			id = Guid.NewGuid();
			content = Random.GetBytes(size);
			using var contentStream = new MemoryStream(content, writable: false);
			var keyEncryptor = new KeyEncryptor(Certificates.Select(cert => cert.PublicKey), Random);
			var dataEncryptor = new DataEncryptor(Random, 1);
			using var encryptionStream = dataEncryptor.OpenEncryptionReadStream(contentStream, 0);
			var encryptedSize = logRepo.StoreLogAsync(AppName, userId, id, ".log", encryptionStream).Result;
			var metadata = LogMetadata.Create(id, app, userId, id, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow, ".log",
				DTO.LogContentEncoding.Plain, encryptedSize, dataEncryptor.GenerateEncryptionInfo(keyEncryptor), complete: true);
			return metadata;
		}

		protected override void SeedDatabase(LogsContext context, IServiceProvider serviceProvider) {
			Domain.Entity.Application app = Domain.Entity.Application.Create(AppName, AppApiToken);
			app.AddRecipient("test recipient 1", createCertificatePem(RecipientCert1));
			app.AddRecipient("test recipient 2", createCertificatePem(RecipientCert2));
			app.AddRecipient("test recipient 3", createCertificatePem(RecipientCert3));
			context.Applications.Add(app);
			Domain.Entity.Application otherApp = Domain.Entity.Application.Create("SomeOtherApp", StringGenerator.GenerateRandomWord(32));
			otherApp.AddRecipient("other test recipient 1", createCertificatePem(createRecipient("Other Test 1").Item1));
			otherApp.AddRecipient("other test recipient 2", createCertificatePem(createRecipient("Other Test 2").Item1));
			context.Applications.Add(otherApp);
			//context.SaveChanges();

			User1Id = Guid.NewGuid();
			User2Id = Guid.NewGuid();
			context.LogMetadata.Add(createLog(serviceProvider, app, User1Id, 512, out Log1Id, out Log1Content));
			context.LogMetadata.Add(createLog(serviceProvider, app, User1Id, 1024, out Log2Id, out Log2Content));
			context.LogMetadata.Add(createLog(serviceProvider, app, User1Id, 2048, out Log3Id, out Log3Content));
			context.LogMetadata.Add(createLog(serviceProvider, app, User2Id, 432, out Log4Id, out Log4Content));
			context.LogMetadata.Add(createLog(serviceProvider, app, User2Id, 789, out Log5Id, out Log5Content));
			context.LogMetadata.Add(createLog(serviceProvider, otherApp, Guid.NewGuid(), 123, out OtherAppLogId, out _));
			context.SaveChanges();
		}

		protected override void OverrideConfig(IServiceCollection services) {
			services.Configure<FileSystemLogRepositoryOptions>(options => options.StorageDirectory = Path.Combine(Environment.CurrentDirectory, "LogStorage"));
		}

		protected override IHostBuilder CreateHostBuilder() {
			return base.CreateHostBuilder().ConfigureAppConfiguration(config => config.AddInMemoryCollection(JwtConfig))
				.ConfigureLogging(logging => logging.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Trace));
		}

		public AuthorizationData GetAuthData(Certificate authCert) {
			var validFor = TimeSpan.FromHours(1);
			var expiry = DateTime.UtcNow + validFor;
			return new AuthorizationData(new AuthorizationToken(AuthorizationTokenScheme.Bearer,
				TokenGenerator.GenerateToken(validFor, ("appname", AppName), ("keyid", authCert.PublicKey.CalculateId().ToString()!),
				("exporter-dn", authCert.SubjectDN.ToString()!))), expiry);
		}
	}


	public class LogExporterIntegrationTest : IClassFixture<LogExporterIntegrationTestFixture> {
		private readonly LogExporterIntegrationTestFixture fixture;
		private readonly ITestOutputHelper output;
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		public LogExporterIntegrationTest(LogExporterIntegrationTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			fixture.Output = output;
		}

	}
}
