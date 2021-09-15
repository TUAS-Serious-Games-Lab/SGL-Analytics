using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Security;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.DTO;
using SGL.Analytics.Backend.Users.Application.Model;

namespace SGL.Analytics.Backend.Users.Registration.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class AnalyticsUserController : ControllerBase {
		private readonly IUserManager userManager;
		private readonly IApplicationRepository appRepo;
		private readonly ILogger<AnalyticsUserController> logger;
		private readonly ILoginService loginService;

		public AnalyticsUserController(IUserManager userManager, ILoginService loginService, IApplicationRepository appRepo, ILogger<AnalyticsUserController> logger) {
			this.userManager = userManager;
			this.loginService = loginService;
			this.appRepo = appRepo;
			this.logger = logger;
		}

		// POST: api/AnalyticsUser
		// To protect from overposting attacks, enable the specific properties you want to bind to, for
		// more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
		[HttpPost]
		public async Task<ActionResult<UserRegistrationResultDTO>> RegisterUser([FromHeader(Name = "App-API-Token")] string appApiToken, [FromBody] UserRegistrationDTO userRegistration) {
			var app = await appRepo.GetApplicationByNameAsync(userRegistration.AppName);
			if (app is null) {
				logger.LogError("RegisterUser POST request for user {username} failed due to unknown application {appName}.", userRegistration.Username, userRegistration.AppName);
				return Unauthorized();
			}
			else if (app.ApiToken != appApiToken) {
				logger.LogError("RegisterUser POST request for user {username} failed due to incorrect API token for application {appName}.", userRegistration.Username, userRegistration.AppName);
				return Unauthorized();
			}

			try {
				var user = await userManager.RegisterUserAsync(userRegistration);
				var result = user.AsRegistrationResult();
				return StatusCode(StatusCodes.Status201Created, result);
			}
			catch (ConcurrencyConflictException ex) {
				logger.LogError(ex, "RegisterUser POST request failed due to DB concurrency conflict.");
				// This should normally be a 409 - Conflict, but as 409 is specifically needed on this route to indicate a unique-violation for the username, we need to map to a different error.
				// Thus, rethrow to report a 500 - Internal Server Error, because the concurrency should not be a concern for users here anyway.
				// But we still use this separate catch becaue we want a separate log statement.
				throw;
			}
			catch (EntityUniquenessConflictException ex) when (ex.ConflictingPropertyName == nameof(UserRegistration.Username)) {
				logger.LogInformation(ex, "RegisterUser POST request failed because the username {username} is already taken.", userRegistration.Username);
				return Conflict("The requested username is already taken.");
			}
			catch (EntityUniquenessConflictException ex) {
				// The other source of EntityUniquenessConflictExceptions would be a conflict of the user id, which is extremely unlikely (128-bit Guid collision).
				// Let that case go to the 500 - ISE handler, triggering the client to retry later.
				// But before that, log the error.
				logger.LogError(ex, "Conflicting user id {id} detected during registration.", ex.ConflictingPropertyValue);
				throw;
			}
			catch (UserPropertyValidationException ex) {
				logger.LogError(ex, "The validation of app-specific properties failed while attempting to register user {username}.", userRegistration.Username);
				return BadRequest(ex.Message);
			}
			catch (Exception ex) {
				logger.LogError(ex, "RegisterUser POST request for user {username} failed due to unexpected exception.", userRegistration.Username);
				throw;
			}
		}

		[ProducesResponseType(typeof(LoginResponseDTO), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[HttpPost("login")]
		public async Task<ActionResult<LoginResponseDTO>> Login([FromBody] LoginRequestDTO loginRequest) {
			var app = await appRepo.GetApplicationByNameAsync(loginRequest.AppName);
			var fixedFailureDelay = loginService.StartFixedFailureDelay();
			User? user = null;
			var token = await loginService.LoginAsync(loginRequest.UserId, loginRequest.UserSecret,
				async userId => (user = await userManager.GetUserByIdAsync(userId)), // stash a reference to user to check app association later
				user => user.HashedSecret,
				async (user, hashedSecret) => {
					user.HashedSecret = hashedSecret;
					await userManager.UpdateUserAsync(user);
				}, fixedFailureDelay);

			if (token is null) {
				logger.LogError("Login attempt for user {userId} failed due to incorrect credentials.", loginRequest.UserId);
			}
			// Intentionally no else if here, to log both failures if both, the app credentials AND the user credentials are invalid.
			if (app is null) {
				logger.LogError("Login attempt for user {userId} failed due to unknown application {appName}.", loginRequest.UserId, loginRequest.AppName);
			}
			else if (app.ApiToken != loginRequest.AppApiToken) {
				app = null; // Clear non-matching app to simplify controll flow below.
				logger.LogError("Login attempt for user {userId} failed due to incorrect API token for application {appName}.", loginRequest.UserId, loginRequest.AppName);
			}
			if (user is not null && (user.App.Name != loginRequest.AppName)) {
				logger.LogError("Login attempt for user {userId} failed because the retrieved user is not associated with the given application {reqAppName}, but with {userAppName}.", loginRequest.UserId, loginRequest.AppName, user.App.Name);
				// Ensure failure, irrespective of what happened with app and user credential checks above.
				token = null;
				app = null;
			}

			if (app is null || token is null) {
				await fixedFailureDelay.WaitAsync(); // If the LoginAsync failed, this is already completed, but await it in case of a failure from app credentials.
				return Unauthorized("Login failed due to invalid credentials.\n" +
					"One of the following was incorrect: AppName, AppApiToken, UserId, UserSecret\n" +
					"Which of these is / are incorrect is not stated for security reasons.");
			}
			else {
				return new LoginResponseDTO(token);
			}
		}
	}
}
