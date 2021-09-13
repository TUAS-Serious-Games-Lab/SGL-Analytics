using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.WebUtilities;
using SGL.Analytics.DTO;

namespace SGL.Analytics.Backend.Logs.Collector.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class AnalyticsLogController : ControllerBase {
		private readonly ILogManager logManager;
		private readonly IApplicationRepository appRepo;
		private readonly ILogger<AnalyticsLogController> logger;

		public AnalyticsLogController(ILogManager logManager, IApplicationRepository appRepo, ILogger<AnalyticsLogController> logger) {
			this.logManager = logManager;
			this.appRepo = appRepo;
			this.logger = logger;
		}

		// POST: api/AnalyticsLog
		[Consumes("application/octet-stream")]
		[ProducesResponseType(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
		[HttpPost]
		public async Task<ActionResult> IngestLog([FromHeader(Name = "App-API-Token")] string appApiToken, [DtoFromHeaderModelBinder] LogMetadataDTO logMetadata) {
			var app = await appRepo.GetApplicationByNameAsync(logMetadata.AppName);
			if (app is null) {
				logger.LogError("IngestLog POST request from user {userId} failed due to unknown application {appName}.", logMetadata.UserId, logMetadata.AppName);
				return Unauthorized();
			}
			else if (app.ApiToken != appApiToken) {
				logger.LogError("IngestLog POST request from user {userId} failed due to incorrect API token for application {appName}.", logMetadata.UserId, logMetadata.AppName);
				return Unauthorized();
			}

			try {
				await logManager.IngestLogAsync(logMetadata, Request.Body);
				return StatusCode(StatusCodes.Status201Created);
			}
			catch (Exception ex) {
				logger.LogError(ex, "IngestLog POST request from user {userId} failed due unexpected exception.", logMetadata.UserId);
				throw;
			}
		}
	}
}
