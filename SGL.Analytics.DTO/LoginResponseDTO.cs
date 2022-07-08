using SGL.Utilities;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the server to the client after a successful login.
	/// </summary>
	public class LoginResponseDTO {
		public AuthorizationToken Token { get; private set; }

		public LoginResponseDTO(AuthorizationToken token) {
			Token = token;
		}
	}
}
