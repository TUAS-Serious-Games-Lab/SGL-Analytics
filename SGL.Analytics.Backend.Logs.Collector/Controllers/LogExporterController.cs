using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
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
	/// <summary>
	/// Implements the API routes for exporting game analytics logs.
	/// These routes are prefixed under <c>api/analytics/log/v2</c>.
	/// All routes here require an authorization that satisfies the <c>ExporterUser</c> policy.
	/// </summary>
	[Route("api/analytics/log/v2")]
	[ApiController]
	[Authorize(Policy = "ExporterUser")]
	public class LogExporterController : ControllerBase {
		private readonly ILogManager logManager;
		private readonly ILogger<LogExporterController> logger;
		private readonly IMetricsManager metrics;

		/// <summary>
		/// Instantiates the controller, injecting the required dependency objects.
		/// </summary>
		public LogExporterController(ILogManager logManager, ILogger<LogExporterController> logger, IMetricsManager metrics) {
			this.logManager = logManager;
			this.logger = logger;
			this.metrics = metrics;
		}

		private static DownstreamLogMetadataDTO ToDto(LogFile log) {
			return new DownstreamLogMetadataDTO(log.Id, log.UserId, log.CreationTime, log.EndTime, log.UploadTime, log.Size,
							log.FilenameSuffix, log.Encoding, log.EncryptionInfo);
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

		/// <summary>
		/// Implements <c>GET api/analytics/log/v2</c>, which provides the list of the ids of all analytics logs of the application indicated by the authorization token.
		/// </summary>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		/// <returns>A JSON list of GUIDs for the analytics logs, or an error state.</returns>
		[ProducesResponseType(typeof(IEnumerable<Guid>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
		[HttpGet]
		public async Task<ActionResult<IEnumerable<Guid>>> GetLogIdList(CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var keyId, out var exporterDN, nameof(GetLogIdList));
			if (credResult != null) return credResult;
			try {
				logger.LogInformation("Listing log ids in application {appName} for exporter {keyId} ({exporterDN}).", appName, keyId, exporterDN);
				var logs = await logManager.ListLogsAsync(appName, null, exporterDN, ct);
				var result = logs.Select(log => log.Id).ToList();
				return result;
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (ApplicationDoesNotExistException ex) {
				logger.LogError(ex, "GetLogIdList GET request for non-existent application {appName} from exporter {keyId} ({exporterDN}).", appName, keyId, exporterDN);
				metrics.HandleUnknownAppError(appName);
				return NotFound($"Application {appName} not found.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "GetLogIdList GET request for application {appName} from exporter {keyId} ({exporterDN}) failed due to unexpected exception.",
					appName, keyId, exporterDN);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}

		/// <summary>
		/// Implements <c>GET api/analytics/log/v2/all</c>, which provides the log metadata for all analytics logs of the application indicated by the authorization token.
		/// The returned data contains the encrypted data keys for the recipient key with the key id indicated by <paramref name="recipientKeyId"/>.
		/// </summary>
		/// <param name="recipientKeyId">The id of the recipient key pair for which to retrieve the data keys.</param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		/// <returns>A sequence of <see cref="UserMetadataDTO"/>s for the user registrations, or an error state.</returns>
		[ProducesResponseType(typeof(IEnumerable<DownstreamLogMetadataDTO>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
		[HttpGet("all")]
		public async Task<ActionResult<IEnumerable<DownstreamLogMetadataDTO>>> GetMetadataForAllLogs([FromQuery(Name = "recipient")] KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN, nameof(GetMetadataForAllLogs));
			if (credResult != null) return credResult;
			try {
				logger.LogInformation("Listing metadata for all logs in application {appName} with recipient keys for {recipientKeyId} for exporter {exporterKeyId} ({exporterDN}).",
					appName, recipientKeyId, exporterKeyId, exporterDN);
				var logs = await logManager.ListLogsAsync(appName, recipientKeyId, exporterDN, ct);
				var result = logs.Select(log => ToDto(log)).ToList();
				return result;
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (ApplicationDoesNotExistException ex) {
				logger.LogError(ex, "GetMetadataForAllLogs GET request for non-existent application {appName} from exporter {keyId} ({exporterDN}).", appName, exporterKeyId, exporterDN);
				metrics.HandleUnknownAppError(appName);
				return NotFound($"Application {appName} not found.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "GetMetadataForAllLogs GET request for application {appName} from exporter {keyId} ({exporterDN}) failed due to unexpected exception.",
					appName, exporterKeyId, exporterDN);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}

		/// <summary>
		/// Implements <c>GET api/analytics/log/v2/{id:Guid}/metadata</c>, which retrieves the metadata for a specific analytics log.
		/// The returned data contains the encrypted data key for the recipient key with the key id indicated by <paramref name="recipientKeyId"/>.
		/// </summary>
		/// <param name="id">The id of the log of which to retrieve the metadata.</param>
		/// <param name="recipientKeyId">The id of the recipient key pair for which to retrieve the data key.</param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		/// <returns>A <see cref="UserMetadataDTO"/> for the user registration, or an error state.</returns>
		[ProducesResponseType(typeof(DownstreamLogMetadataDTO), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
		[HttpGet("{id:Guid}/metadata")]
		public async Task<ActionResult<DownstreamLogMetadataDTO>> GetLogMetadataById([FromRoute] Guid id, [FromQuery(Name = "recipient")] KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN, nameof(GetLogMetadataById));
			if (credResult != null) return credResult;
			try {
				logger.LogInformation("Fetching metadata for log {logId} in application {appName} with recipient key for {recipientKeyId} for exporter {exporterKeyId} ({exporterDN}).",
					id, appName, recipientKeyId, exporterKeyId, exporterDN);
				var log = await logManager.GetLogByIdAsync(id, appName, recipientKeyId, exporterDN, ct);
				var result = ToDto(log);
				return result;
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (ApplicationDoesNotExistException ex) {
				logger.LogError(ex, "GetLogMetadataById GET request for non-existent application {appName} from exporter {keyId} ({exporterDN}).", appName, exporterKeyId, exporterDN);
				metrics.HandleUnknownAppError(appName);
				return NotFound($"Application {appName} not found.");
			}
			catch (LogNotFoundException ex) {
				logger.LogError(ex, "GetLogMetadataById GET request for application {appName} from exporter {keyId} ({exporterDN}) failed because the requested log {logId} was not found.",
					appName, exporterKeyId, exporterDN, id);
				metrics.HandleLogNotFoundError(appName);
				return NotFound($"Log {id} not found in the application {appName}.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "GetLogMetadataById GET request for application {appName} from exporter {keyId} ({exporterDN}) failed due to unexpected exception.",
					appName, exporterKeyId, exporterDN);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}

		/// <summary>
		/// Implements <c>GET api/analytics/log/v2/{id:Guid}/content</c>, which retrieves the content for a specific analytics log.
		/// The response body is the raw byte stream which is encrypted as described by the <see cref="LogMetadataDTO.EncryptionInfo"/> of the metadata,
		/// which also contains the encrypted key material needed for decryption.
		/// </summary>
		/// <param name="id">The id of the log of which to retrieve the content.</param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		/// <returns>A <see cref="UserMetadataDTO"/> for the user registration, or an error state.</returns>
		[ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/octet-stream")]
		[ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
		[HttpGet("{id:Guid}/content")]
		public async Task<ActionResult> GetLogContentById([FromRoute] Guid id, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN, nameof(GetLogContentById));
			if (credResult != null) return credResult;
			try {
				logger.LogInformation("Serving content of log {logId} in application {appName} for exporter {exporterKeyId} ({exporterDN}).",
				id, appName, exporterKeyId, exporterDN);
				var log = await logManager.GetLogByIdAsync(id, appName, null, exporterDN, ct);
				var content = await log.OpenReadAsync(ct);
				return File(content, "application/octet-stream", log.Id.ToString() + log.FilenameSuffix, enableRangeProcessing: true);
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (ApplicationDoesNotExistException ex) {
				logger.LogError(ex, "GetLogContentById GET request for non-existent application {appName} from exporter {keyId} ({exporterDN}).", appName, exporterKeyId, exporterDN);
				metrics.HandleUnknownAppError(appName);
				return NotFound($"Application {appName} not found.");
			}
			catch (LogNotFoundException ex) {
				logger.LogError(ex, "GetLogContentById GET request for application {appName} from exporter {keyId} ({exporterDN}) failed because the requested log {logId} was not found.",
					appName, exporterKeyId, exporterDN, id);
				metrics.HandleLogNotFoundError(appName);
				return NotFound($"Log {id} not found in the application {appName}.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "GetLogContentById GET request for application {appName} from exporter {keyId} ({exporterDN}) failed due to unexpected exception.",
					appName, exporterKeyId, exporterDN);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}
	}
}
