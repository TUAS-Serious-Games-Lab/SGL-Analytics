using Microsoft.IdentityModel.Tokens;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.TestUtilities {

	/// <summary>
	/// A utility class that simplifies validating JWT bearer tokens for testing purposes.
	/// </summary>
	public class JwtTokenValidator {
		private JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
		private TokenValidationParameters tokenValidationParameters;

		/// <summary>
		/// Instantiates the token validator using the given validation parameters.
		/// </summary>
		/// <param name="issuer">The issuer identification to use.</param>
		/// <param name="audience">The audience identifaction to use.</param>
		/// <param name="symmetricKey">The symmetric secret key to use for validating the issued token.</param>
		public JwtTokenValidator(string issuer, string audience, string symmetricKey) {
			tokenValidationParameters = new TokenValidationParameters() {
				ValidateIssuer = true,
				ValidateAudience = true,
				ValidateLifetime = true,
				ValidateIssuerSigningKey = true,
				ValidAudience = audience,
				ValidIssuer = issuer,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(symmetricKey))
			};
		}

		/// <summary>
		/// Instantiates the token validator using the same string for the issuer and the audience.
		/// </summary>
		/// <param name="audienceAndIssuer">The identification to use.</param>
		/// <param name="symmetricKey">The symmetric secret key to use for validating the issued token.</param>
		public JwtTokenValidator(string audienceAndIssuer, string symmetricKey) : this(audienceAndIssuer, audienceAndIssuer, symmetricKey) { }

		/// <summary>
		/// Validates the given token against the parameters specified in the constructor and returns a principal with the contained claims and the decoded token.
		/// It throws exceptions from <see cref="JwtSecurityTokenHandler.ValidateToken(string, TokenValidationParameters, out SecurityToken)"/> if validation fails.
		/// </summary>
		/// <param name="token">The token to validate.</param>
		/// <returns>A principal containing the claims from the token and the decoded token</returns>
		public (ClaimsPrincipal principal, SecurityToken validatedToken) Validate(AuthorizationToken token) {
			var principal = tokenHandler.ValidateToken(token.Value, tokenValidationParameters, out var validatedToken);
			return (principal, validatedToken);
		}
	}
}
