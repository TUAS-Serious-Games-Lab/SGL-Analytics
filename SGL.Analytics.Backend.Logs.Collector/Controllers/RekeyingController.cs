using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Prometheus;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Services;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector.Controllers {
	/// <summary>
	/// Implements the API routes for rekeying data keys for game analytics logs to grant access to a new authorized recipient.
	/// The client iteratively requests key material for rekeying, decrypts the data keys using the user's recipient key pair,
	/// reencrypts them using the new authorized recipient's public key and then uploads the new data keys to be added to the database.
	/// In the next request, for key material, the game analytics logs for which data keys were successfully added are excluded and
	/// a new set of key material is provided.
	/// Logs that could not be successfully rekeyed are skipped using the pagination offset.
	/// This iteration continues until a request for further key material returns an empty response.
	/// These routes are prefixed under <c>api/analytics/log/v2/rekey</c>.
	/// All routes here require an authorization that satisfies the <c>ExporterUser</c> policy.
	/// </summary>
	[Route("api/analytics/log/v2/rekey")]
	[ApiController]
	[Authorize(Policy = "ExporterUser")]
	public class RekeyingController : ControllerBase {
		private readonly ILogManager logManager;
		private readonly ILogger<RekeyingController> logger;
		private readonly IMetricsManager metrics;

		/// <summary>
		/// Instantiates the controller, injecting the required dependency objects.
		/// </summary>
		public RekeyingController(ILogManager logManager, ILogger<RekeyingController> logger, IMetricsManager metrics) {
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

		/// <summary>
		/// Implements <c>GET api/analytics/log/v2/rekey/{recipientKeyId}</c>, which retrieves a dictionary for a chunk of game analytics logs
		/// that maps the user id to the <see cref="EncryptionInfo"/> for the encrypted log content.
		/// The returned data contains the encrypted data keys for the recipient key with the key id indicated by <paramref name="recipientKeyId"/>.
		/// As the requested data is intended for the client to rekey it for a different recipient key-pair,
		/// the data is filtered to only contain logs for which there is not already a data key present for the target recipient indicated by <paramref name="targetKeyId"/>.
		/// Additionally, pagination is supported using <paramref name="offset"/> and the item count configured in <see cref="LogManagerOptions.RekeyingPagination"/>.
		/// </summary>
		/// <param name="recipientKeyId">The key id for the recipient that has access and intends to grant access, i.e. the recipient key of the user making the request.</param>
		/// <param name="targetKeyId">
		/// The key id for the recipient that the client wants to grant access to.
		/// Logs for which this key already has access will be filtered out.
		/// Passed as query parameter <c>targetKeyId</c>.
		/// </param>
		/// <param name="offset">
		/// The number of logs (after filtering) to skip for pagination.
		/// The ordering is done by owning user id and then creation time.
		/// Passed as query parameter <c>offset</c>.
		/// </param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		/// <returns>A <see cref="Dictionary{Guid, EncryptionInfo}"/> containing the encryption metadata for rekeying, or an error state.</returns>
		[ProducesResponseType(typeof(Dictionary<Guid, EncryptionInfo>), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
		[HttpGet("{keyId}")]
		public async Task<ActionResult<Dictionary<Guid, EncryptionInfo>>> GetKeysForRekeying([FromRoute(Name = "keyId")] KeyId recipientKeyId,
				[FromQuery(Name = "targetKeyId")] KeyId targetKeyId, [FromQuery(Name = "offset")] int offset = 0, CancellationToken ct = default) {
			var credResult = GetCredentials(out var appName, out var exporterKeyId, out var exporterDN, nameof(GetKeysForRekeying));
			if (credResult != null) return credResult;
			try {
				logger.LogInformation("Listing key material for logs in application {appName} with recipient keys for {recipientKeyId} for rekeying by exporter {exporterKeyId} ({exporterDN}) to recipient key {targetKeyId} (pagination offset = {offset}).",
					appName, recipientKeyId, exporterKeyId, exporterDN, targetKeyId, offset);
				var result = await logManager.GetKeysForRekeying(appName, recipientKeyId, targetKeyId, exporterDN, offset, ct);
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

		/// <summary>
		/// Implements <c>PUT api/analytics/log/v2/rekey/{newRecipientKeyId}</c>,
		/// which stores data keys for the key-pair indicated by <paramref name="newRecipientKeyId"/>
		/// into the database after they were rekeyed / reencrypted by the client in order to grant access to that key-pair.
		/// </summary>
		/// <param name="newRecipientKeyId">
		/// The key id of the new recipient key-pair for which rekeyed data keys are provided.
		/// </param>
		/// <param name="dataKeys">
		///	The rekeyed data keys provided in the request body as a JSON dictionary that
		///	maps the log ids to the new <see cref="DataKeyInfo"/> that shall be added.
		/// </param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		/// <returns>An <see cref="ActionResult"/> indicating success or an error state.</returns>
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
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
			catch (ApplicationDoesNotExistException ex) {
				logger.LogError(ex, "PutRekeyedKeys PUT request for non-existent application {appName} from exporter {keyId} ({exporterDN}).", appName, exporterKeyId, exporterDN);
				metrics.HandleUnknownAppError(appName);
				return NotFound($"Application {appName} not found.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "PutRekeyedKeys PUT request for application {appName} from exporter {keyId} ({exporterDN}) failed due to unexpected exception.",
					appName, exporterKeyId, exporterDN);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}
	}
}
