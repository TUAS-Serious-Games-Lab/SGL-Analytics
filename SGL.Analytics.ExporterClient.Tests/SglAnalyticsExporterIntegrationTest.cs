using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto.Prng;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.Crypto.Signatures;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit.Abstractions;

namespace SGL.Analytics.ExporterClient.Tests {
	public class SglAnalyticsExporterIntegrationTestFixture {
		private string keyFileContent;

		public SglAnalyticsExporterIntegrationTestFixture() {
			LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(c => c.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Trace));
			Random = new RandomGenerator();
			var signerKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			var signerDN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Analytics"), new("ou", "Tests"), new("cn", "Test Signer") });
			var recipientDN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Analytics"), new("ou", "Tests"), new("cn", "Test") });
			AuthKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			RecipientKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			AuthCert = Certificate.Generate(signerDN, signerKeyPair.Private, recipientDN, AuthKeyPair.Public, TimeSpan.FromHours(1), Random, 128, keyUsages: KeyUsages.DigitalSignature, caConstraint: (false, null));
			RecipientCert = Certificate.Generate(signerDN, signerKeyPair.Private, recipientDN, RecipientKeyPair.Public, TimeSpan.FromHours(1), Random, 128, keyUsages: KeyUsages.KeyEncipherment | KeyUsages.KeyAgreement, caConstraint: (false, null));
			using (var keyFileBuffer = new StringWriter()) {
				AuthCert.StoreToPem(keyFileBuffer);
				RecipientCert.StoreToPem(keyFileBuffer);
				KeyFilePassphrase = "ThisIsATest".ToCharArray();
				AuthKeyPair.StoreToPem(keyFileBuffer, PemEncryptionMode.AES_256_CBC, KeyFilePassphrase, Random);
				RecipientKeyPair.StoreToPem(keyFileBuffer, PemEncryptionMode.AES_256_CBC, KeyFilePassphrase, Random);
				keyFileContent = keyFileBuffer.ToString();
			}
			ChallengeDto = new ExporterKeyAuthChallengeDTO(Guid.NewGuid(), Random.GetBytes(1024), SignatureDigest.Sha256);
			KeyAuthResponseDto = new ExporterKeyAuthResponseDTO(new AuthorizationToken("OK"), DateTime.UtcNow.AddDays(1));
		}

		public (DownstreamLogMetadataDTO Metadata, byte[] ServerContent, byte[] PlainContent) CreateTestGameLog(Guid userId, int size = 128 * 1024,
				DateTime? startTime = null, DateTime? endTime = null, DateTime? uploadTime = null, bool compress = true, bool encrypt = true) {
			var plainContent = Random.GetBytes(size);
			byte[] preEncryptionContent = plainContent;
			if (compress) {
				using var compressionBuffer = new MemoryStream();
				using var compressor = new GZipStream(compressionBuffer, CompressionLevel.Optimal, leaveOpen: true);
				compressor.Write(plainContent, 0, plainContent.Length);
				compressor.Flush();
				compressor.Close();
				preEncryptionContent = compressionBuffer.ToArray();
			}
			byte[] serverContent = preEncryptionContent;
			EncryptionInfo encryptionInfo = EncryptionInfo.CreateUnencrypted();
			if (encrypt) {
				var keyEncryptor = new KeyEncryptor(new[] { RecipientKeyPair.Public }, Random, allowSharedMessageKeyPair: true);
				var dataEncryptor = new DataEncryptor(Random);
				serverContent = dataEncryptor.EncryptData(preEncryptionContent, 0);
				encryptionInfo = dataEncryptor.GenerateEncryptionInfo(keyEncryptor);
			}
			var logId = Guid.NewGuid();
			var metadata = new DownstreamLogMetadataDTO(logId, userId, startTime ?? DateTime.UtcNow.AddMinutes(-2), endTime ?? DateTime.UtcNow.AddMinutes(-1),
				DateTime.UtcNow, serverContent.LongLength, compress ? ".log.gz" : ".log", compress ? LogContentEncoding.GZipCompressed : LogContentEncoding.Plain, encryptionInfo);
			return (metadata, serverContent, plainContent);
		}

		public UserMetadataDTO CreateTestUser(string? username, Action<Dictionary<string, object?>> plainProps, Action<Dictionary<string, object?>> encryptedProps) {
			var userId = Guid.NewGuid();
			var plainPropsDict = new Dictionary<string, object?>();
			var encryptedPropsDict = new Dictionary<string, object?>();
			plainProps(plainPropsDict);
			encryptedProps(encryptedPropsDict);

			byte[]? encryptedPropsBytes = null;
			EncryptionInfo? encryptedPropsEncInfo = null;
			if (encryptedPropsDict.Any()) {
				var keyEncryptor = new KeyEncryptor(new[] { RecipientKeyPair.Public }, Random, allowSharedMessageKeyPair: true);
				var dataEncryptor = new DataEncryptor(Random, 1);
				using var encryptedPropsBuffer = new MemoryStream();
				using (var encryptionStream = dataEncryptor.OpenEncryptionWriteStream(encryptedPropsBuffer, 0, leaveOpen: true)) {
					using var compressionStream = new GZipStream(encryptionStream, CompressionLevel.Optimal, leaveOpen: true);
					JsonSerializer.SerializeAsync(compressionStream, encryptedPropsDict, JsonOptions.UserPropertiesOptions);
				}
				encryptedPropsBytes = encryptedPropsBuffer.ToArray();
				encryptedPropsEncInfo = dataEncryptor.GenerateEncryptionInfo(keyEncryptor);
			}
			var metadata = new UserMetadataDTO(userId, username ?? userId.ToString(), plainPropsDict, encryptedPropsBytes, encryptedPropsEncInfo);
			return metadata;
		}

