using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Security {

	public static class JwtLoginServiceExtensions {
		public static IServiceCollection UseJwtLoginService(this IServiceCollection services, IConfiguration config) {
			services.Configure<JwtOptions>(config.GetSection(JwtOptions.Jwt));
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

		public async Task<string?> LoginAsync<TUserId, TUser>(TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync) {
			var fixedDelay = Task.Delay(options.LoginService.FailureDelay); // On failure, always wait for this fixed delay starting here.
																			// This should mitigate timing attacks for detecting whether the failure is
																			// due to non-existent user or due to incorrect password.
			bool secretCorrect = false;
			bool rehashed = false;
			TUser? user;
			string? hashedSecret = null;
			try {
				if (userId is null) {
					await fixedDelay;
					return null;
				}
				user = await lookupUserAsync(userId);
				if (user is null) {
					await fixedDelay;
					return null;
				}
				hashedSecret = getHashedSecret(user);
				(secretCorrect, rehashed) = SecretHashing.VerifyHashedSecret(ref hashedSecret, providedPlainSecret);
				if (!secretCorrect) {
					await fixedDelay;
					return null;
				}
			}
			catch (Exception) {
				await fixedDelay;
				return null;
			}
			if (rehashed) {
				await updateHashedSecretAsync(user, hashedSecret);
			}
			var token = new JwtSecurityToken(
				issuer: options.Issuer,
				audience: options.Audience,
				claims: new[] { new Claim("userid", userId.ToString() ?? "") },
				expires: DateTime.UtcNow.Add(options.LoginService.ExpirationTime),
				signingCredentials: signingCredentials
			);
			var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
			return tokenString;
		}
	}
}
