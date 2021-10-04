using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Security;
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
		[RequestSizeLimit(200 * 1024 * 1024)]
		[Consumes("application/octet-stream")]
		[ProducesResponseType(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
		[HttpPost]
		[Authorize]
		public async Task<ActionResult> IngestLog([FromHeader(Name = "App-API-Token")] string appApiToken, [DtoFromHeaderModelBinder] LogMetadataDTO logMetadata, CancellationToken ct = default) {
			Guid userId;
			string appName;
			try {
				userId = User.GetClaim<Guid>("userid", Guid.TryParse);
				appName = User.GetClaim("appname");
			}
			catch (ClaimException ex) {
				logger.LogError(ex, "IngestLog operation failed due to an error with the required security token claims.");
				return Unauthorized("The operation failed due to a security token error.");
			}
			Domain.Entity.Application? app = null;
			try {
				app = await appRepo.GetApplicationByNameAsync(appName, ct);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("IngestLog POST request from user {userId} was cancelled while fetching application metadata.", userId);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "IngestLog POST request from user {userId} failed due to unexpected exception when fetching application metadata.", userId);
				throw;
			}
			if (app is null) {
				logger.LogError("IngestLog POST request from user {userId} failed due to unknown application {appName}.", userId, appName);
				return Unauthorized();
			}
			else if (app.ApiToken != appApiToken) {
				logger.LogError("IngestLog POST request from user {userId} failed due to incorrect API token for application {appName}.", userId, appName);
				return Unauthorized();
			}

			try {
				await logManager.IngestLogAsync(userId, appName, logMetadata, Request.Body, ct);
				return StatusCode(StatusCodes.Status201Created);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("IngestLog POST request from user {userId} was cancelled while fetching application metadata.", userId);
				throw;
			}
			catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413RequestEntityTooLarge) {
				logger.LogCritical("IngestLog POST request from user {userId} failed because the log file was too large for the server's limit. " +
					"The Content-Length given by the client was {size}.", userId, HttpContext.Request.ContentLength);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "IngestLog POST request from user {userId} failed due to unexpected exception during log ingest.", userId);
				throw;
			}
		}
	}
}
