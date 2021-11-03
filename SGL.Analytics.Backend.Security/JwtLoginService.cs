using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Security {
	/// <summary>
	/// Provides the <see cref="UseJwtLoginService(IServiceCollection, IConfiguration)"/> extension method.
	/// </summary>
	public static class JwtLoginServiceExtensions {
		/// <summary>
		/// Adds the <see cref="JwtLoginService"/> as the implementation for <see cref="ILoginService"/>, along with its configuration in the service collection.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The root configuration obejct to obtain configuration options from.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseJwtLoginService(this IServiceCollection services, IConfiguration config) {
			services.Configure<JwtOptions>(config.GetSection(JwtOptions.Jwt));
			services.AddScoped<ILoginService, JwtLoginService>();
			return services;
		}
	}

	/// <summary>
	/// Encapsulates the configuration options for <see cref="JwtLoginService"/>.
	/// It is split into general JWT options, that are also needed for services consuming the issued tokens, and the <see cref="JwtLoginServiceOptions"/>, only needed for the login service itself.
	/// </summary>
	public class JwtOptions {
		/// <summary>
		/// A constant defining the key path under which the configuration options are located.
		/// Under this key, the options are named as their respective properties.
		/// The top-level key is <c>Jwt</c>.
		/// </summary>
		public const string Jwt = "Jwt";
		/// <summary>
		/// A secret string used as the signing key for the tokens issued by <see cref="JwtLoginService"/>.
		/// </summary>
		public string? SymmetricKey { get; set; }
		/// <summary>
		/// The issuer identification to use for the tokens issued by <see cref="JwtLoginService"/>.
		/// </summary>
		public string Issuer { get; set; } = "SGL Analytics";
		/// <summary>
		/// The audience identification to use for the tokens issued by <see cref="JwtLoginService"/>.
		/// </summary>
		public string Audience { get; set; } = "SGL Analytics";
		/// <summary>
		/// The configuration options for the <see cref="JwtLoginService"/>.
		/// </summary>
		public JwtLoginServiceOptions LoginService { get; set; } = new JwtLoginServiceOptions();
	}

	/// <summary>
	/// Encapsulates the configuration options for <see cref="JwtLoginService"/> that are only needed on the authentication side.
	/// </summary>
	public class JwtLoginServiceOptions {
		/// <summary>
		/// Specifies the duration of the fixed failure delay time.
		/// </summary>
		public TimeSpan FailureDelay { get; set; } = TimeSpan.FromMilliseconds(1500);
		/// <summary>
		/// Specifies the cryptographic signing algorithm to use to sign the issued tokens.
		/// </summary>
		public string SigningAlgorithm { get; set; } = SecurityAlgorithms.HmacSha256;
		/// <summary>
		/// Specifies the expiration time for the tokens issued by <see cref="JwtLoginService"/>.
		/// </summary>
		public TimeSpan ExpirationTime { get; set; } = TimeSpan.FromDays(1);
	}

	/// <summary>
	/// An implementation of <see cref="ILoginService"/> that issues JWT bearer tokens upon successful authentication.
	/// </summary>
	public class JwtLoginService : ILoginService {
		private ILogger<JwtLoginService> logger;
		private JwtOptions options;
		private SecurityKey signingKey;
		private SigningCredentials signingCredentials;

		/// <summary>
		/// Instantiates a <see cref="JwtLoginService"/> with the given logger and configuration options.
		/// </summary>
		public JwtLoginService(ILogger<JwtLoginService> logger, IOptions<JwtOptions> options) {
			this.logger = logger;
			this.options = options.Value;
			if (this.options.SymmetricKey is not null) {
				signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.SymmetricKey));
				signingCredentials = new SigningCredentials(signingKey, this.options.LoginService.SigningAlgorithm);
			}
			else {
				throw new InvalidOperationException("No signing key given for JwtLoginServiceOptions.");
			}
		}

		/// <summary>
		/// Starts a fixed delay timer with the duration configured in <see cref="JwtLoginServiceOptions.FailureDelay"/> and with the given cancellation token.
		/// </summary>
		/// <returns>An <see cref="ILoginService.IDelayHandle"/> encapsulating the wait timer.</returns>
		public ILoginService.IDelayHandle StartFixedFailureDelay(CancellationToken ct = default) {
			// On failure, always wait for this fixed delay starting here.
			// This should mitigate timing attacks for detecting whether the failure is
			// due to non-existent user or due to incorrect secret.
			// This also slows down brute-force attacks.
			// The task is created here and passed into LoginAsync to optionally allow callers
			// to capture it and await it to also delay related failures outside the login service's responsibility.
			return new ILoginService.DelayHandle(Task.Delay(options.LoginService.FailureDelay, ct));
		}

		/// <summary>
		/// Asynchronously performs a login attemp with the given credentials, using the given delegates to access the user management as described in <see cref="ILoginService"/>,
		/// and upon success, issues a JWT bearer token with a <c>userid</c> claim for the user's id, as well as the additional claims specified in <c>additionalClaims</c>.
		/// </summary>
		/// <returns>A task representing the operation with a result of <see langword="null"/> for failed attempts and with <see cref="AuthorizationToken"/> containing a JWT bearer token for a successful login.</returns>
		public async Task<AuthorizationToken?> LoginAsync<TUserId, TUser>(TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync,
			ILoginService.IDelayHandle fixedFailureDelay, CancellationToken ct = default,
			params (string ClaimType, Func<TUser, string> GetClaimValue)[] additionalClaims) {

			bool secretCorrect = false;
			bool rehashed = false;
			TUser? user;
			string? hashedSecret = null;
			try {
				if (userId is null) {
					logger.LogError("Login failed because no userId was given.");
					await fixedFailureDelay.WaitAsync().ConfigureAwait(false);
					return null;
				}
				user = await lookupUserAsync(userId).ConfigureAwait(false);
				if (user is null) {
					logger.LogError("Login failed because the user with id {userId} was not found.", userId);
					await fixedFailureDelay.WaitAsync().ConfigureAwait(false);
					return null;
				}
				hashedSecret = getHashedSecret(user);
				(secretCorrect, rehashed) = SecretHashing.VerifyHashedSecret(ref hashedSecret, providedPlainSecret);
				if (!secretCorrect) {
					logger.LogError("Login failed because the given secret didn't match the hashed secret of the given user with id {userId}.", userId);
					await fixedFailureDelay.WaitAsync().ConfigureAwait(false);
					return null;
				}
			}
			catch (Exception ex) {
				logger.LogError(ex, "Login failed due to unexpected exception.", userId);
				await fixedFailureDelay.WaitAsync().ConfigureAwait(false);
				return null;
			}
			if (rehashed) {
				await updateHashedSecretAsync(user, hashedSecret).ConfigureAwait(false);
			}
			var claims = additionalClaims.Select(cg => new Claim(cg.ClaimType, cg.GetClaimValue(user)))
				.Prepend(new Claim("userid", userId.ToString() ?? "")).ToArray();
			var token = new JwtSecurityToken(
				issuer: options.Issuer,
				audience: options.Audience,
				claims: claims,
				expires: DateTime.UtcNow.Add(options.LoginService.ExpirationTime),
				signingCredentials: signingCredentials
			);
			var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
			logger.LogInformation("Login succeeded for user {userId}.", userId);
			return new AuthorizationToken(tokenString);
		}
	}
}
