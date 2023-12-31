using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.Threading;
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
	/// Indicates that a delegated login failed because the upstream user id is not registered.
	/// </summary>
	public class NoDelegatedUserException : Exception {
		/// <summary>
		/// Instantiates the exception with the given parameters.
		/// </summary>
		public NoDelegatedUserException() : base("No account registered for delegated authentication.") { }
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
	public interface IUserRegistrationClient : IApiClient, IRecipientCertificatesClient {
		/// <summary>
		/// After a successful call to <see cref="LoginUserAsync(LoginRequestDTO, CancellationToken)"/> or <see cref="OpenSessionFromUpstream(AuthorizationData, CancellationToken)"/>,
		/// contains the Authorization token data obtained from the Users service. It can be used for the clients of other services to authorize requests.
		/// Only unauthenticated APIs can be called while this is null, or while it contains an expired token.
		/// </summary>
		new AuthorizationData? Authorization { get; }
		AuthorizationData? IApiClient.Authorization { get => Authorization; set => throw new NotSupportedException(); }

		/// <summary>
		/// After a successful call to <see cref="LoginUserAsync(LoginRequestDTO, CancellationToken)"/> or <see cref="OpenSessionFromUpstream(AuthorizationData, CancellationToken)"/>,
		/// contains the id of the authenticated in user.
		/// </summary>
		Guid? AuthorizedUserId { get; }

		/// <summary>
		/// An event triggered when a user was authenticated and thus a new <see cref="AuthorizationToken"/> is now available in <see cref="Authorization"/>.
		/// Allows other clients to be notified when a new token was obtained, either initially or through a re-login after a token has expired.
		/// They can then use the new token for their requests as well.
		/// </summary>
		event AsyncEventHandler<UserAuthenticatedEventArgs>? UserAuthenticated;


		/// <summary>
		/// Asynchronously registers a new user with the given user data.
		/// </summary>
		/// <param name="userDTO">The data transfer object containing the user data.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task representing the registration operation, providing the response from the server as its result upon completion.</returns>
		Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userDTO, CancellationToken ct = default);

		/// <summary>
		/// Asynchronously performs a login operation for a user and, if successful, obtains an authorization token that can be used to make other requests as the user.
		/// </summary>
		/// <param name="loginDTO">A data transfer object, bundling the application and user credentials to use for the login request.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task representing the login operation, providing the response from the server, containing an authorization token (if successful), as its result upon completion.</returns>
		Task<LoginResponseDTO> LoginUserAsync(LoginRequestDTO loginDTO, CancellationToken ct = default);

		/// <summary>
		/// Asynchronously opens a user session using delegated authentication with the provided upstream authorization token.
		/// The token is passed to the analytics backend, which verifies it with the upstream backend configured for the application.
		/// Upon success, if the user is registered with the upstream user id, an analytics session token for that user is returned.
		/// </summary>
		/// <param name="upstreamAuthToken">The authorization data for the upstream backend.</param>
		/// <param name="ct">>A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task representing the authentication operation, providing the response from the server, containing an authorization token (if successful), as its result upon completion.</returns>
		/// <exception cref="LoginFailedException">The token was rejected by the upstream backend.</exception>
		/// <exception cref="NoDelegatedUserException">No user with the obtained upstream id was registered in the analytics backend.</exception>
		Task<DelegatedLoginResponseDTO> OpenSessionFromUpstream(AuthorizationData upstreamAuthToken, CancellationToken ct = default);
	}
}
