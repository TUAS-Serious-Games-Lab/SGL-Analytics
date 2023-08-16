using SGL.Utilities;
using System;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the server to the client after a successful login.
	/// </summary>
	public class LoginResponseDTO {
		public AuthorizationToken Token { get; private set; }

		/// <summary>
		/// The id of the user that has logged in. This is also present as a claim in <see cref="Token"/>.
		/// </summary>
		public Guid? UserId { get; private set; }

		/// <summary>
		/// The date and time when <see cref="Token"/> expires.
		/// </summary>
		public DateTime? TokenExpiry { get; private set; }

		public LoginResponseDTO(AuthorizationToken token) {
			Token = token;
		}

		[JsonConstructor]
		public LoginResponseDTO(AuthorizationToken token, Guid? userId, DateTime? tokenExpiry) {
			Token = token;
			UserId = userId;
			TokenExpiry = tokenExpiry;
		}
	}

	public class DelegatedLoginResponseDTO : LoginResponseDTO {
		public Guid UpstreamUserId { get; private set; }

		[JsonConstructor]
		public DelegatedLoginResponseDTO(AuthorizationToken token, Guid? userId, DateTime? tokenExpiry, Guid upstreamUserId) : base(token, userId, tokenExpiry) {
			UpstreamUserId = upstreamUserId;
		}
	}
}
