using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Prometheus;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Registration.Controllers {
	/// <summary>
	/// Implements the API routes for the key-pair-based challenge authentication for exporter clients.
	/// These routes are prefixed under <c>api/analytics/user/v1/exporter-key-auth</c>.
	/// The protocol consists of these steps:
	/// <list type="number">
	/// <item><description>The client calls <c>api/analytics/user/v1/exporter-key-auth/open-challenge</c> with their key id and the app to authenticate for.</description></item>
	/// <item><description>The backend responds to this call with a challenge containing a unique id, a nonce value and the algorithm to use.</description></item>
	/// <item><description>
	/// The client signs a byte sequence formed from the challenge nonce and a few parameters using their private key.
	/// See <see cref="ExporterKeyAuthSignatureDTO.ConstructContentToSign(ExporterKeyAuthRequestDTO, ExporterKeyAuthChallengeDTO)"/> for details.
	/// </description></item>
	/// <item><description>The client calls <c>api/analytics/user/v1/exporter-key-auth/complete-challenge</c> with the challenge id and the signature.</description></item>
	/// <item><description>The backend validates the signature against the known public key and if successful responds by issuing a session token.</description></item>
	/// </list>
	/// </summary>
	[Route("api/analytics/user/v1/exporter-key-auth")]
	[ApiController]
	public class ExporterKeyAuthController : ControllerBase {
		private readonly ILogger<ExporterKeyAuthController> logger;
		private readonly IKeyAuthManager keyAuthManager;
		private readonly IMetricsManager metrics;

		/// <summary>
		/// Instantiates the controller, injecting the required dependency objects.
		/// </summary>
		public ExporterKeyAuthController(ILogger<ExporterKeyAuthController> logger, IKeyAuthManager keyAuthManager, IMetricsManager metrics) {
			this.logger = logger;
			this.keyAuthManager = keyAuthManager;
			this.metrics = metrics;
		}

		/// <summary>
		/// Called by the client to open a challenge.
		/// A challenge with a random nonce byte sequence is generated, remembered by the server, and served to the client.
		/// </summary>
		/// <param name="requestDto">The request data for opening the challenge.</param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		/// <returns>A <see cref="ExporterKeyAuthChallengeDTO"/> containing the challenge data or an error state.</returns>
		[ProducesResponseType(typeof(ExporterKeyAuthChallengeDTO), StatusCodes.Status201Created)]
		[HttpPost("open-challenge")]
		public async Task<ActionResult<ExporterKeyAuthChallengeDTO>> OpenChallenge(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct = default) {
			try {
				var challenge = await keyAuthManager.OpenChallengeAsync(requestDto, ct);
				return StatusCode(StatusCodes.Status201Created, challenge);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("OpenChallenge POST request for app {appName} and key id {keyId} was cancelled.", requestDto.AppName, requestDto.KeyId);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "OpenChallenge POST request for app {appName} and key id {keyId} failed due to unexpected exception.", requestDto.AppName, requestDto.KeyId);
				metrics.HandleUnexpectedError(requestDto.AppName, ex);
				throw;
			}
		}
		/// <summary>
		/// Called by the client to complete a previously posed challenge.
		/// If the supplied signature is valid, a session token is issued to the client.
		/// </summary>
		/// <param name="signatureDto">Contains the id of the challenge to complete and the signature for which the client was challenged.</param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		/// <returns>A <see cref="ExporterKeyAuthResponseDTO"/> containing the session token or an error state.</returns>
		[ProducesResponseType(typeof(ExporterKeyAuthResponseDTO), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(string), StatusCodes.Status410Gone)]
		[HttpPost("complete-challenge")]
		public async Task<ActionResult<ExporterKeyAuthResponseDTO>> CompleteChallenge(ExporterKeyAuthSignatureDTO signatureDto, CancellationToken ct = default) {
			try {
				var response = await keyAuthManager.CompleteChallengeAsync(signatureDto, ct);
				return StatusCode(StatusCodes.Status200OK, response);
			}
			catch (InvalidChallengeException ex) {
				// TODO: metrics
				return StatusCode(StatusCodes.Status410Gone, ex.Message);
			}
			catch (ApplicationDoesNotExistException ex) {
				metrics.HandleUnknownAppError(ex.AppName);
				return NotFound(ex.Message);
			}
			catch (NoCertificateForKeyIdException ex) {
				// TODO: metrics
				return NotFound(ex.Message);
			}
			catch (CertificateException ex) {
				// TODO: metrics
				return StatusCode(StatusCodes.Status500InternalServerError, "There was a problem with the configured exporter certificate.");
			}
			catch (ChallengeCompletionFailedException ex) {
				// TODO: metrics
				return StatusCode(StatusCodes.Status401Unauthorized, "Challenge failed.");
			}
			catch (OperationCanceledException) {
				logger.LogDebug("CompleteChallenge POST request for challenge {id} was cancelled while.", signatureDto.ChallengeId);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "CompleteChallenge POST request for challenge {id} failed due to unexpected exception.", signatureDto.ChallengeId);
				metrics.HandleUnexpectedError("", ex);
				throw;
			}
		}
	}
}
