using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Ocsp;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WireMock;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Client.Tests {
	[Collection("Mock Web Server")]
	public class ClientIntegrationTest : IDisposable {
		private const string appName = "SGLAnalyticsClientIntegrationTest";
		private const string appAPIToken = "FakeApiToken";
		private string dataDirectory;
		private DateTime startTime;
		private ITestOutputHelper output;
		private ILoggerFactory loggerFactory;
		private MockServerFixture serverFixture;
		private DirectoryLogStorage storage;
		private FileRootDataStore rootDS;
		private SglAnalytics analytics;
		private bool finished = false;

		private KeyPair signerKeyPair;
		private KeyPair recipient1KeyPair;
		private KeyPair recipient2KeyPair;
		private Certificate signerCert;
		private Certificate recipient1Cert;
		private Certificate recipient2Cert;
		private ICertificateValidator recipientCertificateValidator;
		private HttpClient httpClient;
		private string recipientCertsPem;

		public ClientIntegrationTest(ITestOutputHelper output, MockServerFixture serverFixture) {
			startTime = DateTime.UtcNow.AddSeconds(-1);
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			this.serverFixture = serverFixture;

			if (string.IsNullOrWhiteSpace(appName)) throw new ArgumentException("Appname must not be empty", nameof(appName));
			dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
			File.Delete(new FileRootDataStore(dataDirectory).StorageFilePath);
			rootDS = new FileRootDataStore(dataDirectory);
			storage = new DirectoryLogStorage(Path.Combine(dataDirectory, "DataLogs"));
			storage.Archiving = false;
			foreach (var log in storage.EnumerateLogs()) {
				log.Remove();
			}

			var random = new RandomGenerator();
			var signerDN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Analytics"), new("ou", "Tests"), new("cn", "Test Signer") });
			var recipient1DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Analytics"), new("ou", "Tests"), new("cn", "Test 1") });
			var recipient2DN = new DistinguishedName(new KeyValuePair<string, string>[] { new("o", "SGL"), new("ou", "Analytics"), new("ou", "Tests"), new("cn", "Test 2") });
			signerKeyPair = KeyPair.GenerateEllipticCurves(random, 521);
			recipient1KeyPair = KeyPair.GenerateEllipticCurves(random, 521);
			recipient2KeyPair = KeyPair.GenerateEllipticCurves(random, 521);
			signerCert = Certificate.Generate(signerDN, signerKeyPair.Private, signerDN, signerKeyPair.Public, TimeSpan.FromHours(2), random, 128, keyUsages: KeyUsages.KeyCertSign, caConstraint: (true, 0));
			recipient1Cert = Certificate.Generate(signerDN, signerKeyPair.Private, recipient1DN, recipient1KeyPair.Public, TimeSpan.FromHours(1), random, 128, keyUsages: KeyUsages.KeyEncipherment | KeyUsages.KeyAgreement, caConstraint: (false, null));
			recipient2Cert = Certificate.Generate(signerDN, signerKeyPair.Private, recipient2DN, recipient2KeyPair.Public, TimeSpan.FromHours(1), random, 128, keyUsages: KeyUsages.KeyEncipherment | KeyUsages.KeyAgreement, caConstraint: (false, null));
			using var signerCertPemBuffer = new StringWriter();
			signerCert.StoreToPem(signerCertPemBuffer);
			using var recipientCertsPemBuffer = new StringWriter();
			recipient1Cert.StoreToPem(recipientCertsPemBuffer);
			recipient2Cert.StoreToPem(recipientCertsPemBuffer);
			recipientCertsPem = recipientCertsPemBuffer.ToString();
			recipientCertificateValidator = new CACertTrustValidator(signerCertPemBuffer.ToString(), ignoreValidityPeriod: false,
				loggerFactory.CreateLogger<CACertTrustValidator>(), loggerFactory.CreateLogger<CertificateStore>());

			httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri(serverFixture.Server.Urls.First());
			analytics = new SglAnalytics(appName, appAPIToken, httpClient, config => {
				config.UseRecipientCertificateValidator(_ => recipientCertificateValidator, dispose: false);
				config.UseRootDataStore(_ => rootDS, dispose: false);
				config.UseLogStorage(_ => storage, dispose: false);
				config.UseLoggerFactory(_ => loggerFactory, dispose: false);
				config.ConfigureCryptography(cryptoConf => {
					cryptoConf.AllowSharedMessageKeyPair();
				});
			});
		}

		public class SimpleTestEvent {
			public string Name { get; set; } = "";
		}

		public class TestUserData : BaseUserData {
			public TestUserData(string? username) : base(username) { }
			[UnencryptedUserProperty]
			public string Label { get; set; } = "";
			[UnencryptedUserProperty]
			public DateTime RegistrationTime { get; set; } = DateTime.Now;
			[UnencryptedUserProperty]
			public int SomeNumber { get; set; } = 0;
			public string EncryptedLabel { get; set; } = "";
			public DateTime EncryptedTime { get; set; } = DateTime.Now;
			public int EncryptedNumber { get; set; } = 0;
			public object EncryptedObject { get; set; } = new { };
		}

		[Theory]
		[InlineData("Testuser")]
		[InlineData(null)]
		public async Task LogEventsAreRecordedAndUploadedAsLogFilesWithCorrectContentAfterRegistration(string? username) {
			var guidMatcher = new RegexMatcher(@"[a-fA-F0-9]{8}[-]([a-fA-F0-9]{4}[-]){3}[a-fA-F0-9]{12}");
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/log/v2").UsingPost()
						.WithHeader("App-API-Token", new ExactMatcher(appAPIToken))
						.WithHeader("Content-Type", new ContentTypeMatcher("multipart/form-data"))
						.WithHeader("Authorization", new ExactMatcher("Bearer OK")))
					.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.NoContent));
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/log/v2/recipient-certificates").UsingGet()
						.WithParam("appName", appName)
						.WithHeader("App-API-Token", new ExactMatcher(appAPIToken)))
					.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK)
						.WithBody(recipientCertsPem));
			Guid userId = Guid.NewGuid();
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/user/v1").UsingPost()
					.WithHeader("App-API-Token", new ExactMatcher(appAPIToken))
					.WithHeader("Content-Type", new ExactMatcher("application/json"))
					.WithBody(b => b?.DetectedBodyType == WireMock.Types.BodyType.Json))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Created)
					.WithBodyAsJson(new UserRegistrationResultDTO(userId), true));
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/user/v1/login").UsingPost()
					.WithHeader("Content-Type", new ExactMatcher("application/json"))
					.WithBody(b => b?.DetectedBodyType == WireMock.Types.BodyType.Json))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK)
					.WithBodyAsJson(new LoginResponseDTO(new AuthorizationToken("OK"))));
			serverFixture.Server.Given(Request.Create().WithPath("/api/analytics/user/v1/recipient-certificates").UsingGet()
					.WithParam("appName", appName)
					.WithHeader("App-API-Token", new ExactMatcher(appAPIToken)))
				.RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK)
					.WithBody(recipientCertsPem));

			analytics.StartNewLog();
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test A" });
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test B" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test C" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap A");
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test D" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap B");
			analytics.RecordSnapshotUnshared("Channel 3", 2, "Snap C");

			analytics.StartNewLog();
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test E" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test F" });
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test G" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap D");
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test H" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test I" });
			analytics.RecordSnapshotUnshared("Channel 3", 2, "Snap E");

			var user = new TestUserData(username) {
				Label = "This is a test!",
				SomeNumber = 42,
				EncryptedLabel = "This is secret!",
				EncryptedNumber = 23,
				EncryptedObject = new {
					Test1 = 123,
					Test2 = "Hello World"
				}
			};
			await analytics.RegisterAsync(user);

			analytics.StartNewLog();
			analytics.RecordEventUnshared("Channel 1", new SimpleTestEvent { Name = "Test J" });
			analytics.RecordEventUnshared("Channel 2", new SimpleTestEvent { Name = "Test K" });
			analytics.RecordSnapshotUnshared("Channel 3", 1, "Snap F");

			await analytics.FinishAsync();
			finished = true;

			var endTime = DateTime.UtcNow.AddSeconds(1);

			static void readAndAssertSimpleTestEvent(ref JsonElement.ArrayEnumerator arrEnumerator, string expChannel, string expName) {
				Assert.True(arrEnumerator.MoveNext());
				var entryElem = arrEnumerator.Current;
				Assert.True(entryElem.TryGetProperty("Channel", out var actChannel));
				Assert.Equal(expChannel, actChannel.GetString());
				Assert.True(entryElem.TryGetProperty("EntryType", out var actEntryType));
				Assert.Equal("Event", actEntryType.GetString());
				Assert.True(entryElem.TryGetProperty("EventType", out var actEventType));
				Assert.Equal("SimpleTestEvent", actEventType.GetString());
				Assert.True(entryElem.TryGetProperty("Payload", out var payload));
				Assert.True(payload.TryGetProperty("Name", out var actName));
				Assert.Equal(expName, actName.GetString());
			}

			static void readAndAssertSimpleSnapshot(ref JsonElement.ArrayEnumerator arrEnumerator, string expChannel, int expObjectId, string expPayload) {
				Assert.True(arrEnumerator.MoveNext());
				var entryElem = arrEnumerator.Current;
				Assert.True(entryElem.TryGetProperty("Channel", out var actChannel));
				Assert.Equal(expChannel, actChannel.GetString());
				Assert.True(entryElem.TryGetProperty("EntryType", out var actEntryType));
				Assert.Equal("Snapshot", actEntryType.GetString());
				Assert.True(entryElem.TryGetProperty("ObjectID", out var actObjectIdElem));
				Assert.True(actObjectIdElem.TryGetInt32(out var actObjectId));
				Assert.Equal(expObjectId, actObjectId);
				Assert.True(entryElem.TryGetProperty("Payload", out var payload));
				Assert.Equal(expPayload, payload.GetString());
			}

			var keyDecryptorRecipient1 = new KeyDecryptor(recipient1KeyPair);
			var keyDecryptorRecipient2 = new KeyDecryptor(recipient2KeyPair);

			var successfulRegRequests = serverFixture.Server.LogEntries
				.Where(le => (int)(le.ResponseMessage.StatusCode ?? 500) < 300 && le.RequestMessage.Path == "/api/analytics/user/v1")
				.Select(le => le.RequestMessage);
			Assert.Single(successfulRegRequests);
			await using (var stream = new MemoryStream(successfulRegRequests.Single().BodyAsBytes)) {
				output.WriteLine("");
				output.WriteLine("Registration:");
				output.WriteStreamContents(stream);
				stream.Position = 0;
				var userReg = await JsonSerializer.DeserializeAsync<UserRegistrationDTO>(stream, JsonOptions.RestOptions);
				Assert.NotNull(userReg);
				Assert.Equal(user.Username, userReg.Username);
				var studyAttr = userReg.StudySpecificProperties as IDictionary<string, object?> ?? new Dictionary<string, object?> { };
				Assert.Equal(user.Label, Assert.Contains("Label", studyAttr));
				Assert.Equal(user.RegistrationTime, Assert.Contains("RegistrationTime", studyAttr));
				Assert.Equal(user.SomeNumber, Assert.Contains("SomeNumber", studyAttr));

				Assert.NotNull(userReg.EncryptedProperties);
				Assert.NotNull(userReg.PropertyEncryptionInfo);
				var propDataDecryptor = DataDecryptor.FromEncryptionInfo(userReg.PropertyEncryptionInfo, keyDecryptorRecipient1);
				Assert.NotNull(propDataDecryptor);
				using var propDecrStream = new GZipStream(
					propDataDecryptor.OpenDecryptionReadStream(
						new MemoryStream(userReg.EncryptedProperties, writable: false), 0),
					CompressionMode.Decompress);
				var encryptedProps = await JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(propDecrStream, JsonOptions.UserPropertiesOptions);
				using var encPropOutBuff = new MemoryStream();
				await JsonSerializer.SerializeAsync(encPropOutBuff, encryptedProps, JsonOptions.UserPropertiesOptions);
				encPropOutBuff.Position = 0;
				output.WriteLine("");
				output.WriteLine("Decrypted user properties:");
				output.WriteStreamContents(encPropOutBuff);
			}

			output.WriteLine("");
			output.WriteLine("Logs:");
			output.WriteLine("=============================================");

			var successfulLogRequests = serverFixture.Server.LogEntries
				.Where(le => (int)(le.ResponseMessage.StatusCode ?? 500) < 300 && le.RequestMessage.Path == "/api/analytics/log/v2")
				.Select(le => le.RequestMessage);
			Assert.Equal(3, successfulLogRequests.Count());
			var requestsEnumerator = successfulLogRequests.GetEnumerator();

			Assert.True(requestsEnumerator.MoveNext());
			var (metadataStream, contentStream) = CheckRequest(requestsEnumerator.Current);
			output.WriteLine("=== Metadata ===");
			output.WriteStreamContents(metadataStream);
			metadataStream.Position = 0;
			var metadata = await JsonSerializer.DeserializeAsync<LogMetadataDTO>(metadataStream, JsonOptions.RestOptions);
			Assert.NotNull(metadata);
			Assert.InRange(metadata.CreationTime.ToUniversalTime(), startTime, endTime);
			Assert.InRange(metadata.EndTime.ToUniversalTime(), startTime, endTime);
			var dataDecryptor = DataDecryptor.FromEncryptionInfo(metadata.EncryptionInfo, keyDecryptorRecipient1);
			Assert.NotNull(dataDecryptor);
			using (var stream = new GZipStream(dataDecryptor.OpenDecryptionReadStream(contentStream, 0, leaveOpen: true), CompressionMode.Decompress)) {
				output.WriteLine("=== Content ===");
				output.WriteStreamContents(stream);
				output.WriteLine("");
			}
			metadataStream.Position = 0;
			contentStream.Position = 0;
			await using (var stream = new GZipStream(dataDecryptor.OpenDecryptionReadStream(contentStream, 0), CompressionMode.Decompress)) {
				using (var jsonDoc = await JsonDocument.ParseAsync(stream)) {
					var arrEnumerator = jsonDoc.RootElement.EnumerateArray().GetEnumerator();
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test A");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test B");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test C");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 1, "Snap A");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test D");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 1, "Snap B");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 2, "Snap C");
				}
			}

			Assert.True(requestsEnumerator.MoveNext());
			(metadataStream, contentStream) = CheckRequest(requestsEnumerator.Current);
			output.WriteLine("=== Metadata ===");
			output.WriteStreamContents(metadataStream);
			metadataStream.Position = 0;
			metadata = await JsonSerializer.DeserializeAsync<LogMetadataDTO>(metadataStream, JsonOptions.RestOptions);
			Assert.NotNull(metadata);
			Assert.InRange(metadata.CreationTime.ToUniversalTime(), startTime, endTime);
			Assert.InRange(metadata.EndTime.ToUniversalTime(), startTime, endTime);
			dataDecryptor = DataDecryptor.FromEncryptionInfo(metadata.EncryptionInfo, keyDecryptorRecipient2);
			Assert.NotNull(dataDecryptor);
			using (var stream = new GZipStream(dataDecryptor.OpenDecryptionReadStream(contentStream, 0, leaveOpen: true), CompressionMode.Decompress)) {
				output.WriteLine("=== Content ===");
				output.WriteStreamContents(stream);
				output.WriteLine("");
			}
			contentStream.Position = 0;
			await using (var stream = new GZipStream(dataDecryptor.OpenDecryptionReadStream(contentStream, 0), CompressionMode.Decompress)) {
				using (var jsonDoc = await JsonDocument.ParseAsync(stream)) {
					var arrEnumerator = jsonDoc.RootElement.EnumerateArray().GetEnumerator();
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test E");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test F");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test G");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 1, "Snap D");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test H");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test I");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 2, "Snap E");
				}
			}

			Assert.True(requestsEnumerator.MoveNext());
			(metadataStream, contentStream) = CheckRequest(requestsEnumerator.Current);
			output.WriteLine("=== Metadata ===");
			output.WriteStreamContents(metadataStream);
			metadataStream.Position = 0;
			metadata = await JsonSerializer.DeserializeAsync<LogMetadataDTO>(metadataStream, JsonOptions.RestOptions);
			Assert.NotNull(metadata);
			Assert.InRange(metadata.CreationTime.ToUniversalTime(), startTime, endTime);
			Assert.InRange(metadata.EndTime.ToUniversalTime(), startTime, endTime);
			dataDecryptor = DataDecryptor.FromEncryptionInfo(metadata.EncryptionInfo, keyDecryptorRecipient1);
			Assert.NotNull(dataDecryptor);
			using (var stream = new GZipStream(dataDecryptor.OpenDecryptionReadStream(contentStream, 0, leaveOpen: true), CompressionMode.Decompress)) {
				output.WriteLine("=== Content ===");
				output.WriteStreamContents(stream);
				output.WriteLine("");
			}
			contentStream.Position = 0;
			await using (var stream = new GZipStream(dataDecryptor.OpenDecryptionReadStream(contentStream, 0), CompressionMode.Decompress)) {
				using (var jsonDoc = await JsonDocument.ParseAsync(stream)) {
					var arrEnumerator = jsonDoc.RootElement.EnumerateArray().GetEnumerator();
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 1", "Test J");
					readAndAssertSimpleTestEvent(ref arrEnumerator, "Channel 2", "Test K");
					readAndAssertSimpleSnapshot(ref arrEnumerator, "Channel 3", 1, "Snap F");
				}
			}

			Assert.False(requestsEnumerator.MoveNext());

		}

		private static (MemoryStream, MemoryStream) CheckRequest(IRequestMessage request) {
			var contentParts = MultipartBodySplitter.SplitMultipartBody(request.BodyAsBytes, MultipartBodySplitter.GetBoundaryFromContentType(request.Headers?["Content-Type"].First())).ToList();
			Assert.Equal("application/json", contentParts[0].SectionHeaders["Content-Type"]);
			Assert.Equal("form-data; name=metadata", contentParts[0].SectionHeaders["Content-Disposition"]);
			var metadataStream = new MemoryStream(contentParts[0].Content, writable: false);
			Assert.Equal("application/octet-stream", contentParts[1].SectionHeaders["Content-Type"]);
			Assert.Equal("form-data; name=content", contentParts[1].SectionHeaders["Content-Disposition"]);
			var contentStream = new MemoryStream(contentParts[1].Content, writable: false);
			return (metadataStream, contentStream);
		}

		public void Dispose() {
			if (!finished) analytics.FinishAsync().Wait();
			analytics.DisposeAsync().AsTask().Wait();
			storage.Archiving = false;
			foreach (var log in storage.EnumerateLogs()) {
				log.Remove();
			}
			File.Delete(rootDS.StorageFilePath);
			serverFixture.Reset();
			httpClient.Dispose();
		}
	}
}
