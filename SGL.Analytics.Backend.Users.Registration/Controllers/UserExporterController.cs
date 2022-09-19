using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Registration.Controllers {
	[Route("api/analytics/user/v1")]
	[ApiController]
	[Authorize(Policy = "ExporterUser")]
	public class UserExporterController : ControllerBase {
		[HttpGet()]
		public Task<ActionResult<IEnumerable<Guid>>> GetUserIdList(CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		[HttpGet("all")]
		public Task<ActionResult<IEnumerable<UserMetadataDTO>>> GetMetadataForAllUsers([FromQuery] KeyId? recipient = null, CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		[HttpGet("{id:Guid}")]
		public Task<ActionResult<UserMetadataDTO>> GetUserMetadataById(Guid id, [FromQuery] KeyId? recipient = null) {
			throw new NotImplementedException();
		}
	}
}
