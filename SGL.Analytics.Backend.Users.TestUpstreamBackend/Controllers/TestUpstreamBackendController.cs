﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Backend.Security;

namespace SGL.Analytics.Backend.Users.TestUpstreamBackend.Controllers {
	[Route("api/analytics/test/upstream/v1")]
	[ApiController]
	public class TestUpstreamBackendController : ControllerBase {
		private readonly ILogger<TestUpstreamBackendController> logger;
		private readonly TestUpstreamBackendOptions options;
		private readonly IExplicitTokenService explicitTokenService;

		public TestUpstreamBackendController(IOptions<TestUpstreamBackendOptions> options, ILogger<TestUpstreamBackendController> logger) {
			this.options = options.Value;
			this.logger = logger;
		}

		[HttpPost("start-session")]
		public async Task<ActionResult<LoginResponseDTO>> StartSession([FromBody] string secret, CancellationToken ct = default) {
			if (string.IsNullOrWhiteSpace(secret) || secret.Length < 10) {
				return BadRequest("Bad secret.");
			}
			if (options.Secret != secret) {
				return Unauthorized("Incorrect secret.");
			}
			else {
				var fakeUserId = Guid.NewGuid();
				var token = explicitTokenService.IssueAuthenticationToken(("userid", $"{fakeUserId:D}"), ("appname", options.AppName));
				return new LoginResponseDTO(token.Token, fakeUserId, token.Expiry);
			}
		}

		[Authorize]
		[HttpPost("check-token")]
		public async Task<ActionResult<UpstreamTokenCheckResponse>> CheckToken([FromBody] UpstreamTokenCheckRequest request, CancellationToken ct = default) {
			var tokenUserId = HttpContext.User.GetClaim<Guid>("userid", Guid.TryParse);
			var tokenAppName = HttpContext.User.GetClaimOrNull("appname");
			var tokenExpiry = User.GetClaim("exp", (string s, out DateTime p) => {
				if (int.TryParse(s, out int timestamp)) {
					p = DateTime.UnixEpoch.AddSeconds(timestamp);
					return true;
				}
				p = default;
				return false;
			});
			if (options.AppName != tokenAppName) {
				return Unauthorized("Invalid token.");
			}
			if (tokenAppName != request.RequestingAppName) {
				return Unauthorized("Token doesn't match requesting app.");
			}
			return new UpstreamTokenCheckResponse(tokenUserId, tokenExpiry);
		}
	}
}