		public void SetupKeyAuth(WireMockServer server) {
			server.Given(Request.Create().UsingPost().WithPath("/api/analytics/user/v1/exporter-key-auth/open-challenge"))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Created).WithBodyAsJson(ChallengeDto));
			server.Given(Request.Create().UsingPost().WithPath("/api/analytics/user/v1/exporter-key-auth/complete-challenge"))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Created).WithBodyAsJson(KeyAuthResponseDto));
		}

		public void SetupGameLogs(WireMockServer server, Func<IEnumerable<(DownstreamLogMetadataDTO Metadata, byte[] ServerContent)>> logsGetter) {
			var logs = logsGetter().ToList();
			var metadata = logs.Select(log => log.Metadata).ToList();
			var contents = logs.ToDictionary(log => log.Metadata.LogFileId, log => log.ServerContent);
			server.Given(Request.Create().UsingGet().WithPath("/api/analytics/log/v2"))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(metadata.Select(md => md.LogFileId).ToList()));
			server.Given(Request.Create().UsingGet().WithPath("/api/analytics/log/v2/all"))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(metadata));
			foreach (var log in logs) {
				server.Given(Request.Create().UsingGet().WithPath($"/api/analytics/log/v2/{log.Metadata.LogFileId:D}/metadata"))
					.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(log.Metadata));
				server.Given(Request.Create().UsingGet().WithPath($"/api/analytics/log/v2/{log.Metadata.LogFileId:D}/content"))
					.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBody(log.ServerContent));
			}
		}

		public void SetupUsers(WireMockServer server, Func<IEnumerable<UserMetadataDTO>> usersGetter) {
			var users = usersGetter().ToList();
			server.Given(Request.Create().UsingGet().WithPath("/api/analytics/user/v1"))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(users.Select(u => u.UserId).ToList()));
			server.Given(Request.Create().UsingGet().WithPath("/api/analytics/user/v1/all"))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(users));
			foreach (var user in users) {
				server.Given(Request.Create().UsingGet().WithPath($"/api/analytics/user/v1/{user.UserId:D}"))
					.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBodyAsJson(user));
			}
		}

		public string AppName { get; } = "SglAnalyticsExporterIntegrationTest";
		public KeyPair AuthKeyPair { get; }
		public KeyPair RecipientKeyPair { get; }
		public Certificate AuthCert { get; }
		public Certificate RecipientCert { get; }
		public ILoggerFactory LoggerFactory { get; }
		public RandomGenerator Random { get; }
		public ITestOutputHelper Output { get; set; } = null!;
		public char[] KeyFilePassphrase { get; }
		public ExporterKeyAuthChallengeDTO ChallengeDto { get; }
		public ExporterKeyAuthResponseDTO KeyAuthResponseDto { get; }

		public TextReader GetKeyFile() => new StringReader(keyFileContent);
	}

	[Collection("Mock Web Server")]
	public class SglAnalyticsExporterIntegrationTest : IClassFixture<SglAnalyticsExporterIntegrationTestFixture> {
		private readonly ITestOutputHelper output;
		private readonly MockServerFixture serverFixture;
		private readonly SglAnalyticsExporterIntegrationTestFixture fixture;
		private readonly ILogger<SglAnalyticsExporterIntegrationTest> logger;

		public SglAnalyticsExporterIntegrationTest(ITestOutputHelper output, MockServerFixture serverFixture, SglAnalyticsExporterIntegrationTestFixture testFixture) {
			this.output = output;
			this.serverFixture = serverFixture;
			fixture = testFixture;
			fixture.Output = output;
			serverFixture.Reset();
			logger = fixture.LoggerFactory.CreateLogger<SglAnalyticsExporterIntegrationTest>();
		}


		[Fact]
		public async Task AuthenticationUsingValidKeyFileWorksCorrectly() {
			fixture.SetupKeyAuth(serverFixture.Server);
			using var client = serverFixture.Server.CreateClient();
			await using var exporter = new SglAnalyticsExporter(client, config => config.UseLoggerFactory(_ => fixture.LoggerFactory, false));
			await exporter.UseKeyFileAsync(fixture.GetKeyFile(), "test.key", () => fixture.KeyFilePassphrase);
			await exporter.SwitchToApplicationAsync(fixture.AppName);
			var open = Assert.Single(serverFixture.Server.LogEntries, le => le.RequestMessage.Path == "/api/analytics/user/v1/exporter-key-auth/open-challenge");
			var complete = Assert.Single(serverFixture.Server.LogEntries, le => le.RequestMessage.Path == "/api/analytics/user/v1/exporter-key-auth/complete-challenge");
			var body = complete.RequestMessage.BodyAsJson as JObject;
			Assert.NotNull(body);
			var challengeId = Guid.Parse(body.Value<string>("challengeId") ?? throw new ArgumentNullException());
			var signature = Convert.FromBase64String(body.Value<string>("signature") ?? throw new ArgumentNullException());
			var verifier = new SignatureVerifier(fixture.AuthKeyPair.Public, SignatureDigest.Sha256);
			KeyId keyId = fixture.AuthKeyPair.Public.CalculateId();
			var sigContent = ExporterKeyAuthSignatureDTO.ConstructContentToSign(
				new ExporterKeyAuthRequestDTO(fixture.AppName, keyId), fixture.ChallengeDto);
			verifier.ProcessBytes(sigContent);
			verifier.CheckSignature(signature);
		}
	}
}
