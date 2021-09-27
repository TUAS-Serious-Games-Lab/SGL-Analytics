using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Analytics.Backend.Security;
using SGL.Analytics.Backend.TestUtilities;
using SGL.Analytics.DTO;
using SGL.Analytics.TestUtilities;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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

		public LogCollectorIntegrationTestFixture() {
			JwtConfig = new() {
				["Jwt:Audience"] = JwtOptions.Audience,
				["Jwt:Issuer"] = JwtOptions.Issuer,
				["Jwt:SymmetricKey"] = JwtOptions.SymmetricKey,
				["Logging:File:Sinks:0:FilenameFormat"] = "{Time:yyyy-MM}/{Time:yyyy-MM-dd}_{ServiceName}.log",
				["Logging:File:Sinks:1:FilenameFormat"] = "{Time:yyyy-MM}/Categories/{Category}.log",
				["Logging:File:Sinks:2:FilenameFormat"] = "{Time:yyyy-MM}/Requests/{RequestId}.log",
				["Logging:File:Sinks:2:MessageFormat"] = "[{RequestPath}] [{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}",
				["Logging:File:Sinks:2:MessageFormatException"] = "[{RequestPath}] [{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}",
				["Logging:File:Sinks:3:FilenameFormat"] = "{Time:yyyy-MM}/users/{UserId}/{Time:yyyy-MM-dd}_{ServiceName}_{UserId}.log",
			};
			TokenGenerator = new JwtTokenGenerator(JwtOptions.Issuer, JwtOptions.Audience, JwtOptions.SymmetricKey);
		}

		protected override void SeedDatabase(LogsContext context) {
			context.Applications.Add(new Domain.Entity.Application(Guid.NewGuid(), AppName, AppApiToken));
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

		public LogCollectorIntegrationTest(LogCollectorIntegrationTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
			fixture.Output = output;
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

		private HttpRequestMessage buildUploadRequest(Stream logContent, LogMetadataDTO logMDTO, Guid userId, string appName) {
			var content = new StreamContent(logContent);
			content.Headers.MapDtoProperties(logMDTO);
			content.Headers.Add("App-API-Token", fixture.AppApiToken);
			content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
			var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsLog");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
				fixture.TokenGenerator.GenerateToken(userId, TimeSpan.FromMinutes(5), ("appname", appName)));
			request.Content = content;
			return request;
		}

		[Fact]
		public async Task LogIngestWithValidParametersSucceeds() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			var logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2));
			using (var logContent = generateRandomGZippedTestData()) {
				using (var client = fixture.CreateClient()) {
					var request = buildUploadRequest(logContent, logMDTO, userId, fixture.AppName);
					var response = await client.SendAsync(request);
					response.EnsureSuccessStatusCode();
				}
				using (var scope = fixture.Services.CreateScope()) {
					var fileRepo = scope.ServiceProvider.GetRequiredService<ILogFileRepository>();
					using (var readStream = await fileRepo.ReadLogAsync(fixture.AppName, userId, logId, ".log.gz")) {
						logContent.Position = 0;
						StreamUtils.AssertEqualContent(logContent, readStream);
					}
					var logMdRepo = scope.ServiceProvider.GetRequiredService<ILogMetadataRepository>();
					var logMd = await logMdRepo.GetLogMetadataByIdAsync(logId);
					Assert.NotNull(logMd);
					Assert.Equal(userId, logMd?.UserId);
					Assert.Equal(fixture.AppName, logMd?.App.Name);
					Assert.Equal(logMDTO.CreationTime.ToUniversalTime(), logMd?.CreationTime);
					Assert.Equal(logMDTO.EndTime.ToUniversalTime(), logMd?.EndTime);
					Assert.InRange(logMd?.UploadTime ?? DateTime.UnixEpoch, DateTime.Now.AddMinutes(-1).ToUniversalTime(), DateTime.Now.AddSeconds(1).ToUniversalTime());
					Assert.True(logMd?.Complete);
				}
			}
		}

		[Fact]
		public async Task LogIngestWithUnknownApplicationReturnsUnauthorizedError() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			using (var logContent = generateRandomGZippedTestData())
			using (var client = fixture.CreateClient()) {
				var request = buildUploadRequest(logContent, new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2)), userId, "DoesNotExist");
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
				content.Headers.MapDtoProperties(new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2)));
				content.Headers.Add("App-API-Token", "IncorrectToken");
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsLog");
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
					fixture.TokenGenerator.GenerateToken(userId, TimeSpan.FromMinutes(5), ("appname", fixture.AppName)));
				request.Content = content;
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
				content.Headers.MapDtoProperties(new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2)));
				content.Headers.Add("App-API-Token", fixture.AppApiToken);
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsLog");
				request.Content = content;
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
				content.Headers.MapDtoProperties(new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2)));
				content.Headers.Add("App-API-Token", fixture.AppApiToken);
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsLog");
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
									new JwtTokenGenerator(fixture.JwtOptions.Issuer, fixture.JwtOptions.Audience, "InvalidKeyInvalidKeyInvalidKeyInvalidKeyInvalidKey")
									.GenerateToken(userId, TimeSpan.FromMinutes(5), ("appname", fixture.AppName)));
				request.Content = content;
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
				content.Headers.MapDtoProperties(new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2)));
				content.Headers.Add("App-API-Token", fixture.AppApiToken);
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsLog");
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
									new JwtTokenGenerator("InvalidIssuer", fixture.JwtOptions.Audience, fixture.JwtOptions.SymmetricKey!)
									.GenerateToken(userId, TimeSpan.FromMinutes(5), ("appname", fixture.AppName)));
				request.Content = content;
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
				content.Headers.MapDtoProperties(new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2)));
				content.Headers.Add("App-API-Token", fixture.AppApiToken);
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var request = new HttpRequestMessage(HttpMethod.Post, "/api/AnalyticsLog");
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
									new JwtTokenGenerator(fixture.JwtOptions.Issuer, "InvalidAudience", fixture.JwtOptions.SymmetricKey!)
									.GenerateToken(userId, TimeSpan.FromMinutes(5), ("appname", fixture.AppName)));
				request.Content = content;
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
			var logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2));
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
					var streamWrapper = new TriggeredBlockingStream(logContent);
					var request = buildUploadRequest(streamWrapper, logMDTO, userId, fixture.AppName);
					var task = client.SendAsync(request, cts.Token);

					streamWrapper.TriggerReadReady(1);
					// Because the headers were already sent, the server-side request handling should be invoked despite the body transfer being stalled after the first byte.
					// Poll the database to wait for the LogMetadata entry to appear:
					await PollWaitForLogMetadata(logId);
					// After it has appeared, inject the error into the body transfer and cancel the request:
					streamWrapper.TriggerReadError(new IOException("Generic I/O error"));
					cts.Cancel();
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
					Assert.Equal(logMDTO.CreationTime.ToUniversalTime(), logMd?.CreationTime);
					Assert.Equal(logMDTO.EndTime.ToUniversalTime(), logMd?.EndTime);
					Assert.InRange(logMd?.UploadTime ?? DateTime.UnixEpoch, DateTime.Now.AddMinutes(-1).ToUniversalTime(), DateTime.Now.AddSeconds(1).ToUniversalTime());
					Assert.False(logMd?.Complete);
				}
				// Reattempt normally...
				using (var client = fixture.CreateClient()) {
					logContent.Position = 0;
					var request = buildUploadRequest(logContent, logMDTO, userId, fixture.AppName);
					var response = await client.SendAsync(request);
					response.EnsureSuccessStatusCode();
				}
				using (var scope = fixture.Services.CreateScope()) {
					// Should be fine now.
					var fileRepo = scope.ServiceProvider.GetRequiredService<ILogFileRepository>();
					using (var readStream = await fileRepo.ReadLogAsync(fixture.AppName, userId, logId, ".log.gz")) {
						logContent.Position = 0;
						StreamUtils.AssertEqualContent(logContent, readStream);
					}
					var logMdRepo = scope.ServiceProvider.GetRequiredService<ILogMetadataRepository>();
					var logMd = await logMdRepo.GetLogMetadataByIdAsync(logId);
					Assert.NotNull(logMd);
					Assert.Equal(userId, logMd?.UserId);
					Assert.Equal(fixture.AppName, logMd?.App.Name);
					Assert.Equal(logMDTO.CreationTime.ToUniversalTime(), logMd?.CreationTime);
					Assert.Equal(logMDTO.EndTime.ToUniversalTime(), logMd?.EndTime);
					Assert.InRange(logMd?.UploadTime ?? DateTime.UnixEpoch, DateTime.Now.AddMinutes(-1).ToUniversalTime(), DateTime.Now.AddSeconds(1).ToUniversalTime());
					Assert.True(logMd?.Complete);
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
			// First, create the conflicting log:
			using (var client = fixture.CreateClient()) {
				Guid otherUserId = Guid.NewGuid();
				var request = buildUploadRequest(Stream.Null, new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-90), DateTime.Now.AddMinutes(-45)), otherUserId, fixture.AppName);
				var response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();
			}
			// Now try to upload a log with the same id from a different user:
			var userId = Guid.NewGuid();
			var logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2));
			using (var logContent = generateRandomGZippedTestData()) {
				using (var client = fixture.CreateClient()) {
					var request = buildUploadRequest(logContent, logMDTO, userId, fixture.AppName);
					var response = await client.SendAsync(request);
					response.EnsureSuccessStatusCode();
				}
				using (var scope = fixture.Services.CreateScope()) {
					var db = scope.ServiceProvider.GetRequiredService<LogsContext>();
					var logMd = await db.LogMetadata.Where(lm => lm.LocalLogId == logId && lm.UserId == userId).Include(lm => lm.App).SingleOrDefaultAsync<LogMetadata?>();
					Assert.NotNull(logMd);
					Assert.NotEqual(logId, logMd?.Id);
					Assert.Equal(userId, logMd?.UserId);
					Assert.Equal(fixture.AppName, logMd?.App.Name);
					Assert.Equal(logMDTO.CreationTime.ToUniversalTime(), logMd?.CreationTime);
					Assert.Equal(logMDTO.EndTime.ToUniversalTime(), logMd?.EndTime);
					Assert.InRange(logMd?.UploadTime ?? DateTime.UnixEpoch, DateTime.Now.AddMinutes(-1).ToUniversalTime(), DateTime.Now.AddSeconds(1).ToUniversalTime());
					Assert.True(logMd?.Complete);
					var fileRepo = scope.ServiceProvider.GetRequiredService<ILogFileRepository>();
					using (var readStream = await fileRepo.ReadLogAsync(fixture.AppName, userId, logMd?.Id ?? Guid.Empty, ".log.gz")) {
						logContent.Position = 0;
						StreamUtils.AssertEqualContent(logContent, readStream);
					}
				}
			}
		}
		[Fact]
		public async Task ReattemptingIngestOfLogWhereServerAssignedNewIdPicksUpTheExistingEntry() {
			var logId = Guid.NewGuid();
			// First, create the conflicting log:
			using (var client = fixture.CreateClient()) {
				Guid otherUserId = Guid.NewGuid();
				var request = buildUploadRequest(Stream.Null, new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-90), DateTime.Now.AddMinutes(-45)), otherUserId, fixture.AppName);
				var response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();
			}
			// Now try to upload a log with the same id from a different user...
			var userId = Guid.NewGuid();
			var logMDTO = new LogMetadataDTO(logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2));
			using (var logContent = generateRandomGZippedTestData()) {
				// ... but initially fail to do so due to a simulated connection fault:
				using (var client = fixture.CreateClient(new() {
					AllowAutoRedirect = false,
					HandleCookies = false
				})) {
					client.Timeout = TimeSpan.FromMilliseconds(100000);
					var cts = new CancellationTokenSource();
					var streamWrapper = new TriggeredBlockingStream(logContent);
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
					var content = new StreamContent(logContent);
					var request = buildUploadRequest(logContent, logMDTO, userId, fixture.AppName);
					var response = await client.SendAsync(request);
					response.EnsureSuccessStatusCode();
				}
				using (var scope = fixture.Services.CreateScope()) {
					var db = scope.ServiceProvider.GetRequiredService<LogsContext>();
					var logMd = await db.LogMetadata.Where(lm => lm.LocalLogId == logId && lm.UserId == userId).Include(lm => lm.App).SingleOrDefaultAsync<LogMetadata?>();
					Assert.NotNull(logMd);
					Assert.NotEqual(logId, logMd?.Id);
					Assert.Equal(userId, logMd?.UserId);
					Assert.Equal(fixture.AppName, logMd?.App.Name);
					Assert.Equal(logMDTO.CreationTime.ToUniversalTime(), logMd?.CreationTime);
					Assert.Equal(logMDTO.EndTime.ToUniversalTime(), logMd?.EndTime);
					Assert.InRange(logMd?.UploadTime ?? DateTime.UnixEpoch, DateTime.Now.AddMinutes(-1).ToUniversalTime(), DateTime.Now.AddSeconds(1).ToUniversalTime());
					Assert.True(logMd?.Complete);
					var fileRepo = scope.ServiceProvider.GetRequiredService<ILogFileRepository>();
					using (var readStream = await fileRepo.ReadLogAsync(fixture.AppName, userId, logMd?.Id ?? Guid.Empty, ".log.gz")) {
						logContent.Position = 0;
						StreamUtils.AssertEqualContent(logContent, readStream);
					}
				}
			}
		}
	}
}
