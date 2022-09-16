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
	[Route("api/analytics/user/v1/exporter-key-auth")]
	[ApiController]
	public class ExporterKeyAuthController : ControllerBase {
		private readonly ILogger<ExporterKeyAuthController> logger;
		private readonly IKeyAuthManager keyAuthManager;
		private readonly IMetricsManager metrics;

		public ExporterKeyAuthController(ILogger<ExporterKeyAuthController> logger, IKeyAuthManager keyAuthManager, IMetricsManager metrics) {
			this.logger = logger;
			this.keyAuthManager = keyAuthManager;
			this.metrics = metrics;
		}

		[HttpPost("open-challenge")]
		public async Task<ActionResult<ExporterKeyAuthChallengeDTO>> OpenChallenge(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct = default) {
			try {
				var challenge = await keyAuthManager.OpenChallengeAsync(requestDto, ct);
				return StatusCode(StatusCodes.Status201Created, challenge);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("OpenChallenge POST request for app {appName} and key id {keyId} was cancelled while registering the user.", requestDto.AppName, requestDto.KeyId);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "OpenChallenge POST request for app {appName} and key id {keyId} failed due to unexpected exception.", requestDto.AppName, requestDto.KeyId);
				metrics.HandleUnexpectedError(requestDto.AppName, ex);
				throw;
			}
		}

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
				logger.LogDebug("CompleteChallenge POST request for challenge {id} was cancelled while registering the user.", signatureDto.ChallengeId);
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
