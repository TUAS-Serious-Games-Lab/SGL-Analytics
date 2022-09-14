using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Threading;

namespace SGL.Analytics.Backend.Users.Registration.Controllers {
	[Route("api/analytics/user/v1/exporter-key-auth")]
	[ApiController]
	public class ExporterKeyAuthController : ControllerBase {
		[HttpPost("challenge")]
		public ActionResult<ExporterKeyAuthChallengeDTO> ObtainChallenge(ExporterKeyAuthRequestDTO requestDto, CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		[HttpPost("complete")]
		public ActionResult<ExporterKeyAuthResponseDTO> CompleteChallenge(ExporterKeyAuthSignatureDTO signatureDto, CancellationToken ct = default) {
			throw new NotImplementedException();
		}
	}
}
