using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Prometheus;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend.AspNetCore;
using SGL.Utilities.Backend.Security;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Registration.Controllers {
	/// <summary>
	/// The controller class serving the <c>api/analytics/user</c> and <c>api/analytics/user/login</c> routes that manage user registrations for SGL Analytics and perform logins for user sessions.
	/// </summary>
	[Route("api/analytics/user")]
	[ApiController]
	public class AnalyticsUserController : ControllerBase {
		private static readonly Counter loginCounter = Metrics.CreateCounter("sgla_logins_total", "Number of successful logins performed by SGL Analytics, labeled by app.", "app");
		private static readonly Gauge lastSuccessfulLoginTime = Metrics.CreateGauge("sgla_last_successful_login_time_seconds", "Unix timestamp of the last successful login for the labeled app (in UTC).", "app");
		private static readonly Counter errorCounter = Metrics.CreateCounter("sgla_errors_total", "Number of service-level errors encountered by SGL Analytics, labeled by error type and app.", "type", "app");
		private const string ERROR_NONEXISTENT_USERNAME = "Nonexistent username";
		private const string ERROR_NONEXISTENT_USERID = "Nonexistent username";
		private const string ERROR_INCORRECT_USER_SECRET = "Incorrect user secret";
		private const string ERROR_UNKNOWN_APP = "Unknown app";
		private const string ERROR_INCORRECT_APP_API_TOKEN = "Incorrect app API token";
		private const string ERROR_USERID_APP_MISMATCH = "Userid does not belong to app";
		private const string ERROR_USER_PROP_VALIDATION_FAILED = "User property validation failed";
		private const string ERROR_CONCURRENCY_CONFLICT = "Concurrency conflict";
		private const string ERROR_USERNAME_ALREADY_TAKEN = "Username is already taken";

		private readonly IUserManager userManager;
		private readonly IApplicationRepository appRepo;
		private readonly ILogger<AnalyticsUserController> logger;
		private readonly ILoginService loginService;

		/// <summary>
		/// Instantiates the controller, injecting the required dependency objects.
		/// </summary>
		public AnalyticsUserController(IUserManager userManager, ILoginService loginService, IApplicationRepository appRepo, ILogger<AnalyticsUserController> logger) {
			this.userManager = userManager;
			this.loginService = loginService;
			this.appRepo = appRepo;
			this.logger = logger;
		}

		// POST: /api/analytics/user
		// To protect from overposting attacks, enable the specific properties you want to bind to, for
		// more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
		/// <summary>
		/// Handles POST requests to <c>api/analytics/user</c> for user registrations.
		/// The controller responds with a <see cref="UserRegistrationResultDTO"/> in JSON form, containing the assigned user id, and a <see cref="StatusCodes.Status201Created"/> upon sucess.
		/// The client needs to use this id wenn logging in using <see cref="Login(LoginRequestDTO, CancellationToken)"/>.
		/// If the <see cref="UserRegistrationDTO.StudySpecificProperties"/> contains invalid properties, the controller responds with a <see cref="StatusCodes.Status400BadRequest"/>.
		/// If the <see cref="UserRegistrationDTO.Username"/> is already in use, the controller responds with a <see cref="StatusCodes.Status409Conflict"/>.
		/// If the <see cref="UserRegistrationDTO.AppName"/> or <paramref name="appApiToken"/> don't match or are otherwise invalid, the controller responds with a <see cref="StatusCodes.Status401Unauthorized"/>.
		/// Other errors are represented by responding with a <see cref="StatusCodes.Status500InternalServerError"/>.
		/// </summary>
		/// <param name="appApiToken">The API token of the client application, provided by the HTTP header <c>App-API-Token</c>.</param>
		/// <param name="userRegistration">The data transfer object describing the user registration data, provided through the request body in JSON form.</param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		[ProducesResponseType(typeof(UserRegistrationResultDTO), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
		[ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
		[HttpPost]
		public async Task<ActionResult<UserRegistrationResultDTO>> RegisterUser([FromHeader(Name = "App-API-Token")] string appApiToken,
			[FromBody] UserRegistrationDTO userRegistration, CancellationToken ct = default) {
			using var appScope = logger.BeginApplicationScope(userRegistration.AppName);
			ApplicationWithUserProperties? app = null;
			try {
				app = await appRepo.GetApplicationByNameAsync(userRegistration.AppName, ct);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("RegisterUser POST request for user {username} was cancelled while fetching application metadata.", userRegistration.Username);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "RegisterUser POST request for user {username} failed due to an unexpected exception.", userRegistration.Username);
				errorCounter.WithLabels(ex.GetType().FullName ?? "Unknown", userRegistration.AppName).Inc();
				throw;
			}
			var appCredentialsErrorMessage = "The registration failed due to invalid application credentials.\n" +
					"One of the following was incorrect: AppName, AppApiToken\n" +
					"Which of these is / are incorrect is not stated for security reasons.";
			if (app is null) {
				logger.LogError("RegisterUser POST request for user {username} failed due to unknown application {appName}.", userRegistration.Username, userRegistration.AppName);
				errorCounter.WithLabels(ERROR_UNKNOWN_APP, userRegistration.AppName).Inc();
				return Unauthorized(appCredentialsErrorMessage);
			}
			else if (app.ApiToken != appApiToken) {
				logger.LogError("RegisterUser POST request for user {username} failed due to incorrect API token for application {appName}.", userRegistration.Username, userRegistration.AppName);
				errorCounter.WithLabels(ERROR_INCORRECT_APP_API_TOKEN, userRegistration.AppName).Inc();
				return Unauthorized(appCredentialsErrorMessage);
			}

			try {
				var user = await userManager.RegisterUserAsync(userRegistration, ct);
				var result = user.AsRegistrationResult();
				using var userScope = logger.BeginUserScope(user.Id);
				logger.LogInformation("Successfully registered user {username} with id {userid} for application {appName}", user.Username, user.Id, user.App.Name);
				return StatusCode(StatusCodes.Status201Created, result);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("RegisterUser POST request for user {username} was cancelled while registering the user.", userRegistration.Username);
				throw;
			}
			catch (ConcurrencyConflictException ex) {
				logger.LogError(ex, "RegisterUser POST request failed due to DB concurrency conflict.");
				// This should normally be a 409 - Conflict, but as 409 is specifically needed on this route to indicate a unique-violation for the username, we need to map to a different error.
				// Thus, rethrow to report a 500 - Internal Server Error, because the concurrency should not be a concern for users here anyway.
				// But we still use this separate catch becaue we want a separate log statement.
				errorCounter.WithLabels(ERROR_CONCURRENCY_CONFLICT, userRegistration.AppName).Inc();
				throw;
			}
			catch (EntityUniquenessConflictException ex) when (ex.ConflictingPropertyName == nameof(UserRegistration.Username)) {
				logger.LogInformation(ex, "RegisterUser POST request failed because the username {username} is already taken.", userRegistration.Username);
				errorCounter.WithLabels(ERROR_USERNAME_ALREADY_TAKEN, userRegistration.AppName).Inc();
				return Conflict("The requested username is already taken.");
			}
			catch (EntityUniquenessConflictException ex) {
				// The other source of EntityUniquenessConflictExceptions would be a conflict of the user id, which is extremely unlikely (128-bit Guid collision).
				// Let that case go to the 500 - ISE handler, triggering the client to retry later.
				// But before that, log the error.
				logger.LogError(ex, "Conflicting user id {id} detected during registration.", ex.ConflictingPropertyValue);
				errorCounter.WithLabels(ERROR_CONCURRENCY_CONFLICT, userRegistration.AppName).Inc();
				throw;
			}
			catch (UserPropertyValidationException ex) {
				logger.LogError(ex, "The validation of app-specific properties failed while attempting to register user {username}.", userRegistration.Username);
				errorCounter.WithLabels(ERROR_USER_PROP_VALIDATION_FAILED, userRegistration.AppName).Inc();
				return BadRequest(ex.Message);
			}
			catch (Exception ex) {
				logger.LogError(ex, "RegisterUser POST request for user {username} failed due to unexpected exception.", userRegistration.Username);
				errorCounter.WithLabels(ex.GetType().FullName ?? "Unknown", userRegistration.AppName).Inc();
				throw;
			}
		}

		/// <summary>
		/// Handles POST requests to <c>api/analytics/user/login</c> for user logins to start a session.
		/// Upon success, the controller responds with a JSON-encoded <see cref="LoginResponseDTO"/>, containing a session token that can be used to
		/// authenticate requests to SGL Analytics services as the logged-in user, and a <see cref="StatusCodes.Status200OK"/>.
		/// If the login fails because any of the credentials are incorrect or the credentials don't match, the controller responds with a <see cref="StatusCodes.Status401Unauthorized"/>.
		/// A further distinction which part of the credentials was incorrect is not made for security reasons.
		/// Other errors are represented by responding with a <see cref="StatusCodes.Status500InternalServerError"/>.
		/// </summary>
		/// <param name="loginRequest">
		/// A data transfer object, containing the credentials to use for the login attempt.
		/// The controller supports the following login request types: <see cref="IdBasedLoginRequestDTO"/>, <see cref="UsernameBasedLoginRequestDTO"/>
		/// </param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		[ProducesResponseType(typeof(LoginResponseDTO), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
		[HttpPost("login")]
		public async Task<ActionResult<LoginResponseDTO>> Login([FromBody] LoginRequestDTO loginRequest, CancellationToken ct = default) {
			using var appScope = logger.BeginApplicationScope(loginRequest.AppName);
			try {
				using var userScope = logger.BeginUserScope(loginRequest.GetUserIdentifier());
				var app = await appRepo.GetApplicationByNameAsync(loginRequest.AppName, ct);
				var fixedFailureDelay = loginService.StartFixedFailureDelay(ct);
				User? user = null; // stash a reference to user to check app association later, and for username-based login between id-lookup and actual login.
				Guid userid;
				Func<Guid, Task<User?>> getUser;
				if (loginRequest is IdBasedLoginRequestDTO idBased) {
					userid = idBased.UserId;
					getUser = async uid => {
						user = await userManager.GetUserByIdAsync(userid, ct);
						if (user == null) {
							errorCounter.WithLabels(ERROR_NONEXISTENT_USERID, loginRequest.AppName).Inc();
						}
						return user;
					};
				}
				else if (loginRequest is UsernameBasedLoginRequestDTO usernameBased) {
					user = await userManager.GetUserByUsernameAndAppNameAsync(usernameBased.Username, usernameBased.AppName, ct);
					if (user != null) {
						userid = user.Id;
						getUser = uid => {
							if (user.Id != uid) {
								logger.LogError("Unexpected userid mismatch between id lookup and login. Expected {expected}, got {actual}", user.Id, uid);
								return Task.FromResult<User?>(null);
							}
							return Task.FromResult<User?>(user);
						};
					}
					else {
						logger.LogError("Login attempt for username {username} in application {appName} failed because no such user could be found.", usernameBased.Username, usernameBased.AppName);
						errorCounter.WithLabels(ERROR_NONEXISTENT_USERNAME, loginRequest.AppName).Inc();
						// Ensure failure of this incorrect login attempt:
						app = null;
						userid = Guid.Empty;
						getUser = uid => Task.FromResult<User?>(null);
					}
				}
				else {
					logger.LogError("Login attempt with unsupported LoginRequestDTO subtype {type}.", loginRequest.GetType().FullName);
					return BadRequest("Unsupported login credentials type");
				}
				var token = await loginService.LoginAsync(userid, loginRequest.UserSecret,
					getUser,
					user => user.HashedSecret,
					async (user, hashedSecret) => {
						user.HashedSecret = hashedSecret;
						await userManager.UpdateUserAsync(user, ct);
					},
					fixedFailureDelay!, ct,
					("appname", u => u.App.Name));

				if (token == null) {
					logger.LogError("Login attempt for user {userId} failed due to incorrect credentials.", loginRequest.GetUserIdentifier());
					if (user != null) {
						errorCounter.WithLabels(ERROR_INCORRECT_USER_SECRET, loginRequest.AppName).Inc();
					}
				}
				// Intentionally no else if here, to log both failures if both, the app credentials AND the user credentials are invalid.
				if (app == null) {
					logger.LogError("Login attempt for user {userId} failed due to unknown application {appName}.", loginRequest.GetUserIdentifier(), loginRequest.AppName);
					errorCounter.WithLabels(ERROR_UNKNOWN_APP, loginRequest.AppName).Inc();
				}
				else if (app.ApiToken != loginRequest.AppApiToken) {
					app = null; // Clear non-matching app to simplify controll flow below.
					logger.LogError("Login attempt for user {userId} failed due to incorrect API token for application {appName}.", loginRequest.GetUserIdentifier(), loginRequest.AppName);
					errorCounter.WithLabels(ERROR_INCORRECT_APP_API_TOKEN, loginRequest.AppName).Inc();
				}
				if (user != null && (user.App.Name != loginRequest.AppName)) {
					logger.LogError("Login attempt for user {userId} failed because the retrieved user is not associated with the given application {reqAppName}, but with {userAppName}.", loginRequest.GetUserIdentifier(), loginRequest.AppName, user.App.Name);
					errorCounter.WithLabels(ERROR_USERID_APP_MISMATCH, loginRequest.AppName).Inc();
					// Ensure failure, irrespective of what happened with app and user credential checks above.
					token = null;
					app = null;
				}

				if (app == null || token == null) {
					await fixedFailureDelay.WaitAsync(); // If the LoginAsync failed, this is already completed, but await it in case of a failure from app credentials.
					return Unauthorized("Login failed due to invalid credentials.\n" +
						"One of the following was incorrect: AppName, AppApiToken, UserId, UserSecret\n" +
						"Which of these is / are incorrect is not stated for security reasons.");
				}
				else {
					loginCounter.WithLabels(loginRequest.AppName).Inc();
					lastSuccessfulLoginTime.WithLabels(loginRequest.AppName).IncToCurrentTimeUtc();
					return new LoginResponseDTO(new AuthorizationToken(token));
				}
			}
			catch (OperationCanceledException) {
				logger.LogDebug("Login attempt for user {userId} was cancelled.", loginRequest.GetUserIdentifier());
				throw;
			}
		}
	}
}
