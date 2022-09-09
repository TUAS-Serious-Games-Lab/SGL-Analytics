using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	/// <summary>
	/// Indicates that the backend server denied a request because it was made without a valid authorization token for a logged in user.
	/// This usually indicates an expired login token.
	/// </summary>
	public class LoginRequiredException : Exception {
		/// <summary>
		/// Instantiates the exception.
		/// </summary>
		public LoginRequiredException() : base("The backend requires authentication using a token obtained by login.") { }
	}
	/// <summary>
	/// Indicates that the server denied access with the used app and user credentials.
	/// </summary>
	public class UnauthorizedException : Exception {
		/// <summary>
		/// Instantiates the exception.
		/// </summary>
		public UnauthorizedException(Exception? innerException = null) : base("The operation couldn't be completed due to an authorization error.", innerException) { }
	}
	/// <summary>
	/// Indicates that the server rejected a file due to its size exceeding the configured limit.
	/// </summary>
	public class FileTooLargeException : Exception {
		/// <summary>
		/// Instantiates the exception.
		/// </summary>
		public FileTooLargeException(Exception? innerException) : base("The file was rejected by the server because it is too large.", innerException) { }
	}

	/// <summary>
	/// The interface that clients for the log collector backend need to implement.
	/// </summary>
	public interface ILogCollectorClient : IRecipientCertificatesClient {
		/// <summary>
		/// Indicates whether the log collection is active.
		/// This property should usually return true for real implementations (as the default implementation always does).
		/// It can be changed to false to disable the upload process for testing purposes.
		/// </summary>
		/// <remarks>
		/// This value is read by a background thread.
		/// Therefore, if an implementation allows changing this value during the lifetime of the client object, it needs to do so in a thread-safe way,
		/// i.e. it must take care of synchronization between the background thread and the thread on which the value is changed, e.g. by lock-blocks in both, the setter and the getter.
		/// </remarks>
		bool IsActive => true;

		/// <summary>
		/// Asynchronously uploads the given analytics log file to the backend using the given application credentials and the given authorization token for the user.
		/// </summary>
		/// <param name="appName">The technical name of the application used to identify it in the backend.</param>
		/// <param name="appAPIToken">The API token for the application to authenticate it with the backend.</param>
		/// <param name="authToken">The authorization token for the user, obtained by <see cref="IUserRegistrationClient.LoginUserAsync(LoginRequestDTO)"/>.</param>
		/// <param name="metadata">The metadata for the log file to upload.</param>
		/// <param name="content">The raw content of the log file to upload, encoded as described by <see cref="LogMetadataDTO.LogContentEncoding"/> in <paramref name="metadata"/>.</param>
		/// <returns>A task representing the upload operation.</returns>
		/// <exception cref="LoginRequiredException">
		/// Thrown when <paramref name="authToken"/> has expired or doesn't contain a recognized token.
		/// If this happens, a new token needs to be obtained using <see cref="IUserRegistrationClient.LoginUserAsync(LoginRequestDTO)"/> before reattempting the upload.
		/// </exception>
		/// <exception cref="UnauthorizedException">
		/// Thrown when the server denies access for the upload request with the given credentials.
		/// This can be a problem with any of the credential parameters <paramref name="appName"/>, <paramref name="appAPIToken"/>, or <paramref name="authToken"/>.
		/// Which of them causes the problem is not reported to the client for security reasons. Thus investigation may require inquiry with the backend operator.
		/// </exception>
		/// <exception cref="FileTooLargeException">
		/// Thrown when the server rejected the upload request because the given file is too large for the size limit of the server and thus can't be uploaded.
		/// </exception>
		Task UploadLogFileAsync(string appName, string appAPIToken, AuthorizationToken authToken, LogMetadataDTO metadata, Stream content);
	}
}
