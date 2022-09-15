using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Threading;

namespace SGL.Analytics.Backend.Users.Registration.Controllers {
	[Route("api/analytics/user/v1/exporter-key-auth")]
	[ApiController]
	public class ExporterKeyAuthController : ControllerBase {
		private readonly ILogger<ExporterKeyAuthController> logger;
		private readonly IKeyAuthManager keyAuthManager;

		public ExporterKeyAuthController(ILogger<ExporterKeyAuthController> logger, IKeyAuthManager keyAuthManager) {
			this.logger = logger;
			this.keyAuthManager = keyAuthManager;
		}

		[HttpPost("open-challenge")]
		public ActionResult<ExporterKeyAuthChallengeDTO> OpenChallenge(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct = default) {
			// TODO: Exception handling, logging, metrics
			var challenge = keyAuthManager.OpenChallengeAsync(requestDto, ct);
			return challenge;
		}

		[HttpPost("complete-challenge")]
		public ActionResult<ExporterKeyAuthResponseDTO> CompleteChallenge(ExporterKeyAuthSignatureDTO signatureDto, CancellationToken ct = default) {
			// TODO: Exception handling, logging, metrics
			var response = keyAuthManager.CompleteChallengeAsync(signatureDto, ct);
			return response;
		}
	}
}
