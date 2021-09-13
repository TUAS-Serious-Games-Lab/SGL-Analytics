using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public class LoginFailedException : Exception {
		public LoginFailedException() : base("Login failed due to invalid credentials.") { }
	}

	public class LoginErrorException : Exception {
		public LoginErrorException(Exception? innerException = null) : base("Login failed due to an error.", innerException) { }
		public LoginErrorException(string errorMessage, Exception? innerException = null) : base($"Login failed to the following error: {errorMessage}", innerException) { }
	}

	public class UserRegistrationResponseException : Exception {
		public UserRegistrationResponseException(string? message) : base(message) { }
		public UserRegistrationResponseException(string? message, Exception? innerException) : base(message, innerException) { }
	}

	public class UsernameAlreadyTakenException : Exception {
		public string Username { get; }
		public UsernameAlreadyTakenException(string username) : base($"The username '{username}' is already taken.") {
			Username = username;
		}
		public UsernameAlreadyTakenException(string username, Exception? innerException) : base($"The username '{username}' is already taken.", innerException) {
			Username = username;
		}
	}

	public interface IUserRegistrationClient {
		Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userDTO, string appAPIToken);
		Task<LoginResponseDTO> LoginUserAsync(LoginRequestDTO loginDTO);
	}
}
