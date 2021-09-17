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
	public class JwtTokenValidator {
		private JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
		private TokenValidationParameters tokenValidationParameters;

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

		public JwtTokenValidator(string audienceAndIssuer, string symmetricKey) : this(audienceAndIssuer, audienceAndIssuer, symmetricKey) { }

		public (ClaimsPrincipal principal, SecurityToken validatedToken) Validate(AuthorizationToken token) {
			var principal = tokenHandler.ValidateToken(token.Value, tokenValidationParameters, out var validatedToken);
			return (principal, validatedToken);
		}
	}
}
