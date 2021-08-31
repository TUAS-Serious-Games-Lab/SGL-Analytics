using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.WebUtilities;
using SGL.Analytics.DTO;

namespace SGL.Analytics.Backend.LogCollector.Controllers {
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
		// To protect from overposting attacks, enable the specific properties you want to bind to, for
		// more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
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
				return StatusCode(((int)HttpStatusCode.Created));
			}
			catch (Exception ex) {
				logger.LogError(ex, "IngestLog POST request from user {userId} failed due unexpected exception.", logMetadata.UserId);
				throw;
			}
		}
	}
}
