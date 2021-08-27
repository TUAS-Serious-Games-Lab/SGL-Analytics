using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.DTO;

namespace SGL.Analytics.Backend.UserDB.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class AnalyticsUserController : ControllerBase {
		private readonly IUserManager userManager;

		public AnalyticsUserController(IUserManager userManager) {
			this.userManager = userManager;
		}

		// POST: api/AnalyticsUser
		// To protect from overposting attacks, enable the specific properties you want to bind to, for
		// more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
		[HttpPost]
		public async Task<ActionResult<UserRegistrationResultDTO>> PostUserRegistration(UserRegistrationDTO userRegistration) {
			UserRegistrationResultDTO result = await userManager.RegisterUserAsync(userRegistration);

			return StatusCode(((int)HttpStatusCode.Created));
		}
	}
}
