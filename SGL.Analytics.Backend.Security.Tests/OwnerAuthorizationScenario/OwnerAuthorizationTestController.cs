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
		[Authorize(policy: "RouteOwnerUserId")]
		public ActionResult User1([FromRoute] Guid userId) => Ok(userId);

		[HttpGet("user2/{userid:guid}")]
		[Authorize(policy: "RouteOwnerUserId")]
		public ActionResult User2([FromRoute] Guid userid) => Ok(userid);

		[HttpGet("user3/{UserId:guid}")]
		[Authorize(policy: "RouteOwnerUserId")]
		public ActionResult User3([FromRoute] Guid UserId) => Ok(UserId);

		[HttpGet("user4/{UserID:guid}")]
		[Authorize(policy: "RouteOwnerUserId")]
		public ActionResult User4([FromRoute] Guid UserID) => Ok(UserID);

		[HttpGet("other1/{other:guid}")]
		[Authorize(policy: "RouteOwnerUserId")]
		public ActionResult Other1([FromRoute] Guid other) => Ok(other);


		[HttpGet("user5")]
		[Authorize(policy: "HeaderOwnerUserId")]
		public ActionResult User5([FromHeader] Guid userId) => Ok(userId);

		[HttpGet("user6")]
		[Authorize(policy: "HeaderOwnerUserId")]
		public ActionResult User6([FromHeader] Guid userid) => Ok(userid);

		[HttpGet("user7")]
		[Authorize(policy: "HeaderOwnerUserId")]
		public ActionResult User7([FromHeader] Guid UserId) => Ok(UserId);

		[HttpGet("user8")]
		[Authorize(policy: "HeaderOwnerUserId")]
		public ActionResult User8([FromHeader] Guid UserID) => Ok(UserID);

		[HttpGet("other2")]
		[Authorize(policy: "HeaderOwnerUserId")]
		public ActionResult Other2([FromHeader] Guid other) => Ok(other);
	}
}
