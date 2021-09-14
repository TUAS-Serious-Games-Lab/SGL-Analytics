using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SGL.Analytics.Backend.Security.Tests.OwnerAuthorizationScenario {
	[Route("api/[controller]")]
	[ApiController]
	public class OwnerAuthorizationTestController : ControllerBase {
		private readonly ILogger<OwnerAuthorizationTestController> logger;

		public OwnerAuthorizationTestController(ILogger<OwnerAuthorizationTestController> logger) {
			this.logger = logger;
		}

		[HttpGet("user1/{userId:guid}")]
		[Authorize(policy: "Owner")]
		public ActionResult User1([FromRoute] Guid userId) => Ok(userId);

		[HttpGet("user2/{user:guid}")]
		[Authorize(policy: "Owner")]
		public ActionResult User2([FromRoute] Guid user) => Ok(user);

		[HttpGet("user3/{UserId:guid}")]
		[Authorize(policy: "Owner")]
		public ActionResult User3([FromRoute] Guid UserId) => Ok(UserId);

		[HttpGet("user4/{User:guid}")]
		[Authorize(policy: "Owner")]
		public ActionResult User4([FromRoute] Guid User) => Ok(User);


		[HttpGet("owner1/{ownerId:guid}")]
		[Authorize(policy: "Owner")]
		public ActionResult Owner1([FromRoute] Guid ownerId) => Ok(ownerId);

		[HttpGet("owner2/{owner:guid}")]
		[Authorize(policy: "Owner")]
		public ActionResult Owner2([FromRoute] Guid owner) => Ok(owner);

		[HttpGet("owner3/{OwnerId:guid}")]
		[Authorize(policy: "Owner")]
		public ActionResult Owner3([FromRoute] Guid OwnerId) => Ok(OwnerId);

		[HttpGet("owner4/{Owner:guid}")]
		[Authorize(policy: "Owner")]
		public ActionResult Owner4([FromRoute] Guid Owner) => Ok(Owner);

		[HttpGet("both/{ownerId:guid}/{userId:guid}")]
		[Authorize(policy: "Owner")]
		public ActionResult Both1([FromRoute] Guid ownerId, [FromRoute] Guid userId) => Ok(new { ownerId, userId });

		[HttpGet("other/{other:guid}")]
		[Authorize(policy: "Owner")]
		public ActionResult Other1([FromRoute] Guid other) => Ok(other);

	}
}
