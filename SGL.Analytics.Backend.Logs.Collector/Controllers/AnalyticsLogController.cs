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
		public async Task<ActionResult<LogMetadata>> IngestLog([FromHeader(Name = "App-API-Token")] string appApiToken, [FromHeader] LogMetadataDTO logMetaDTO, [FromBody] Stream logContent) {
			// TODO: Check API token.
			return Unauthorized();

			await _repository.IngestLogAsync(logMetaDTO, logContent);

			return StatusCode(((int)HttpStatusCode.Created));
		}
	}
}
