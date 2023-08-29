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
			SymmetricKey = "TestingS3cr3tTestingS3cr3tTestingS3cr3t"
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
		public byte[] Log1Content = Array.Empty<byte>();
		public Guid Log2Id;
		public byte[] Log2Content = Array.Empty<byte>();
		public Guid Log3Id;
		public byte[] Log3Content = Array.Empty<byte>();

		public Guid User2Id;
		public Guid Log4Id;
		public byte[] Log4Content = Array.Empty<byte>();
		public Guid Log5Id;
		public byte[] Log5Content = Array.Empty<byte>();

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

		public LogExporterIntegrationTest(LogExporterIntegrationTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			fixture.Output = output;
		}

		[Fact]
		public async Task GetLogIdListReturnsIdsOfAllLogsOfCurrentAppAndOnlyThose() {
			var authData = fixture.GetAuthData(fixture.ExporterCert);
			using (var httpClient = fixture.CreateClient()) {
				var exporterClient = new LogExporterApiClient(httpClient, authData);
				var logIds = await exporterClient.GetLogIdListAsync();
				Assert.Contains(fixture.Log1Id, logIds);
				Assert.Contains(fixture.Log2Id, logIds);
				Assert.Contains(fixture.Log3Id, logIds);
				Assert.Contains(fixture.Log4Id, logIds);
				Assert.Contains(fixture.Log5Id, logIds);
				Assert.DoesNotContain(fixture.OtherAppLogId, logIds);
			}
		}
		[Fact]
		public async Task GetMetadataForAllLogsReturnsMetadataOfAllLogsOfCurrentAppAndOnlyThose() {
			var authData = fixture.GetAuthData(fixture.ExporterCert);
			using (var httpClient = fixture.CreateClient()) {
				var exporterClient = new LogExporterApiClient(httpClient, authData);
				var logs = await exporterClient.GetMetadataForAllLogsAsync();
				Assert.Contains(logs, log => log.LogFileId == fixture.Log1Id && log.UserId == fixture.User1Id);
				Assert.Contains(logs, log => log.LogFileId == fixture.Log2Id && log.UserId == fixture.User1Id);
				Assert.Contains(logs, log => log.LogFileId == fixture.Log3Id && log.UserId == fixture.User1Id);
				Assert.Contains(logs, log => log.LogFileId == fixture.Log4Id && log.UserId == fixture.User2Id);
				Assert.Contains(logs, log => log.LogFileId == fixture.Log5Id && log.UserId == fixture.User2Id);
				Assert.DoesNotContain(logs, log => log.LogFileId == fixture.OtherAppLogId);
			}
		}

		private async Task<byte[]> downloadAndDecryptLog(LogExporterApiClient exporterClient, KeyDecryptor keyDecryptor, DownstreamLogMetadataDTO metadata) {
			Assert.NotNull(metadata.EncryptionInfo);
			var logDecryptor = DataDecryptor.FromEncryptionInfo(metadata.EncryptionInfo, keyDecryptor);
			Assert.NotNull(logDecryptor);
			using var contentStream = await exporterClient.GetLogContentByIdAsync(metadata.LogFileId);
			using var decryptionStream = logDecryptor.OpenDecryptionReadStream(contentStream, 0);
			using var buffer = new MemoryStream();
			await decryptionStream.CopyToAsync(buffer);
			return buffer.ToArray();
		}
		[Fact]
		public async Task GetMetadataForAllLogsProvidesCorrectEncryptionInfosForTheGivenRecipientKeyId() {
			var authData = fixture.GetAuthData(fixture.ExporterCert);
			using (var httpClient = fixture.CreateClient()) {
				var exporterClient = new LogExporterApiClient(httpClient, authData);
				KeyId recipientKeyId = fixture.RecipientKeyPair2.Public.CalculateId();
				var logs = await exporterClient.GetMetadataForAllLogsAsync(recipientKeyId);
				var log1 = logs.Single(u => u.LogFileId == fixture.Log1Id);
				var log2 = logs.Single(u => u.LogFileId == fixture.Log2Id);
				var log3 = logs.Single(u => u.LogFileId == fixture.Log3Id);
				var log4 = logs.Single(u => u.LogFileId == fixture.Log4Id);
				var log5 = logs.Single(u => u.LogFileId == fixture.Log5Id);
				var keyDecryptor = new KeyDecryptor(fixture.RecipientKeyPair2);
				var log1Content = await downloadAndDecryptLog(exporterClient, keyDecryptor, log1);
				var log2Content = await downloadAndDecryptLog(exporterClient, keyDecryptor, log2);
				var log3Content = await downloadAndDecryptLog(exporterClient, keyDecryptor, log3);
				var log4Content = await downloadAndDecryptLog(exporterClient, keyDecryptor, log4);
				var log5Content = await downloadAndDecryptLog(exporterClient, keyDecryptor, log5);
				Assert.Equal(fixture.Log1Content, log1Content);
				Assert.Equal(fixture.Log2Content, log2Content);
				Assert.Equal(fixture.Log3Content, log3Content);
				Assert.Equal(fixture.Log4Content, log4Content);
				Assert.Equal(fixture.Log5Content, log5Content);
			}
		}
		[Fact]
		public async Task GetMetadataForAllLogsProvidesEncryptionInfosOnlyForTheGivenRecipientKeyId() {
			var authData = fixture.GetAuthData(fixture.ExporterCert);
			using (var httpClient = fixture.CreateClient()) {
				var exporterClient = new LogExporterApiClient(httpClient, authData);
				KeyId recipientKeyId = fixture.RecipientKeyPair2.Public.CalculateId();
				var logs = await exporterClient.GetMetadataForAllLogsAsync(recipientKeyId);
				Assert.All(logs, log => {
					Assert.NotNull(log.EncryptionInfo);
					var dk = Assert.Single(log.EncryptionInfo.DataKeys);
					Assert.Equal(recipientKeyId, dk.Key);
				});
			}
		}
		[Fact]
		public async Task GetLogMetadataByIdReturnsMetadataOfRequestedUser() {
			var authData = fixture.GetAuthData(fixture.ExporterCert);
			using (var httpClient = fixture.CreateClient()) {
				var exporterClient = new LogExporterApiClient(httpClient, authData);
				var log = await exporterClient.GetLogMetadataByIdAsync(fixture.Log2Id);
				Assert.Equal(fixture.Log2Id, log.LogFileId);
				Assert.Equal(fixture.User1Id, log.UserId);
				Assert.Equal(LogContentEncoding.Plain, log.LogContentEncoding);
			}
		}
		[Fact]
		public async Task GetLogMetadataByIdDoesNotReturnDataForUsersOfOtherApps() {
			var authData = fixture.GetAuthData(fixture.ExporterCert);
			using (var httpClient = fixture.CreateClient()) {
				var exporterClient = new LogExporterApiClient(httpClient, authData);
				await Assert.ThrowsAnyAsync<Exception>(() => exporterClient.GetLogMetadataByIdAsync(fixture.OtherAppLogId));
			}
		}
		[Fact]
		public async Task GetLogMetadataByIdProvidesCorrectEncryptionInfoForTheGivenRecipientKeyId() {
			var authData = fixture.GetAuthData(fixture.ExporterCert);
			using (var httpClient = fixture.CreateClient()) {
				var exporterClient = new LogExporterApiClient(httpClient, authData);
				KeyId recipientKeyId = fixture.RecipientKeyPair2.Public.CalculateId();
				var log2 = await exporterClient.GetLogMetadataByIdAsync(fixture.Log2Id, recipientKeyId);
				Assert.Equal(fixture.Log2Id, log2.LogFileId);
				Assert.Equal(fixture.User1Id, log2.UserId);
				var keyDecryptor = new KeyDecryptor(fixture.RecipientKeyPair2);
				var log2Content = await downloadAndDecryptLog(exporterClient, keyDecryptor, log2);
				Assert.Equal(fixture.Log2Content, log2Content);
			}
		}
		[Fact]
		public async Task GetLogMetadataByIdProvidesEncryptionInfosOnlyForTheGivenRecipientKeyId() {
			var authData = fixture.GetAuthData(fixture.ExporterCert);
			using (var httpClient = fixture.CreateClient()) {
				var exporterClient = new LogExporterApiClient(httpClient, authData);
				KeyId recipientKeyId = fixture.RecipientKeyPair2.Public.CalculateId();
				var log1 = await exporterClient.GetLogMetadataByIdAsync(fixture.Log1Id, recipientKeyId);
				Assert.Equal(fixture.Log1Id, log1.LogFileId);
				Assert.Equal(fixture.User1Id, log1.UserId);
				Assert.NotNull(log1.EncryptionInfo);
				var dk = Assert.Single(log1.EncryptionInfo.DataKeys);
				Assert.Equal(recipientKeyId, dk.Key);
			}
		}
	}
}
