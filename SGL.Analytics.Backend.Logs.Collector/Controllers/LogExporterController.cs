﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector.Controllers {
	[Route("api/analytics/log/v2")]
	[ApiController]
	[Authorize(Policy = "ExporterUser")]
	public class LogExporterController : ControllerBase {
		private readonly ILogManager logManager;
		private readonly ILogger<AnalyticsLogController> logger;
		private readonly IMetricsManager metrics;

		public LogExporterController(ILogManager logManager, ILogger<AnalyticsLogController> logger, IMetricsManager metrics) {
			this.logManager = logManager;
			this.logger = logger;
			this.metrics = metrics;
		}

		private static DownstreamLogMetadataDTO ToDto(LogFile log) {
			return new DownstreamLogMetadataDTO(log.Id, log.CreationTime, log.EndTime, log.UploadTime, log.Size,
							log.FilenameSuffix, log.Encoding, log.EncryptionInfo);
		}

		private ActionResult? GetCredentials(out string appName, out KeyId keyId, out string exporterDN) {
			try {
				appName = User.GetClaim("appname");
				keyId = User.GetClaim<KeyId>("keyid", KeyId.TryParse!);
				exporterDN = User.GetClaim("exporter-dn");
				return null;
			}
			catch (ClaimException ex) {
				logger.LogError(ex, "GetLogIds operation failed due to an error with the required security token claims.");
				metrics.HandleIncorrectSecurityTokenClaimsError();
				appName = null!;
				keyId = null!;
				exporterDN = null!;
				return Unauthorized("The operation failed due to a security token error.");
			}
		}

		[HttpGet]
		public async Task<ActionResult<IEnumerable<Guid>>> GetLogIds(CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var keyId, out var exporterDN);
			if (credResult != null) return credResult;
			var logs = await logManager.ListLogsAsync(appName, null, exporterDN, ct);
			var result = logs.Select(log => log.Id).ToList();
			return result;
		}

		[HttpGet("all")]
		public async Task<ActionResult<IEnumerable<DownstreamLogMetadataDTO>>> GetMetadataForAllLogs([FromQuery] KeyId? recipient = null, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var keyId, out var exporterDN);
			if (credResult != null) return credResult;
			var logs = await logManager.ListLogsAsync(appName, recipient, exporterDN, ct);
			var result = logs.Select(log => ToDto(log)).ToList();
			return result;
		}

		[HttpGet("{id:Guid}/metadata")]
		public async Task<ActionResult<DownstreamLogMetadataDTO>> GetLogMetadataById(Guid id, [FromQuery] KeyId? recipient = null, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var keyId, out var exporterDN);
			if (credResult != null) return credResult;
			var log = await logManager.GetLogByIdAsync(id, appName, recipient, exporterDN, ct);
			var result = ToDto(log);
			return result;
		}

		[HttpGet("{id:Guid}/content")]
		public async Task<ActionResult> GetLogContentById(Guid id, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var keyId, out var exporterDN);
			if (credResult != null) return credResult;
			var log = await logManager.GetLogByIdAsync(id, appName, null, exporterDN, ct);
			var content = await log.OpenReadAsync(ct);
			return File(content, "application/octet-stream", log.Id.ToString() + log.FilenameSuffix, enableRangeProcessing: true);
		}
	}
}
