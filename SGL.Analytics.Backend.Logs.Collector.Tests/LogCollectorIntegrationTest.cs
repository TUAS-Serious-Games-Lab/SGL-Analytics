using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGL.Analytics.Backend.LogCollector;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
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
		protected override void SeedDatabase(LogsContext context) {
			context.Applications.Add(new Domain.Entity.Application(0, AppName, AppApiToken));
			context.SaveChanges();
		}

		protected override IHostBuilder CreateHostBuilder() {
			return base.CreateHostBuilder().ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string> {
				["FileSystemLogRepository:StorageDirectory"] = "./bin/Debug/LogStorage"
			}));
		}
	}

	public class LogCollectorIntegrationTest : IClassFixture<LogCollectorIntegrationTestFixture> {
		private readonly LogCollectorIntegrationTestFixture fixture;
		private readonly ITestOutputHelper output;

		public LogCollectorIntegrationTest(LogCollectorIntegrationTestFixture fixture, ITestOutputHelper output) {
			this.fixture = fixture;
			this.output = output;
		}

		private Stream generateRandomGZippedTestData() {
			var stream = new MemoryStream();
			using (var writer = new StreamWriter(new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))) {
				for (int i = 0; i < 20; ++i) {
					writer.WriteLine(StringGenerator.GenerateRandomString(128));
				}
			}
			return stream;
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
	}
}
