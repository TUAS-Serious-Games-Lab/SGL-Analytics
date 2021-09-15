using SGL.Analytics.DTO;
using System;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	public class LoginRequiredException : Exception {
		public LoginRequiredException() : base("The backend requires authentication using a token obtained by login.") { }
	}
	public class UnauthorizedException : Exception {
		public UnauthorizedException(Exception? innerException = null) : base("The operation couldn't be completed due to an authorization error.", innerException) { }
	}
	public class FileTooLargeException : Exception {
		public FileTooLargeException(Exception? innerException) : base("The file was rejected by the server because it is too large.", innerException) { }
	}

	public interface ILogCollectorClient {
		/// <summary>
		/// Indicates whether the log collection is active.
		/// This property should usually return true for real implementations (as the default implementation always does).
		/// It can be changed to false to disable the upload process for testing purposes.
		/// </summary>
		/// <remarks>This value is read by a background thread.
		/// Therefore, if an implementation allows changing this value during the lifetime of the client object, it needs to do so in a thread-safe way,
		/// i.e. it must take care of synchronization between the background thread and the thread on which the value is changed, e.g. by lock-blocks in both, the setter and the getter.</remarks>
		bool IsActive => true;

		Task UploadLogFileAsync(string appName, string appAPIToken, Guid userID, LoginResponseDTO loginData, ILogStorage.ILogFile logFile);
	}
}