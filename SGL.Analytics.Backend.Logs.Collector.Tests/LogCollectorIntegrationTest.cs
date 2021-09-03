using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.LogCollector;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Analytics.Backend.TestUtilities;
using SGL.Analytics.DTO;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Logs.Collector.Tests {
	public class LogCollectorIntegrationTestFixture : DbWebAppIntegrationTestFixtureBase<LogsContext, Startup> {
		public readonly string AppName = "LogCollectorIntegrationTest";
		public string AppApiToken { get; } = StringGenerator.GenerateRandomWord(32);

		public ITestOutputHelper? Output { get; set; } = null;

		protected override void SeedDatabase(LogsContext context) {
			context.Applications.Add(new Domain.Entity.Application(Guid.NewGuid(), AppName, AppApiToken));
			context.SaveChanges();
		}

		protected override void OverrideConfig(IServiceCollection services) {
			services.Configure<FileSystemLogRepositoryOptions>(options => options.StorageDirectory = Path.Combine(Environment.CurrentDirectory, "LogStorage"));
		}

		protected override IHostBuilder CreateHostBuilder() {
			return base.CreateHostBuilder().ConfigureLogging(logging => logging.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Trace));
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

		[Fact]
		public async Task LogIngestWithValidParametersSucceeds() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			var logMDTO = new LogMetadataDTO(fixture.AppName, userId, logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2));
			using (var logContent = generateRandomGZippedTestData()) {
				using (var client = fixture.CreateClient()) {
					var content = new StreamContent(logContent);
					content.Headers.MapDtoProperties(logMDTO);
					content.Headers.Add("App-API-Token", fixture.AppApiToken);
					content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
					var response = await client.PostAsync("/api/AnalyticsLog", content);
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
				var content = new StreamContent(logContent);
				content.Headers.MapDtoProperties(new LogMetadataDTO("DoesNotExist", userId, logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2)));
				content.Headers.Add("App-API-Token", fixture.AppApiToken);
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var response = await client.PostAsync("/api/AnalyticsLog", content);
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCode()).StatusCode);
			}
		}

		[Fact]
		public async Task LogIngestWithIncorrectApiTokenReturnsUnauthorizedError() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			using (var logContent = generateRandomGZippedTestData())
			using (var client = fixture.CreateClient()) {
				var content = new StreamContent(logContent);
				content.Headers.MapDtoProperties(new LogMetadataDTO(fixture.AppName, userId, logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2)));
				content.Headers.Add("App-API-Token", "IncorrectToken");
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var response = await client.PostAsync("/api/AnalyticsLog", content);
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCode()).StatusCode);
			}
		}

		[Fact]
		public async Task FailedLogIngestCanBeSuccessfullyRetried() {
			var userId = Guid.NewGuid();
			var logId = Guid.NewGuid();
			var logMDTO = new LogMetadataDTO(fixture.AppName, userId, logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2));
			using (var logContent = generateRandomGZippedTestData()) {
				using (var client = fixture.CreateClient(new() {
					// These options are required for the fault simulation,
					// because the default options attempt to duplicate the request content internally
					// and thus trigger the fault before the backend is invoked.
					AllowAutoRedirect = false,
					HandleCookies = false
				})) {
					// We want to simulate a network fault or the client crashing, resulting in a server-side timeout.
					// -> Use a very strict timeout. With the WebApplicationFactory test server that we are using, the timeout set on the client translates to the server.
					client.Timeout = TimeSpan.FromMilliseconds(200);
					var streamWrapper = new TriggeredBlockingStream(logContent);
					var content = new StreamContent(streamWrapper);
					content.Headers.MapDtoProperties(logMDTO);
					content.Headers.Add("App-API-Token", fixture.AppApiToken);
					content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
					var task = client.PostAsync("/api/AnalyticsLog", content);
					// Interrupt transfer of the body on the client.
					// Because the headers were already sent, the server-side request handling should be invoked despite the client error.
					// But reading from the body stream on the server will timeout.
					streamWrapper.TriggerReadError(new IOException("Generic I/O error"));
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
					var content = new StreamContent(logContent);
					content.Headers.MapDtoProperties(logMDTO);
					content.Headers.Add("App-API-Token", fixture.AppApiToken);
					content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
					var response = await client.PostAsync("/api/AnalyticsLog", content);
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
		[Fact]
		public async Task LogIngestWithCollidingIdSucceedsButGetsNewId() {
			var logId = Guid.NewGuid();
			// First, create the conflicting log:
			using (var client = fixture.CreateClient()) {
				var content = new StreamContent(Stream.Null);
				content.Headers.MapDtoProperties(new LogMetadataDTO(fixture.AppName, Guid.NewGuid(), logId, DateTime.Now.AddMinutes(-90), DateTime.Now.AddMinutes(-45)));
				content.Headers.Add("App-API-Token", fixture.AppApiToken);
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var response = await client.PostAsync("/api/AnalyticsLog", content);
				response.EnsureSuccessStatusCode();
			}
			// Now try to upload a log with the same id from a different user:
			var userId = Guid.NewGuid();
			var logMDTO = new LogMetadataDTO(fixture.AppName, userId, logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2));
			using (var logContent = generateRandomGZippedTestData()) {
				using (var client = fixture.CreateClient()) {
					var content = new StreamContent(logContent);
					content.Headers.MapDtoProperties(logMDTO);
					content.Headers.Add("App-API-Token", fixture.AppApiToken);
					content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
					var response = await client.PostAsync("/api/AnalyticsLog", content);
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
				var content = new StreamContent(Stream.Null);
				content.Headers.MapDtoProperties(new LogMetadataDTO(fixture.AppName, Guid.NewGuid(), logId, DateTime.Now.AddMinutes(-90), DateTime.Now.AddMinutes(-45)));
				content.Headers.Add("App-API-Token", fixture.AppApiToken);
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var response = await client.PostAsync("/api/AnalyticsLog", content);
				response.EnsureSuccessStatusCode();
			}
			// Now try to upload a log with the same id from a different user...
			var userId = Guid.NewGuid();
			var logMDTO = new LogMetadataDTO(fixture.AppName, userId, logId, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-2));
			using (var logContent = generateRandomGZippedTestData()) {
				// ... but initially fail to do so due to a simulated connection fault:
				using (var client = fixture.CreateClient(new() {
					AllowAutoRedirect = false,
					HandleCookies = false
				})) {
					client.Timeout = TimeSpan.FromMilliseconds(200);
					var streamWrapper = new TriggeredBlockingStream(logContent);
					var content = new StreamContent(streamWrapper);
					content.Headers.MapDtoProperties(logMDTO);
					content.Headers.Add("App-API-Token", fixture.AppApiToken);
					content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
					var task = client.PostAsync("/api/AnalyticsLog", content);
					streamWrapper.TriggerReadError(new IOException("Generic I/O error"));
					await Assert.ThrowsAnyAsync<Exception>(async () => await task);
				}
				// Now reattempt normally:
				using (var client = fixture.CreateClient()) {
					var content = new StreamContent(logContent);
					content.Headers.MapDtoProperties(logMDTO);
					content.Headers.Add("App-API-Token", fixture.AppApiToken);
					content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
					var response = await client.PostAsync("/api/AnalyticsLog", content);
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
