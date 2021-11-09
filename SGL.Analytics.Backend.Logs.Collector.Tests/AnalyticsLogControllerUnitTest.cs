using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Services;
using SGL.Analytics.Backend.Logs.Collector.Controllers;
using SGL.Analytics.DTO;
using SGL.Utilities.TestUtilities.XUnit;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Logs.Collector.Tests {
	public class AnalyticsLogControllerUnitTest {
		private readonly ITestOutputHelper output;
		private DummyLogManager logManager = new DummyLogManager();
		private ILoggerFactory loggerFactory;
		private AnalyticsLogController controller;
		private string apiToken = StringGenerator.GenerateRandomWord(32);

		public AnalyticsLogControllerUnitTest(ITestOutputHelper output) {
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			logManager.Apps.Add(nameof(AnalyticsLogControllerUnitTest), new Domain.Entity.Application(Guid.NewGuid(), nameof(AnalyticsLogControllerUnitTest), apiToken));
			controller = new AnalyticsLogController(logManager, logManager, loggerFactory.CreateLogger<AnalyticsLogController>());
		}

		private ControllerContext createControllerContext(string appNameClaim, Guid userIdClaim) {
			return createControllerContext(appNameClaim, userIdClaim, Stream.Null);
		}

		private ControllerContext createControllerContext(string appNameClaim, Guid userIdClaim, Stream bodyContent) {
			var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("appname", appNameClaim), new Claim("userid", userIdClaim.ToString()) }));
			var httpContext = new DefaultHttpContext();
			httpContext.User = principal;
			httpContext.Request.Body = bodyContent;
			httpContext.Request.ContentLength = bodyContent.Length;
			return new ControllerContext() { HttpContext = httpContext };
		}

		[Fact]
		public async Task IngestLogWithInvalidAppNameFailsWithUnauthorized() {
			controller.ControllerContext = createControllerContext("DoesNotExist", Guid.NewGuid());
			var res = await controller.IngestLog(apiToken, new LogMetadataDTO(Guid.NewGuid(), DateTime.Now.AddMinutes(-20), DateTime.Now.AddMinutes(-2), ".log", LogContentEncoding.Plain));
			Assert.IsType<UnauthorizedResult>(res);
			Assert.Empty(logManager.Ingests);
		}
		[Fact]
		public async Task IngestLogWithInvalidApiTokensFailsWithUnauthorized() {
			controller.ControllerContext = createControllerContext(nameof(AnalyticsLogControllerUnitTest), Guid.NewGuid());
			var res = await controller.IngestLog(StringGenerator.GenerateRandomWord(32), new LogMetadataDTO(Guid.NewGuid(), DateTime.Now.AddMinutes(-20), DateTime.Now.AddMinutes(-2), ".log", LogContentEncoding.Plain));
			Assert.IsType<UnauthorizedResult>(res);
			Assert.Empty(logManager.Ingests);
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
		public async Task IngestLogWithValidCredentialsSucceedsWithCreated() {
			using (var content = generateRandomGZippedTestData()) {
				var appName = nameof(AnalyticsLogControllerUnitTest);
				var userId = Guid.NewGuid();
				controller.ControllerContext = createControllerContext(appName, userId, content);
				var logDto = new LogMetadataDTO(Guid.NewGuid(), DateTime.Now.AddMinutes(-20), DateTime.Now.AddMinutes(-2), ".log", LogContentEncoding.Plain);
				var res = await controller.IngestLog(apiToken, logDto);
				Assert.Equal(StatusCodes.Status201Created, Assert.IsType<StatusCodeResult>(res).StatusCode);
				content.Position = 0;
				var ingest = logManager.Ingests.Single();
				Assert.Equal(appName, ingest.LogMetadata.App.Name);
				Assert.Equal(logDto.LogFileId, ingest.LogMetadata.Id);
				Assert.Equal(userId, ingest.LogMetadata.UserId);
				Assert.Equal(logDto.CreationTime.ToUniversalTime(), ingest.LogMetadata.CreationTime);
				Assert.Equal(logDto.EndTime.ToUniversalTime(), ingest.LogMetadata.EndTime);
				Assert.Equal(".log", ingest.LogMetadata.FilenameSuffix);
				Assert.Equal(LogContentEncoding.Plain, ingest.LogMetadata.Encoding);
				StreamUtils.AssertEqualContent(content, ingest.LogContent);
			}
		}
	}
}
