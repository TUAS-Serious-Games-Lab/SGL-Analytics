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

	public static class JwtLoginServiceExtensions {
		public static IServiceCollection UseJwtLoginService(this IServiceCollection services, IConfiguration config) {
			services.Configure<JwtOptions>(config.GetSection(JwtOptions.Jwt));
			services.AddScoped<ILoginService, JwtLoginService>();
			return services;
		}
	}

	public class JwtOptions {
		public const string Jwt = "Jwt";
		public string? SymmetricKey { get; set; }
		public string Issuer { get; set; } = "SGL Analytics";
		public string Audience { get; set; } = "SGL Analytics";
		public JwtLoginServiceOptions LoginService { get; set; } = new JwtLoginServiceOptions();
	}

	public class JwtLoginServiceOptions {
		public TimeSpan FailureDelay { get; set; } = TimeSpan.FromMilliseconds(1500);
		public string SigningAlgorithm { get; set; } = SecurityAlgorithms.HmacSha256;
		public TimeSpan ExpirationTime { get; set; } = TimeSpan.FromDays(1);
	}

	public class JwtLoginService : ILoginService {
		private ILogger<JwtLoginService> logger;
		private JwtOptions options;
		private SecurityKey signingKey;
		private SigningCredentials signingCredentials;

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

		public ILoginService.IDelayHandle StartFixedFailureDelay(CancellationToken ct = default) {
			// On failure, always wait for this fixed delay starting here.
			// This should mitigate timing attacks for detecting whether the failure is
			// due to non-existent user or due to incorrect password.
			// This also slows down brute-force attacks.
			// The task is created here and passed into LoginAsync to optionally allow callers
			// to capture it and await it to also delay related failures outside the login service's responsibility.
			return new ILoginService.DelayHandle(Task.Delay(options.LoginService.FailureDelay, ct));
		}

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
