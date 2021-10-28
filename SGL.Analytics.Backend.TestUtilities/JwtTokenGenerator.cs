using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.TestUtilities {

	/// <summary>
	/// A utility class to simplify issuing JWT bearer tokens for testing purposes.
	/// </summary>
	public class JwtTokenGenerator {
		private string issuer;
		private string audience;
		private string? symmetricKey;
		private SecurityKey signingKey;
		private SigningCredentials signingCredentials;
		/// <summary>
		/// Instantiates the token generator using the given token parameters.
		/// </summary>
		/// <param name="issuer">The issuer identification to use.</param>
		/// <param name="audience">The audience identifaction to use.</param>
		/// <param name="symmetricKey">The symmetric secret key to use for signing the issued token.</param>
		public JwtTokenGenerator(string issuer, string audience, string symmetricKey) {
			this.issuer = issuer;
			this.audience = audience;
			this.symmetricKey = symmetricKey;
			signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(symmetricKey));
			signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
		}

		/// <summary>
		/// Generates a JWT bearer token with the given parameters.
		/// </summary>
		/// <param name="userId">The user id of the user that is authenticated by the token.</param>
		/// <param name="expirationTime">The time until the token expires.</param>
		/// <param name="additionalClaims">Additional claims to add to the token besides the user id.</param>
		/// <returns></returns>
		public string GenerateToken(Guid userId, TimeSpan expirationTime, params (string ClaimType, string ClaimValue)[] additionalClaims) {
			var claims = additionalClaims.Select(c => new Claim(c.ClaimType, c.ClaimValue))
				.Prepend(new Claim("userid", userId.ToString() ?? "")).ToArray();
			var token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				expires: DateTime.UtcNow.Add(expirationTime),
				signingCredentials: signingCredentials
			);
			var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
			return tokenString;
		}
	}
}
