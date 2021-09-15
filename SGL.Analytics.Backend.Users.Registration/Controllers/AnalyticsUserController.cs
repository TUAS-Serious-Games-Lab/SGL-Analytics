using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Security;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.DTO;

namespace SGL.Analytics.Backend.Users.Registration.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class AnalyticsUserController : ControllerBase {
		private readonly IUserManager userManager;
		private readonly ILoginService loginService;

		public AnalyticsUserController(IUserManager userManager, ILoginService loginService) {
			this.userManager = userManager;
			this.loginService = loginService;
		}

		// POST: api/AnalyticsUser
		// To protect from overposting attacks, enable the specific properties you want to bind to, for
		// more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
		[HttpPost]
		public async Task<ActionResult<UserRegistrationResultDTO>> PostUserRegistration(UserRegistrationDTO userRegistration) {
			// TODO: Check API token.
			return Unauthorized();

			var user = await userManager.RegisterUserAsync(userRegistration);
			var result = user.AsRegistrationResult();

			return StatusCode(StatusCodes.Status201Created, result);
		}

		[ProducesResponseType(typeof(LoginResponseDTO), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[HttpPost("login")]
		public async Task<ActionResult<LoginResponseDTO>> Login([FromBody] LoginRequestDTO loginRequest) {
			var token = await loginService.LoginAsync(loginRequest.UserId, loginRequest.UserSecret,
				userId => userManager.GetUserById(userId),
				user => user.HashedSecret,
				async (user, hashedSecret) => {
					user.HashedSecret = hashedSecret;
					await userManager.UpdateUserAsync(user);
				});
			if (token is null) {
				return StatusCode(StatusCodes.Status403Forbidden, "Login failed: The given user id or secret was invalid.");
			}
			else {
				return new LoginResponseDTO(token);
			}
		}
	}
}
