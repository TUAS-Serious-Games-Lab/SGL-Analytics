using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.TestUtilities {
	public class JwtTokenGenerator {
		private string issuer;
		private string audience;
		private string? symmetricKey;
		private SecurityKey signingKey;
		private SigningCredentials signingCredentials;
		public JwtTokenGenerator(string issuer, string audience, string symmetricKey) {
			this.issuer = issuer;
			this.audience = audience;
			this.symmetricKey = symmetricKey;
			signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(symmetricKey));
			signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
		}

		public string GenerateToken(Guid userId, TimeSpan expirationTime) {
			var token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: new[] { new Claim("userid", userId.ToString() ?? "") },
				expires: DateTime.UtcNow.Add(expirationTime),
				signingCredentials: signingCredentials
			);
			var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
			return tokenString;
		}
	}
}
