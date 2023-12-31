using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Backend;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Backend.AspNetCore;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Registration.Controllers {
	/// <summary>
	/// The controller class serving the <c>api/analytics/user/v1</c>, <c>api/analytics/user/v1/login</c>, <c>api/analytics/user/v1/open-session-from-upstream</c>, and <c>api/analytics/user/v1/recipient-certificates</c>
	/// routes that manage user registrations for SGL Analytics, perform logins for user sessions and provide recipient certificates for end-to-end encryption.
	/// </summary>
	[Route("api/analytics/user/v1")]
	[ApiController]
	public class AnalyticsUserController : ControllerBase {
		private readonly IUserManager userManager;
		private readonly IApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions> appRepo;
		private readonly ILogger<AnalyticsUserController> logger;
		private readonly ILoginAuthenticationService loginService;
		private readonly IMetricsManager metrics;

		/// <summary>
		/// Instantiates the controller, injecting the required dependency objects.
		/// </summary>
		public AnalyticsUserController(IUserManager userManager, ILoginAuthenticationService loginService, IApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions> appRepo, ILogger<AnalyticsUserController> logger, IMetricsManager metrics) {
			this.userManager = userManager;
			this.loginService = loginService;
			this.appRepo = appRepo;
			this.logger = logger;
			this.metrics = metrics;
		}

		/// <summary>
		/// Handles registrations of new users.
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
		/// <returns>A <see cref="UserRegistrationResultDTO"/> containing the assigned user id, or an error state.</returns>
		[ProducesResponseType(typeof(UserRegistrationResultDTO), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
		[ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
		[HttpPost]
		public async Task<ActionResult<UserRegistrationResultDTO>> RegisterUser([FromHeader(Name = "App-API-Token")][StringLength(64, MinimumLength = 8)] string appApiToken,
			[FromBody] UserRegistrationDTO userRegistration, CancellationToken ct = default) {
			using var appScope = logger.BeginApplicationScope(userRegistration.AppName);
			ApplicationWithUserProperties? app = null;
			try {
				app = await appRepo.GetApplicationByNameAsync(userRegistration.AppName, ct: ct);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("RegisterUser POST request for user {username} was cancelled while fetching application metadata.", userRegistration.Username);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "RegisterUser POST request for user {username} failed due to an unexpected exception.", userRegistration.Username);
				metrics.HandleUnexpectedError(userRegistration.AppName, ex);
				throw;
			}
			var appCredentialsErrorMessage = "The registration failed due to invalid application credentials.\n" +
					"One of the following was incorrect: AppName, AppApiToken\n" +
					"Which of these is / are incorrect is not stated for security reasons.";
			if (app is null) {
				logger.LogError("RegisterUser POST request for user {username} failed due to unknown application {appName}.", userRegistration.Username, userRegistration.AppName);
				metrics.HandleUnknownAppError(userRegistration.AppName);
				return Unauthorized(appCredentialsErrorMessage);
			}
			else if (app.ApiToken != appApiToken) {
				logger.LogError("RegisterUser POST request for user {username} failed due to incorrect API token for application {appName}.", userRegistration.Username, userRegistration.AppName);
				metrics.HandleIncorrectAppApiTokenError(userRegistration.AppName);
				return Unauthorized(appCredentialsErrorMessage);
			}
			if (userRegistration.Secret == null) { // If we are registering an account with authentication delegation, pass on the provided auth header:
				if (userRegistration.UpstreamAuthorizationHeader == null) {
					return Unauthorized("The operation failed due to missing upstream authorization token.");
				}
			}
			try {
				var user = await userManager.RegisterUserAsync(userRegistration, ct);
				var result = user.AsRegistrationResult();
				using var userScope = logger.BeginUserScope(user.Id);
				logger.LogInformation("Successfully registered user {username} with id {userid} for application {appName}", user.Username, user.Id, user.App.Name);
				metrics.HandleSuccessfulRegistration(userRegistration.AppName);
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
				metrics.HandleConcurrencyConflictError(userRegistration.AppName);
				throw;
			}
			catch (EntityUniquenessConflictException ex) when (ex.ConflictingPropertyName == nameof(UserRegistration.Username)) {
				logger.LogInformation(ex, "RegisterUser POST request failed because the username {username} is already taken.", userRegistration.Username);
				metrics.HandleUsernameAlreadyTakenError(userRegistration.AppName);
				return Conflict("The requested username is already taken.");
			}
			catch (EntityUniquenessConflictException ex) when (ex.ConflictingPropertyName == nameof(UserRegistration.BasicFederationUpstreamUserId)) {
				logger.LogInformation(ex, "RegisterUser POST request failed because the upstream user id {upstreamUserId} is already registered.", ex.ConflictingPropertyValue);
				// TODO: metrics
				return Conflict("The provided upstream user id is already registered.");
			}
			catch (EntityUniquenessConflictException ex) {
				// The other source of EntityUniquenessConflictExceptions would be a conflict of the user id, which is extremely unlikely (128-bit Guid collision).
				// Let that case go to the 500 - ISE handler, triggering the client to retry later.
				// But before that, log the error.
				logger.LogError(ex, "Conflicting user id {id} detected during registration.", ex.ConflictingPropertyValue);
				metrics.HandleUniquenessConflictError(userRegistration.AppName);
				throw;
			}
			catch (UserPropertyValidationException ex) {
				logger.LogError(ex, "The validation of app-specific properties failed while attempting to register user {username}.", userRegistration.Username);
				metrics.HandleUserPropertyValidationError(userRegistration.AppName);
				return BadRequest(ex.Message);
			}
			catch (InvalidCryptographicMetadataException ex) {
				logger.LogError(ex, "RegisterUser POST request failed because the registration uses encrypted user properties and there was a problem with the associated cryptographic metadata.");
				metrics.HandleCryptoMetadataError(userRegistration.AppName);
				return BadRequest(ex.Message);
			}
			catch (Exception ex) {
				logger.LogError(ex, "RegisterUser POST request for user {username} failed due to unexpected exception.", userRegistration.Username);
				metrics.HandleUnexpectedError(userRegistration.AppName, ex);
				throw;
			}
		}

		/// <summary>
		/// Handles user logins to start a session.
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
		/// <returns>A <see cref="LoginResponseDTO"/> containing the session token and the id of the user, or an error state.</returns>
		[ProducesResponseType(typeof(LoginResponseDTO), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
		[HttpPost("login")]
		public async Task<ActionResult<LoginResponseDTO>> Login([FromBody] LoginRequestDTO loginRequest, CancellationToken ct = default) {
			using var appScope = logger.BeginApplicationScope(loginRequest.AppName);
			try {
				using var userScope = logger.BeginUserScope(loginRequest.GetUserIdentifier());
				var app = await appRepo.GetApplicationByNameAsync(loginRequest.AppName, ct: ct);
				var fixedFailureDelay = loginService.StartFixedFailureDelay(ct);
				User? user = null; // stash a reference to user to check app association later, and for username-based login between id-lookup and actual login.
				Guid userid;
				Func<Guid, Task<User?>> getUser;
				if (loginRequest is IdBasedLoginRequestDTO idBased) {
					userid = idBased.UserId;
					getUser = async uid => {
						user = await userManager.GetUserByIdAsync(userid, ct: ct);
						if (user == null) {
							metrics.HandleNonexistentUserIdError(loginRequest.AppName);
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
						metrics.HandleNonexistentUsernameError(loginRequest.AppName);
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
					user => user.HashedSecret ?? throw new ArgumentNullException(nameof(user.HashedSecret),
						"The user has no hashed secret. Can't login using user secret, but only by delegated authentication."),
					async (user, hashedSecret) => {
						user.HashedSecret = hashedSecret;
						await userManager.UpdateUserAsync(user, ct);
					},
					fixedFailureDelay!, ct,
					("appname", u => u.App.Name));

				if (token == null) {
					logger.LogError("Login attempt for user {userId} failed due to incorrect credentials.", loginRequest.GetUserIdentifier());
					if (user != null) {
						metrics.HandleIncorrectUserSecretError(loginRequest.AppName);
					}
				}
				// Intentionally no else if here, to log both failures if both, the app credentials AND the user credentials are invalid.
				if (app == null) {
					logger.LogError("Login attempt for user {userId} failed due to unknown application {appName}.", loginRequest.GetUserIdentifier(), loginRequest.AppName);
					metrics.HandleUnknownAppError(loginRequest.AppName);
				}
				else if (app.ApiToken != loginRequest.AppApiToken) {
					app = null; // Clear non-matching app to simplify controll flow below.
					logger.LogError("Login attempt for user {userId} failed due to incorrect API token for application {appName}.", loginRequest.GetUserIdentifier(), loginRequest.AppName);
					metrics.HandleIncorrectAppApiTokenError(loginRequest.AppName);
				}
				if (user != null && (user.App.Name != loginRequest.AppName)) {
					logger.LogError("Login attempt for user {userId} failed because the retrieved user is not associated with the given application {reqAppName}, but with {userAppName}.", loginRequest.GetUserIdentifier(), loginRequest.AppName, user.App.Name);
					metrics.HandleUserIdAppMismatchError(loginRequest.AppName);
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
					metrics.HandleSuccessfulLogin(loginRequest.AppName);
					var authData = token.Value;
					return new LoginResponseDTO(authData.Token, userid, authData.Expiry);
				}
			}
			catch (OperationCanceledException) {
				logger.LogDebug("Login attempt for user {userId} was cancelled.", loginRequest.GetUserIdentifier());
				throw;
			}
		}
		/// <summary>
		/// Handles starting a session using authentication delegation to an upstream backend.
		/// Upon success, the controller responds with a JSON-encoded <see cref="DelegatedLoginResponseDTO"/>,
		/// containing a session token that can be used to authenticate requests to SGL Analytics services as the logged-in user,
		/// and a <see cref="StatusCodes.Status200OK"/>.
		/// If the upstream backend rejects the supplied authorization token, a <see cref="StatusCodes.Status401Unauthorized"/> is returned.
		/// If the upstream user id is not registered yet, a <see cref="StatusCodes.Status404NotFound"/> is returned.
		/// If the token validation fails due to an error with the upstream backend, a <see cref="StatusCodes.Status503ServiceUnavailable"/> is returned.
		/// Other errors are represented by responding with a <see cref="StatusCodes.Status500InternalServerError"/>.
		/// </summary>
		/// <param name="requestDto">
		/// A data transfer object, containing the credentials to use for the login attempt.
		/// </param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		/// <returns>A <see cref="DelegatedLoginResponseDTO"/> containing the session token and the analytics and upstream ids of the user, or an error state.</returns>
		[ProducesResponseType(typeof(LoginResponseDTO), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable)]
		[HttpPost("open-session-from-upstream")]
		public async Task<ActionResult<DelegatedLoginResponseDTO>> OpenSessionFromUpstream([FromBody] UpstreamSessionRequestDTO requestDto, CancellationToken ct = default) {
			var appName = requestDto.AppName;
			using var appScope = logger.BeginApplicationScope(appName);
			ApplicationWithUserProperties? app = null;
			try {
				app = await appRepo.GetApplicationByNameAsync(appName, ct: ct);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("OpenSessionFromUpstream POST request for app {appName} was cancelled while fetching application metadata.", appName);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "OpenSessionFromUpstream POST request for app {appName} failed due to an unexpected exception.", appName);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
			if (app is null) {
				logger.LogError("OpenSessionFromUpstream POST request failed due to unknown application {appName}.", appName);
				metrics.HandleUnknownAppError(appName);
				return Unauthorized("The operation failed due to invalid application credentials.");
			}
			else if (app.ApiToken != requestDto.AppApiToken) {
				logger.LogError("RegisterUser POST request failed due to incorrect API token for application {appName}.", appName);
				metrics.HandleIncorrectAppApiTokenError(appName);
				return Unauthorized("The operation failed due to invalid application credentials.");
			}
			if (requestDto.UpstreamAuthorizationHeader == null) {
				return Unauthorized("The operation failed due to missing upstream authorization token.");
			}
			try {
				DelegatedLoginResponseDTO response = await userManager.OpenSessionFromUpstreamAsync(app, requestDto.UpstreamAuthorizationHeader, ct);
				// TODO: metrics
				return response;
			}
			catch (OperationCanceledException) {
				logger.LogDebug("OpenSessionFromUpstream POST request for app {appName} was cancelled while checking with upstream and looking up user.", appName);
				// TODO: metrics
				throw;
			}
			catch (NoUserForUpstreamIdException ex) {
				logger.LogInformation(ex, "OpenSessionFromUpstream POST request for app {appName} could not complete because there was no user for the upstream user id {uuid}. " +
					"The client needs to perform registration first.", appName, ex.UpstreamId);
				// TODO: metrics
				return NotFound("No user account for the upstream user id in the given auth token found.");
			}
			catch (UpstreamTokenRejectedException ex) {
				logger.LogError(ex, "OpenSessionFromUpstream POST request for app {appName} failed because the provided upstream authorization token was rejected by the upstream backend.", appName);
				// TODO: metrics
				return Unauthorized("The operation failed because the provided upstream authorization token was rejected by the upstream backend.");
			}
			catch (UpstreamTokenCheckFailedException ex) {
				logger.LogError(ex, "OpenSessionFromUpstream POST request for app {appName} failed because the check of the upstream token failed.", appName);
				// TODO: metrics
				return StatusCode(StatusCodes.Status503ServiceUnavailable, "Couldn't check provided auth token with upstream backend.");
			}
			catch (Exception ex) {
				logger.LogError(ex, "OpenSessionFromUpstream POST request for app {appName} failed due to an unexpected exception.", appName);
				metrics.HandleUnexpectedError(appName, ex);
				throw;
			}
		}

		/// <summary>
		/// Provides the list of authorized recipient certificates.
		/// Upon success, the controller responds with a PEM-encoded list of X509 certificates,
		/// one for each authorized recipient key pair, all signed by the app's signer certificate,
		/// and a <see cref="StatusCodes.Status200OK"/>.
		/// </summary>
		/// <param name="appName">The unique name of the app for which to obtain the list, passed as a query parameter.</param>
		/// <param name="appApiToken">
		/// The API token authentication token for the app identified by <paramref name="appName"/>,
		/// passed as an <c>App-API-Token</c> header.
		/// </param>
		/// <param name="ct">A cancellation token that is triggered when the client cancels the request.</param>
		/// <returns>
		/// A PEM-encoded list of certificates for the authorized recipient key-pairs,
		/// signed by the applications signed key, or an error state.
		/// </returns>
		[HttpGet("recipient-certificates")]
		[Produces("application/x-pem-file")]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		public async Task<ActionResult<IEnumerable<string>>> GetRecipientCertificates([FromQuery] string appName, [FromHeader(Name = "App-API-Token")][StringLength(64, MinimumLength = 8)] string? appApiToken, CancellationToken ct = default) {
			var app = await appRepo.GetApplicationByNameAsync(appName, new ApplicationQueryOptions { FetchRecipients = true }, ct: ct);
			if (app == null) {
				return Unauthorized();
			}
			if (appApiToken != null && app.ApiToken != appApiToken) {
				return Unauthorized();
			}
			else if (appApiToken == null) {
				var result = await HttpContext.AuthenticateAsync();
				var tokenAppName = result?.Principal?.GetClaim("appname");
				var keyId = result?.Principal?.GetClaim<KeyId>("keyid", KeyId.TryParse!);
				var exporterDN = result?.Principal?.GetClaim("exporter-dn");
				if (tokenAppName != appName) {
					return Unauthorized();
				}
				if (keyId == null) {
					return Unauthorized();
				}
				if (exporterDN == null) {
					return Unauthorized();
				}
			}
			return Ok(app.DataRecipients.Select(r => r.CertificatePem));
		}

	}
}
