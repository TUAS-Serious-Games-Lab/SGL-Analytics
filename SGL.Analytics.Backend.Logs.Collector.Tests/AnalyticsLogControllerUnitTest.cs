using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Collector.Controllers;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Logs.Collector.Tests {
	public class AnalyticsLogControllerUnitTest {
		private readonly ITestOutputHelper output;
		private Utilities.Backend.TestUtilities.Applications.DummyApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions> appRepo = new();
		private DummyLogManager logManager;
		private ILoggerFactory loggerFactory;
		private AnalyticsLogController controller;
		private string apiToken = StringGenerator.GenerateRandomWord(32);

		public AnalyticsLogControllerUnitTest(ITestOutputHelper output) {
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			logManager = new DummyLogManager(appRepo);
			appRepo.AddApplicationAsync(new Domain.Entity.Application(Guid.NewGuid(), nameof(AnalyticsLogControllerUnitTest), apiToken)).Wait();
			controller = new AnalyticsLogController(logManager, appRepo, loggerFactory.CreateLogger<AnalyticsLogController>(), new NullMetricsManager());
		}

		private Task<ControllerContext> createControllerContext(string appNameClaim, Guid userIdClaim, LogMetadataDTO metadata) {
			return createControllerContext(appNameClaim, userIdClaim, metadata, Stream.Null);
		}

		private async Task<ControllerContext> createControllerContext(string appNameClaim, Guid userIdClaim, LogMetadataDTO metadata, Stream content) {
			var multipartBodyObj = new MultipartFormDataContent();
			multipartBodyObj.Add(JsonContent.Create(metadata, MediaTypeHeaderValue.Parse("application/json"), DTO.JsonOptions.RestOptions), "metadata");
			var contentObj = new StreamContent(content);
			contentObj.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
			multipartBodyObj.Add(contentObj, "content");
			var multipartStream = await multipartBodyObj.ReadAsStreamAsync();

			var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("appname", appNameClaim), new Claim("userid", userIdClaim.ToString()) }));
			var httpContext = new DefaultHttpContext();
			httpContext.User = principal;
			httpContext.Request.Body = multipartStream;
			httpContext.Request.ContentLength = multipartStream.Length;
			httpContext.Request.ContentType = multipartBodyObj.Headers.ContentType!.ToString();
			return new ControllerContext() { HttpContext = httpContext };
		}

		[Fact]
		public async Task IngestLogWithInvalidAppNameFailsWithUnauthorized() {
			var dto = new LogMetadataDTO(Guid.NewGuid(), DateTime.Now.AddMinutes(-20), DateTime.Now.AddMinutes(-2), ".log", LogContentEncoding.Plain);
			controller.ControllerContext = await createControllerContext("DoesNotExist", Guid.NewGuid(), dto);
			var res = await controller.IngestLog(apiToken);
			Assert.IsType<UnauthorizedResult>(res);
			Assert.Empty(logManager.Ingests);
		}
		[Fact]
		public async Task IngestLogWithInvalidApiTokensFailsWithUnauthorized() {
			var dto = new LogMetadataDTO(Guid.NewGuid(), DateTime.Now.AddMinutes(-20), DateTime.Now.AddMinutes(-2), ".log", LogContentEncoding.Plain);
			controller.ControllerContext = await createControllerContext(nameof(AnalyticsLogControllerUnitTest), Guid.NewGuid(), dto);
			var res = await controller.IngestLog(StringGenerator.GenerateRandomWord(32));
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
				var logDto = new LogMetadataDTO(Guid.NewGuid(), DateTime.Now.AddMinutes(-20), DateTime.Now.AddMinutes(-2), ".log", LogContentEncoding.Plain);
				controller.ControllerContext = await createControllerContext(appName, userId, logDto, content);
				var res = await controller.IngestLog(apiToken);
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
