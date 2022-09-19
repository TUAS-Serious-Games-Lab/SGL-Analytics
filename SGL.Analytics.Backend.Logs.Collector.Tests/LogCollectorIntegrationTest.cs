using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Analytics.DTO;
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
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Logs.Collector.Tests {
	public class LogCollectorIntegrationTestFixture : DbWebAppIntegrationTestFixtureBase<LogsContext, Startup> {
		public readonly string AppName = "LogCollectorIntegrationTest";
		public string AppApiToken { get; } = StringGenerator.GenerateRandomWord(32);
		public JwtOptions JwtOptions { get; } = new JwtOptions() {
			Audience = "LogCollectorIntegrationTest",
			Issuer = "LogCollectorIntegrationTest",
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

		public LogCollectorIntegrationTestFixture() {
			JwtConfig = new() {
				["Jwt:Audience"] = JwtOptions.Audience,
				["Jwt:Issuer"] = JwtOptions.Issuer,
				["Jwt:SymmetricKey"] = JwtOptions.SymmetricKey,
				["Logging:File:BaseDirectory"] = "logs/{ServiceName}",
				["Logging:File:Sinks:0:FilenameFormat"] = "{Time:yyyy-MM}/{Time:yyyy-MM-dd}_{ServiceName}.log",
				["Logging:File:Sinks:1:FilenameFormat"] = "{Time:yyyy-MM}/Categories/{Category}.log",
				["Logging:File:Sinks:2:FilenameFormat"] = "{Time:yyyy-MM}/Requests/{RequestId}.log",
				["Logging:File:Sinks:2:MessageFormat"] = "[{RequestPath}] [{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}",
				["Logging:File:Sinks:2:MessageFormatException"] = "[{RequestPath}] [{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}",
				["Logging:File:Sinks:3:FilenameFormat"] = "{Time:yyyy-MM}/users/{UserId}/{Time:yyyy-MM-dd}_{ServiceName}_{UserId}.log",
			};
			TokenGenerator = new JwtTokenGenerator(JwtOptions.Issuer, JwtOptions.Audience, JwtOptions.SymmetricKey);

			signerKeyPair = KeyPair.GenerateEllipticCurves(Random, 521);
			(RecipientCert1, RecipientKeyPair1) = createRecipient("Test 1");
			(RecipientCert2, RecipientKeyPair2) = createRecipient("Test 2");
			(RecipientCert3, RecipientKeyPair3) = createRecipient("Test 3");
			Certificates = new List<Certificate> { RecipientCert1, RecipientCert2, RecipientCert3 };
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

		protected override void SeedDatabase(LogsContext context) {
			Domain.Entity.Application app = Domain.Entity.Application.Create(AppName, AppApiToken);
			app.AddRecipient("test recipient 1", createCertificatePem(RecipientCert1));
			app.AddRecipient("test recipient 2", createCertificatePem(RecipientCert2));
			app.AddRecipient("test recipient 3", createCertificatePem(RecipientCert3));
			context.Applications.Add(app);
			Domain.Entity.Application otherApp = Domain.Entity.Application.Create("SomeOtherApp", StringGenerator.GenerateRandomWord(32));
			otherApp.AddRecipient("other test recipient 1", createCertificatePem(createRecipient("Other Test 1").Item1));
			otherApp.AddRecipient("other test recipient 2", createCertificatePem(createRecipient("Other Test 2").Item1));
			context.Applications.Add(otherApp);
			context.SaveChanges();
		}

		protected override void OverrideConfig(IServiceCollection services) {
			services.Configure<FileSystemLogRepositoryOptions>(options => options.StorageDirectory = Path.Combine(Environment.CurrentDirectory, "LogStorage"));
		}

		protected override IHostBuilder CreateHostBuilder() {
			return base.CreateHostBuilder().ConfigureAppConfiguration(config => config.AddInMemoryCollection(JwtConfig))
				.ConfigureLogging(logging => logging.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Trace));
		}
	}

	public class LogCollectorIntegrationTest : IClassFixture<LogCollectorIntegrationTestFixture> {
		private readonly LogCollectorIntegrationTestFixture fixture;
		private readonly ITestOutputHelper output;
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		public LogCollectorIntegrationTest(LogCollectorIntegrationTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			fixture.Output = output;
		}

		[Fact]
		public async Task RecipientCertificateListContainsExpectedEntries() {
			using (var client = fixture.CreateClient()) {
				var request = new HttpRequestMessage(HttpMethod.Get, $"/api/analytics/log/v2/recipient-certificates?appName={fixture.AppName}");
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

		private Stream generateRandomGZippedTestData() {
			var stream = new MemoryStream();
			using (var writer = new StreamWriter(new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))) {
				for (int i = 0; i < 20; ++i) {
					writer.WriteLine(StringGenerator.GenerateRandomString(128));
				}
			}
			stream.Position = 0;
			return stream;
		}

		private HttpRequestMessage buildUploadRequest(Stream logContent, LogMetadataDTO logMDTO, Guid userId, string appName, string? appApiToken = null) {
			var content = new StreamContent(logContent);
			content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
			var metadata = JsonContent.Create(logMDTO, MediaTypeHeaderValue.Parse("application/json"), jsonOptions);
			var multipartContent = new MultipartFormDataContent();
			multipartContent.Add(metadata, "metadata");
			multipartContent.Add(content, "content");
			var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/log/v2");
			request.Headers.Add("App-API-Token", appApiToken ?? fixture.AppApiToken);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
				fixture.TokenGenerator.GenerateToken(userId, TimeSpan.FromMinutes(5), ("appname", appName)));
			request.Content = multipartContent;
			return request;
		}

		[Fact]
		public async Task LogIngestWithValidParametersSucceeds() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			var keyEncryptor = new KeyEncryptor(fixture.Certificates.Select(cert => cert.PublicKey), fixture.Random);
			var dataEncryptor = new DataEncryptor(fixture.Random, 1);
			var logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, dataEncryptor.GenerateEncryptionInfo(keyEncryptor));
			using (var logContent = generateRandomGZippedTestData()) {
				using (var client = fixture.CreateClient()) {
					using var encrStream = dataEncryptor.OpenEncryptionReadStream(logContent, 0, leaveOpen: true);
					var request = buildUploadRequest(encrStream, logMDTO, userId, fixture.AppName);
					var response = await client.SendAsync(request);
					response.EnsureSuccessStatusCode();
				}
				using (var scope = fixture.Services.CreateScope()) {
					var logMdRepo = scope.ServiceProvider.GetRequiredService<ILogMetadataRepository>();
					var logMd = await logMdRepo.GetLogMetadataByIdAsync(logId, new LogMetadataQueryOptions { FetchRecipientKeys = true });
					Assert.NotNull(logMd);
					Assert.Equal(userId, logMd?.UserId);
					Assert.Equal(fixture.AppName, logMd?.App.Name);
					Assert.Equal(logMDTO.CreationTime.ToUniversalTime(), logMd?.CreationTime);
					Assert.Equal(logMDTO.EndTime.ToUniversalTime(), logMd?.EndTime);
					Assert.Equal(logMDTO.NameSuffix, logMd?.FilenameSuffix);
					Assert.Equal(logMDTO.LogContentEncoding, logMd?.Encoding);
					Assert.InRange(logMd?.UploadTime ?? DateTime.UnixEpoch, DateTime.Now.AddMinutes(-1).ToUniversalTime(), DateTime.Now.AddSeconds(1).ToUniversalTime());
					Assert.True(logMd?.Complete);
					Assert.Equal(DataEncryptionMode.AES_256_CCM, logMd?.EncryptionMode);

					var keyDecryptor = new KeyDecryptor(fixture.RecipientKeyPair2);
					var dataDecryptor = DataDecryptor.FromEncryptionInfo(logMd!.EncryptionInfo, keyDecryptor);
					Assert.NotNull(dataDecryptor);
					var fileRepo = scope.ServiceProvider.GetRequiredService<ILogFileRepository>();
					using (var readStream = await fileRepo.ReadLogAsync(fixture.AppName, userId, logId, ".log.gz")) {
						logContent.Position = 0;
						using var decrStream = dataDecryptor.OpenDecryptionReadStream(readStream, 0);
						StreamUtils.AssertEqualContent(logContent, decrStream);
						Assert.Equal(readStream.Length, logMd.Size);
					}
				}
			}
		}

		[Fact]
		public async Task LogIngestWithUnknownApplicationReturnsUnauthorizedError() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			using (var logContent = generateRandomGZippedTestData())
			using (var client = fixture.CreateClient()) {
				var request = buildUploadRequest(logContent, new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, EncryptionInfo.CreateUnencrypted()), userId, "DoesNotExist");
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.Unauthorized, Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCode()).StatusCode);
				Assert.Empty(response.Headers.WwwAuthenticate); // Ensure the error is not from JWT challenge but from the missing application.
			}
		}

		[Fact]
		public async Task LogIngestWithIncorrectApiTokenReturnsUnauthorizedError() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			using (var logContent = generateRandomGZippedTestData())
			using (var client = fixture.CreateClient()) {
				var content = new StreamContent(logContent);
				LogMetadataDTO logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, EncryptionInfo.CreateUnencrypted());
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var metadata = JsonContent.Create(logMDTO, MediaTypeHeaderValue.Parse("application/json"), jsonOptions);
				var multipartContent = new MultipartFormDataContent();
				multipartContent.Add(metadata, "metadata");
				multipartContent.Add(content, "content");
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/log/v2");
				request.Headers.Add("App-API-Token", "IncorrectToken");
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
					fixture.TokenGenerator.GenerateToken(userId, TimeSpan.FromMinutes(5), ("appname", fixture.AppName)));
				request.Content = multipartContent;
				var response = await client.SendAsync(request);
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCode()).StatusCode);
				Assert.Empty(response.Headers.WwwAuthenticate); // Ensure the error is not from JWT challenge but from the incorrect app token.
			}
		}

		[Fact]
		public async Task LogIngestWithoutJwtAuthReturnsUnauthorizedWithAuthChallenge() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			using (var logContent = generateRandomGZippedTestData())
			using (var client = fixture.CreateClient()) {
				var content = new StreamContent(logContent);
				LogMetadataDTO logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, EncryptionInfo.CreateUnencrypted());
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var metadata = JsonContent.Create(logMDTO, MediaTypeHeaderValue.Parse("application/json"), jsonOptions);
				var multipartContent = new MultipartFormDataContent();
				multipartContent.Add(metadata, "metadata");
				multipartContent.Add(content, "content");
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/log/v2");
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				request.Content = multipartContent;
				var response = await client.SendAsync(request);
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCode()).StatusCode);
				// Ensure the error is from JWT challenge, not from incorrect app credentials.
				Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
			}
		}

		[Fact]
		public async Task LogIngestWithInvalidJwtKeyReturnsUnauthorizedWithAuthChallenge() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			using (var logContent = generateRandomGZippedTestData())
			using (var client = fixture.CreateClient()) {
				var content = new StreamContent(logContent);
				LogMetadataDTO logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, EncryptionInfo.CreateUnencrypted());
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var metadata = JsonContent.Create(logMDTO, MediaTypeHeaderValue.Parse("application/json"), jsonOptions);
				var multipartContent = new MultipartFormDataContent();
				multipartContent.Add(metadata, "metadata");
				multipartContent.Add(content, "content");
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/log/v2");
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
									new JwtTokenGenerator(fixture.JwtOptions.Issuer, fixture.JwtOptions.Audience, "InvalidKeyInvalidKeyInvalidKeyInvalidKeyInvalidKey")
									.GenerateToken(userId, TimeSpan.FromMinutes(5), ("appname", fixture.AppName)));
				request.Content = multipartContent;
				var response = await client.SendAsync(request);
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCode()).StatusCode);
				// Ensure the error is from JWT challenge, not from incorrect app credentials.
				Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
			}
		}

		[Fact]
		public async Task LogIngestWithInvalidJwtIssuerReturnsUnauthorizedWithAuthChallenge() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			using (var logContent = generateRandomGZippedTestData())
			using (var client = fixture.CreateClient()) {
				var content = new StreamContent(logContent);
				LogMetadataDTO logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, EncryptionInfo.CreateUnencrypted());
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var metadata = JsonContent.Create(logMDTO, MediaTypeHeaderValue.Parse("application/json"), jsonOptions);
				var multipartContent = new MultipartFormDataContent();
				multipartContent.Add(metadata, "metadata");
				multipartContent.Add(content, "content");
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/log/v2");
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
									new JwtTokenGenerator("InvalidIssuer", fixture.JwtOptions.Audience, fixture.JwtOptions.SymmetricKey!)
									.GenerateToken(userId, TimeSpan.FromMinutes(5), ("appname", fixture.AppName)));
				request.Content = multipartContent;
				var response = await client.SendAsync(request);
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCode()).StatusCode);
				// Ensure the error is from JWT challenge, not from incorrect app credentials.
				Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
			}
		}

		[Fact]
		public async Task LogIngestWithInvalidJwtAudienceReturnsUnauthorizedWithAuthChallenge() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			using (var logContent = generateRandomGZippedTestData())
			using (var client = fixture.CreateClient()) {
				var content = new StreamContent(logContent);
				LogMetadataDTO logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, EncryptionInfo.CreateUnencrypted());
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var metadata = JsonContent.Create(logMDTO, MediaTypeHeaderValue.Parse("application/json"), jsonOptions);
				var multipartContent = new MultipartFormDataContent();
				multipartContent.Add(metadata, "metadata");
				multipartContent.Add(content, "content");
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/analytics/log/v2");
				request.Headers.Add("App-API-Token", fixture.AppApiToken);
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
									new JwtTokenGenerator(fixture.JwtOptions.Issuer, "InvalidAudience", fixture.JwtOptions.SymmetricKey!)
									.GenerateToken(userId, TimeSpan.FromMinutes(5), ("appname", fixture.AppName)));
				request.Content = multipartContent;
				var response = await client.SendAsync(request);
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCode()).StatusCode);
				// Ensure the error is from JWT challenge, not from incorrect app credentials.
				Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
			}
		}

		[Fact]
		public async Task FailedLogIngestCanBeSuccessfullyRetried() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			var keyEncryptor = new KeyEncryptor(fixture.Certificates.Select(cert => cert.PublicKey), fixture.Random);
			var logMDTOBase = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, EncryptionInfo.CreateUnencrypted() /*Placeholder*/);
			using (var logContent = generateRandomGZippedTestData()) {
				using (var client = fixture.CreateClient(new() {
					// These options are required for the fault simulation,
					// because the default options attempt to duplicate the request content internally
					// and thus trigger the fault before the backend is invoked.
					AllowAutoRedirect = false,
					HandleCookies = false
				})) {
					// We want to simulate a network fault or the client crashing. For this, we halt the body transfer after the first byte.
					// After the metadata record was written, we inject an error and cancel the request from the client side,
					// so the server-side code fails while transferring the body.
					// For this, we give an ample timeout to ensure the write can occur before the request times out.
					client.Timeout = TimeSpan.FromMilliseconds(100000);
					var cts = new CancellationTokenSource();
					var dataEncryptor = new DataEncryptor(fixture.Random, 1);
					var logMDTO = new LogMetadataDTO(logMDTOBase.LogFileId, logMDTOBase.CreationTime, logMDTOBase.EndTime, logMDTOBase.NameSuffix, logMDTOBase.LogContentEncoding, dataEncryptor.GenerateEncryptionInfo(keyEncryptor));
					var streamWrapper = new TriggeredBlockingStream(dataEncryptor.OpenEncryptionReadStream(logContent, 0, leaveOpen: true));
					var request = buildUploadRequest(streamWrapper, logMDTO, userId, fixture.AppName);
					var task = client.SendAsync(request, cts.Token);

					streamWrapper.TriggerReadReady(1);
					// Because the headers were already sent, the server-side request handling should be invoked despite the body transfer being stalled after the first byte.
					// Poll the database to wait for the LogMetadata entry to appear:
					await PollWaitForLogMetadata(logId);
					// After it has appeared, inject the error into the body transfer and cancel the request:
					streamWrapper.TriggerReadError(new IOException("Generic I/O error"));
					cts.CancelAfter(TimeSpan.FromSeconds(2));
					// Awaiting the task here does not only wait for the client, but also for the server-side request handling (because of WebApplicationFactory not actually going over network).
					await Assert.ThrowsAnyAsync<Exception>(async () => await task);
				}
				using (var scope = fixture.Services.CreateScope()) {
					// Now the metadata entry should be present, but with Complete=false because the upload failed.
					var logMdRepo = scope.ServiceProvider.GetRequiredService<ILogMetadataRepository>();
					var logMd = await logMdRepo.GetLogMetadataByIdAsync(logId);
					Assert.NotNull(logMd);
					Assert.Equal(userId, logMd?.UserId);
					Assert.Equal(fixture.AppName, logMd?.App.Name);
					Assert.Equal(logMDTOBase.CreationTime.ToUniversalTime(), logMd?.CreationTime);
					Assert.Equal(logMDTOBase.EndTime.ToUniversalTime(), logMd?.EndTime);
					Assert.Equal(logMDTOBase.NameSuffix, logMd?.FilenameSuffix);
					Assert.Equal(logMDTOBase.LogContentEncoding, logMd?.Encoding);
					Assert.InRange(logMd?.UploadTime ?? DateTime.UnixEpoch, DateTime.Now.AddMinutes(-1).ToUniversalTime(), DateTime.Now.AddSeconds(1).ToUniversalTime());
					Assert.Null(logMd?.Size);
					Assert.False(logMd?.Complete);
				}
				// Reattempt normally...
				using (var client = fixture.CreateClient()) {
					logContent.Position = 0;
					var dataEncryptor = new DataEncryptor(fixture.Random, 1);
					var logMDTO = new LogMetadataDTO(logMDTOBase.LogFileId, logMDTOBase.CreationTime, logMDTOBase.EndTime, logMDTOBase.NameSuffix, logMDTOBase.LogContentEncoding, dataEncryptor.GenerateEncryptionInfo(keyEncryptor));
					var request = buildUploadRequest(dataEncryptor.OpenEncryptionReadStream(logContent, 0, leaveOpen: true), logMDTO, userId, fixture.AppName);
					var response = await client.SendAsync(request);
					response.EnsureSuccessStatusCode();
				}
				using (var scope = fixture.Services.CreateScope()) {
					// Should be fine now.
					var logMdRepo = scope.ServiceProvider.GetRequiredService<ILogMetadataRepository>();
					var logMd = await logMdRepo.GetLogMetadataByIdAsync(logId, new LogMetadataQueryOptions { FetchRecipientKeys = true });
					Assert.NotNull(logMd);
					Assert.Equal(userId, logMd?.UserId);
					Assert.Equal(fixture.AppName, logMd?.App.Name);
					Assert.Equal(logMDTOBase.CreationTime.ToUniversalTime(), logMd?.CreationTime);
					Assert.Equal(logMDTOBase.EndTime.ToUniversalTime(), logMd?.EndTime);
					Assert.Equal(logMDTOBase.NameSuffix, logMd?.FilenameSuffix);
					Assert.Equal(logMDTOBase.LogContentEncoding, logMd?.Encoding);
					Assert.InRange(logMd?.UploadTime ?? DateTime.UnixEpoch, DateTime.Now.AddMinutes(-1).ToUniversalTime(), DateTime.Now.AddSeconds(1).ToUniversalTime());
					Assert.True(logMd?.Complete);
					Assert.Equal(DataEncryptionMode.AES_256_CCM, logMd?.EncryptionMode);

					var keyDecryptor = new KeyDecryptor(fixture.RecipientKeyPair2);
					var dataDecryptor = DataDecryptor.FromEncryptionInfo(logMd!.EncryptionInfo, keyDecryptor);
					Assert.NotNull(dataDecryptor);
					var fileRepo = scope.ServiceProvider.GetRequiredService<ILogFileRepository>();
					using (var readStream = await fileRepo.ReadLogAsync(fixture.AppName, userId, logId, ".log.gz")) {
						logContent.Position = 0;
						using var decrStream = dataDecryptor.OpenDecryptionReadStream(readStream, 0);
						StreamUtils.AssertEqualContent(logContent, decrStream);
						Assert.Equal(readStream.Length, logMd.Size);
					}
				}
			}

		}

		private async Task PollWaitForLogMetadata(Guid logId) {
			LogMetadata? pollLogMd = null;
			while (pollLogMd == null) {
				await Task.Delay(100);
				using (var scope = fixture.Services.CreateScope()) {
					var logMdRepo = scope.ServiceProvider.GetRequiredService<ILogMetadataRepository>();
					pollLogMd = await logMdRepo.GetLogMetadataByIdAsync(logId);
				}
			}
		}

		[Fact]
		public async Task LogIngestWithCollidingIdSucceedsButGetsNewId() {
			var logId = Guid.NewGuid();
			var keyEncryptor = new KeyEncryptor(fixture.Certificates.Select(cert => cert.PublicKey), fixture.Random);
			// First, create the conflicting log:
			using (var client = fixture.CreateClient()) {
				var dataEncryptorOther = new DataEncryptor(fixture.Random, 1);
				Guid otherUserId = Guid.NewGuid();
				var request = buildUploadRequest(Stream.Null, new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-90), DateTime.Now.AddMinutes(-45), ".log.gz", LogContentEncoding.GZipCompressed,
					dataEncryptorOther.GenerateEncryptionInfo(keyEncryptor)), otherUserId, fixture.AppName);
				var response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();
			}
			// Now try to upload a log with the same id from a different user:
			var userId = Guid.NewGuid();
			var dataEncryptor = new DataEncryptor(fixture.Random, 1);
			var logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, dataEncryptor.GenerateEncryptionInfo(keyEncryptor));
			using (var logContent = generateRandomGZippedTestData()) {
				using (var client = fixture.CreateClient()) {
					var request = buildUploadRequest(dataEncryptor.OpenEncryptionReadStream(logContent, 0, leaveOpen: true), logMDTO, userId, fixture.AppName);
					var response = await client.SendAsync(request);
					response.EnsureSuccessStatusCode();
				}
				using (var scope = fixture.Services.CreateScope()) {
					var db = scope.ServiceProvider.GetRequiredService<LogsContext>();
					var logMd = await db.LogMetadata.Where(lm => lm.LocalLogId == logId && lm.UserId == userId)
						.Include(lm => lm.App).Include(lm => lm.RecipientKeys).SingleOrDefaultAsync<LogMetadata?>();
					Assert.NotNull(logMd);
					Assert.NotEqual(logId, logMd?.Id);
					Assert.Equal(userId, logMd?.UserId);
					Assert.Equal(fixture.AppName, logMd?.App.Name);
					Assert.Equal(logMDTO.CreationTime.ToUniversalTime(), logMd?.CreationTime);
					Assert.Equal(logMDTO.EndTime.ToUniversalTime(), logMd?.EndTime);
					Assert.Equal(logMDTO.NameSuffix, logMd?.FilenameSuffix);
					Assert.Equal(logMDTO.LogContentEncoding, logMd?.Encoding);
					Assert.InRange(logMd?.UploadTime ?? DateTime.UnixEpoch, DateTime.Now.AddMinutes(-1).ToUniversalTime(), DateTime.Now.AddSeconds(1).ToUniversalTime());
					Assert.True(logMd?.Complete);

					var keyDecryptor = new KeyDecryptor(fixture.RecipientKeyPair1);
					var dataDecryptor = DataDecryptor.FromEncryptionInfo(logMd!.EncryptionInfo, keyDecryptor);
					Assert.NotNull(dataDecryptor);
					var fileRepo = scope.ServiceProvider.GetRequiredService<ILogFileRepository>();
					using (var readStream = await fileRepo.ReadLogAsync(fixture.AppName, userId, logMd?.Id ?? Guid.Empty, ".log.gz")) {
						logContent.Position = 0;
						using var decrStream = dataDecryptor.OpenDecryptionReadStream(readStream, 0);
						StreamUtils.AssertEqualContent(logContent, decrStream);
						Assert.Equal(readStream.Length, logMd?.Size);
					}
				}
			}
		}
		[Fact]
		public async Task ReattemptingIngestOfLogWhereServerAssignedNewIdPicksUpTheExistingEntry() {
			var logId = Guid.NewGuid();
			var keyEncryptor = new KeyEncryptor(fixture.Certificates.Select(cert => cert.PublicKey), fixture.Random);
			// First, create the conflicting log:
			using (var client = fixture.CreateClient()) {
				var dataEncryptorOther = new DataEncryptor(fixture.Random, 1);
				Guid otherUserId = Guid.NewGuid();
				var request = buildUploadRequest(Stream.Null, new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-90), DateTime.Now.AddMinutes(-45), ".log.gz", LogContentEncoding.GZipCompressed,
					dataEncryptorOther.GenerateEncryptionInfo(keyEncryptor)), otherUserId, fixture.AppName);
				var response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();
			}
			// Now try to upload a log with the same id from a different user...
			var userId = Guid.NewGuid();
			var logMDTOBase = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, EncryptionInfo.CreateUnencrypted() /* Placeholder */);
			using (var logContent = generateRandomGZippedTestData()) {
				// ... but initially fail to do so due to a simulated connection fault:
				using (var client = fixture.CreateClient(new() {
					AllowAutoRedirect = false,
					HandleCookies = false
				})) {
					var dataEncryptor = new DataEncryptor(fixture.Random, 1);
					var logMDTO = new LogMetadataDTO(logMDTOBase.LogFileId, logMDTOBase.CreationTime, logMDTOBase.EndTime, logMDTOBase.NameSuffix, logMDTOBase.LogContentEncoding, dataEncryptor.GenerateEncryptionInfo(keyEncryptor));
					client.Timeout = TimeSpan.FromMilliseconds(100000);
					var cts = new CancellationTokenSource();
					var streamWrapper = new TriggeredBlockingStream(dataEncryptor.OpenEncryptionReadStream(logContent, 0, leaveOpen: true));
					var request = buildUploadRequest(streamWrapper, logMDTO, userId, fixture.AppName);
					var task = client.SendAsync(request, cts.Token);
					streamWrapper.TriggerReadReady(1);
					await PollWaitForLogMetadata(logId);
					streamWrapper.TriggerReadError(new IOException("Generic I/O error"));
					cts.Cancel();
					await Assert.ThrowsAnyAsync<Exception>(async () => await task);
				}
				// Now reattempt normally:
				using (var client = fixture.CreateClient()) {
					var dataEncryptor = new DataEncryptor(fixture.Random, 1);
					var logMDTO = new LogMetadataDTO(logMDTOBase.LogFileId, logMDTOBase.CreationTime, logMDTOBase.EndTime, logMDTOBase.NameSuffix, logMDTOBase.LogContentEncoding, dataEncryptor.GenerateEncryptionInfo(keyEncryptor));
					var request = buildUploadRequest(dataEncryptor.OpenEncryptionReadStream(logContent, 0, leaveOpen: true), logMDTO, userId, fixture.AppName);
					var response = await client.SendAsync(request);
					response.EnsureSuccessStatusCode();
				}
				using (var scope = fixture.Services.CreateScope()) {
					var db = scope.ServiceProvider.GetRequiredService<LogsContext>();
					var logMd = await db.LogMetadata.Where(lm => lm.LocalLogId == logId && lm.UserId == userId)
						.Include(lm => lm.App).Include(lm => lm.RecipientKeys).SingleOrDefaultAsync<LogMetadata?>();
					Assert.NotNull(logMd);
					Assert.NotEqual(logId, logMd?.Id);
					Assert.Equal(userId, logMd?.UserId);
					Assert.Equal(fixture.AppName, logMd?.App.Name);
					Assert.Equal(logMDTOBase.CreationTime.ToUniversalTime(), logMd?.CreationTime);
					Assert.Equal(logMDTOBase.EndTime.ToUniversalTime(), logMd?.EndTime);
					Assert.Equal(logMDTOBase.NameSuffix, logMd?.FilenameSuffix);
					Assert.Equal(logMDTOBase.LogContentEncoding, logMd?.Encoding);
					Assert.InRange(logMd?.UploadTime ?? DateTime.UnixEpoch, DateTime.Now.AddMinutes(-1).ToUniversalTime(), DateTime.Now.AddSeconds(1).ToUniversalTime());
					Assert.True(logMd?.Complete);

					var keyDecryptor = new KeyDecryptor(fixture.RecipientKeyPair1);
					var dataDecryptor = DataDecryptor.FromEncryptionInfo(logMd!.EncryptionInfo, keyDecryptor);
					Assert.NotNull(dataDecryptor);
					var fileRepo = scope.ServiceProvider.GetRequiredService<ILogFileRepository>();
					using (var readStream = await fileRepo.ReadLogAsync(fixture.AppName, userId, logMd?.Id ?? Guid.Empty, ".log.gz")) {
						logContent.Position = 0;
						using var decrStream = dataDecryptor.OpenDecryptionReadStream(readStream, 0);
						StreamUtils.AssertEqualContent(logContent, decrStream);
						Assert.Equal(readStream.Length, logMd?.Size);
					}
				}
			}
		}
		[Fact]
		public async Task LogIngestWithTooShortAppApiTokenFailsWithBadRequestError() {
			var logId = Guid.NewGuid();
			using (var client = fixture.CreateClient()) {
				Guid userId = Guid.NewGuid();
				var request = buildUploadRequest(Stream.Null, new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-90), DateTime.Now.AddMinutes(-45), ".log.gz", LogContentEncoding.GZipCompressed, EncryptionInfo.CreateUnencrypted()), userId, fixture.AppName, "x");
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				output.WriteStreamContents(response.Content.ReadAsStream());
			}
		}
		[Fact]
		public async Task AtemptToInjectPathInSuffixFailsWithBadRequestError() {
			var logId = Guid.NewGuid();
			using (var client = fixture.CreateClient()) {
				Guid userId = Guid.NewGuid();
				var request = buildUploadRequest(Stream.Null, new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-90), DateTime.Now.AddMinutes(-45), "/../test", LogContentEncoding.GZipCompressed, EncryptionInfo.CreateUnencrypted()), userId, fixture.AppName);
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				output.WriteStreamContents(response.Content.ReadAsStream());
			}
		}

		[Fact]
		public async Task AttemptToInjectEncryptedLogWithoutRecipientKeysFailsWithBadRequestError() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			var keyEncryptor = new KeyEncryptor(fixture.Certificates.Select(cert => cert.PublicKey), fixture.Random);
			var dataEncryptor = new DataEncryptor(fixture.Random, 1);
			var encryptionInfo = dataEncryptor.GenerateEncryptionInfo(keyEncryptor);
			encryptionInfo.DataKeys.Clear();
			var logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2), ".log.gz", LogContentEncoding.GZipCompressed, encryptionInfo);
			using (var logContent = generateRandomGZippedTestData()) {
				using (var client = fixture.CreateClient()) {
					using var encrStream = dataEncryptor.OpenEncryptionReadStream(logContent, 0, leaveOpen: true);
					var request = buildUploadRequest(encrStream, logMDTO, userId, fixture.AppName);
					var response = await client.SendAsync(request);
					Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
					output.WriteStreamContents(response.Content.ReadAsStream());
				}
			}
		}
	}
}
