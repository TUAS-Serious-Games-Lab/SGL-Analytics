using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Prometheus;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector.Controllers {
	[Route("api/analytics/log/v2/rekey")]
	[ApiController]
	[Authorize(Policy = "ExporterUser")]
	public class RekeyingController : ControllerBase {
		private readonly ILogManager logManager;
		private readonly ILogger<LogExporterController> logger;
		private readonly IMetricsManager metrics;

		public RekeyingController(ILogManager logManager, ILogger<LogExporterController> logger, IMetricsManager metrics) {
			this.logManager = logManager;
			this.logger = logger;
			this.metrics = metrics;
		}

		private ActionResult? GetCredentials(out string appName, out KeyId keyId, out string exporterDN, string operationName) {
			try {
				appName = User.GetClaim("appname");
				keyId = User.GetClaim<KeyId>("keyid", KeyId.TryParse!);
				exporterDN = User.GetClaim("exporter-dn");
				return null;
			}
			catch (ClaimException ex) {
				logger.LogError(ex, "{operationName} operation failed due to an error with the required security token claims.", operationName);
				metrics.HandleIncorrectSecurityTokenClaimsError();
				appName = null!;
				keyId = null!;
				exporterDN = null!;
				return Unauthorized("The operation failed due to a security token error.");
			}
		}

		[HttpGet("{keyId}")]
		public async Task<ActionResult<Dictionary<Guid, EncryptionInfo>>> GetKeysForRekeying([FromRoute(Name = "keyId")] KeyId recipientKeyId, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN, nameof(GetKeysForRekeying));
			if (credResult != null) return credResult;
			try {
				logger.LogInformation("Listing key material for all logs in application {appName} with recipient keys for {recipientKeyId} for rekeying by exporter {exporterKeyId} ({exporterDN}).",
					appName, recipientKeyId, exporterKeyId, exporterDN);
				var logs = await logManager.ListLogsAsync(appName, recipientKeyId, exporterDN, ct);
				var result = logs.ToDictionary(log => log.Id, log => log.EncryptionInfo);
				return result;
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (ApplicationDoesNotExistException ex) {
				logger.LogError(ex, "GetKeysForRekeying GET request for non-existent application {appName} from exporter {keyId} ({exporterDN}).", appName, exporterKeyId, exporterDN);
				metrics.HandleUnknownAppError(appName);
				return NotFound($"Application {appName} not found.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "GetKeysForRekeying GET request for application {appName} from exporter {keyId} ({exporterDN}) failed due to unexpected exception.",
					appName, exporterKeyId, exporterDN);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}

		[HttpPut("{keyId}")]
		public async Task<ActionResult> PutRekeyedKeys([FromRoute(Name = "keyId")] KeyId newRecipientKeyId, [FromBody] Dictionary<Guid, DataKeyInfo> dataKeys, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN, nameof(PutRekeyedKeys));
			if (credResult != null) return credResult;
			try {
				await logManager.AddRekeyedKeysAsync(appName, newRecipientKeyId, dataKeys, exporterDN, ct);
				return Ok(dataKeys);
			}
			catch (OperationCanceledException) {
				throw;
			}
			// TODO: catch applicable exceptions
			catch (Exception ex) {
				logger.LogError(ex, "PutRekeyedKeys PUT request for application {appName} from exporter {keyId} ({exporterDN}) failed due to unexpected exception.",
					appName, exporterKeyId, exporterDN);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}
	}
}
