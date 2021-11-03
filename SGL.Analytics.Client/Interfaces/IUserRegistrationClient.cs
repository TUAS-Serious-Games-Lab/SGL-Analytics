using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	/// <summary>
	/// Indicates that a login failed due to incorrect credentials.
	/// </summary>
	public class LoginFailedException : Exception {
		/// <summary>
		/// Instantiates the exception with the given parameters.
		/// </summary>
		public LoginFailedException() : base("Login failed due to invalid credentials.") { }
	}

	/// <summary>
	/// Indicates a general error during a login request.
	/// </summary>
	public class LoginErrorException : Exception {
		/// <summary>
		/// Instantiates the exception with the given parameters.
		/// </summary>
		public LoginErrorException(Exception? innerException = null) : base("Login failed due to an error.", innerException) { }
		/// <summary>
		/// Instantiates the exception with the given parameters.
		/// </summary>
		public LoginErrorException(string errorMessage, Exception? innerException = null) : base($"Login failed to the following error: {errorMessage}", innerException) { }
	}

	/// <summary>
	/// Indicates that the response for user registration did not contain the expected <see cref="UserRegistrationResultDTO"/>.
	/// </summary>
	public class UserRegistrationResponseException : Exception {
		/// <summary>
		/// Instantiates the exception with the given parameters.
		/// </summary>
		public UserRegistrationResponseException(string? message) : base(message) { }
		/// <summary>
		/// Instantiates the exception with the given parameters.
		/// </summary>
		public UserRegistrationResponseException(string? message, Exception? innerException) : base(message, innerException) { }
	}

	/// <summary>
	/// Indicates that the username given for the registration is already in use by an existing account.
	/// </summary>
	public class UsernameAlreadyTakenException : Exception {
		/// <summary>
		/// The affected username.
		/// </summary>
		public string Username { get; }

		/// <summary>
		/// Instantiates the exception with the given parameters.
		/// </summary>
		public UsernameAlreadyTakenException(string username) : base($"The username '{username}' is already taken.") {
			Username = username;
		}
		/// <summary>
		/// Instantiates the exception with the given parameters.
		/// </summary>
		public UsernameAlreadyTakenException(string username, Exception? innerException) : base($"The username '{username}' is already taken.", innerException) {
			Username = username;
		}
	}

	/// <summary>
	/// The interface that clients for the user registration backend need to implement.
	/// </summary>
	public interface IUserRegistrationClient {
		/// <summary>
		/// Asynchronously registers a new user with the given user data and the given application API token.
		/// </summary>
		/// <param name="userDTO">The data transfer object containing the user data.</param>
		/// <param name="appAPIToken">The API token to authenticate the application.</param>
		/// <returns>A task representing the registration operation, providing the response from the server as its result upon completion.</returns>
		Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userDTO, string appAPIToken);
		/// <summary>
		/// Asynchronously performs a login operation for a user and, if successful, obtains an authorization token that can be used to make other requests as the user.
		/// </summary>
		/// <param name="loginDTO">A data transfer object, bundling the application and user credentials to use for the login request.</param>
		/// <returns>A task representing the login operation, providing the response from the server, containing an authorization token (if successful), as its result upon completion.</returns>
		Task<AuthorizationToken> LoginUserAsync(LoginRequestDTO loginDTO);
	}
}
