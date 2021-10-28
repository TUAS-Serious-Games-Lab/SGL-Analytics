using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SGL.Analytics.Backend.Security;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.WebUtilities {
	internal class Authentication { };

	/// <summary>
	/// Provides the <see cref="UseJwtBearerAuthentication(IServiceCollection, IConfiguration)"/> extension method.
	/// </summary>
	public static class JwtAuthenticationExtensions {

		/// <summary>
		/// Enables JWT bearer token authentication to accept requests authorized by tokens issued by <see cref="JwtLoginService"/>, mapping the claims contained in the token into the <see cref="HttpContext.User"/>.
		/// </summary>
		/// <param name="services">The service collection where the authentication handler shall added.</param>
		/// <param name="config">The configuration root to use.</param>
		/// <returns></returns>
		/// <remarks>
		/// The given configuration needs to contain the following keys:
		/// <list type="table">
		/// <listheader><term>Key</term><term>Meaning</term></listheader>
		/// <item><term><c>Jwt:Issuer</c></term><description>The issuer identification from which tokens must be issued to be accepted.</description></item>
		/// <item><term><c>Jwt:Audience</c></term><description>The audience identification for which a token must be issued to be accepted.</description></item>
		/// <item><term><c>Jwt:SymmetricKey</c></term><description>The secret string used as the symmetric key for validating the token signatures.</description></item>
		/// </list>
		/// Note that this configuration structure is compatible with that used by <see cref="JwtOptions"/>.
		/// </remarks>
		public static IServiceCollection UseJwtBearerAuthentication(this IServiceCollection services, IConfiguration config) {
			services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
				options.TokenValidationParameters = new TokenValidationParameters {
					ValidateIssuer = true,
					ValidateAudience = true,
					ValidateLifetime = true,
					ValidateIssuerSigningKey = true,
					ValidIssuer = config["Jwt:Issuer"],
					ValidAudience = config["Jwt:Audience"],
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SymmetricKey"]))
				};
				options.SaveToken = true;
				options.Events = new JwtBearerEvents {
					OnAuthenticationFailed = context => {
						var token = readTokenFromRequest(context.Request);
						var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Authentication>>();
						logger.LogError(context.Exception, "Authentication failed for user {userid} and app {appName}.",
							token?.Claims.GetClaim("userid") ?? "null", token?.Claims.GetClaim("appname") ?? "null");
						return Task.CompletedTask;
					},
					OnChallenge = context => {
						var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Authentication>>();
						logger.LogInformation("Challenging user for authentication for {verb} access to {path}.",
							context.Request.Method, context.Request.Path.Value ?? "<path not specified in request>");
						return Task.CompletedTask;
					},
					OnForbidden = context => {
						var token = readTokenFromRequest(context.Request);
						var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Authentication>>();
						logger.LogError(context.Result.Failure, "Access forbidden for {verb} access to {path} from user {userid} and app {appName}.",
							token?.Claims.GetClaim("userid") ?? "null", token?.Claims.GetClaim("appname") ?? "null");
						return Task.CompletedTask;
					},
					OnTokenValidated = context => {
						var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Authentication>>();
						logger.LogInformation("Successfully authenticated user {userid} from app {appName} for {verb} access to {path}",
							context.Principal?.GetClaim("userid") ?? "null", context.Principal?.GetClaim("appname") ?? "null",
							context.Request.Method, context.Request.Path.Value ?? "<path not specified in request>");
						return Task.CompletedTask;
					}
				};
			});
			return services;
		}

		private static JwtSecurityToken? readTokenFromRequest(HttpRequest request) {
			try {
				var authHeader = request.Headers["Authorization"].FirstOrDefault();
				if (authHeader == null) return null;
				var parsedAuthHeader = AuthenticationHeaderValue.Parse(authHeader);
				if (parsedAuthHeader == null) return null;
				return new JwtSecurityTokenHandler().ReadJwtToken(parsedAuthHeader.Parameter);
			}
			catch {
				return null;
			}
		}
	}
}
