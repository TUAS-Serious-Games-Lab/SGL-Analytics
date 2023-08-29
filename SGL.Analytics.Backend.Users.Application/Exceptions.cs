using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application {
	/// <summary>
	/// An exception thrown when an attempt to complete a challenge was made with a non-existent challenge id.
	/// </summary>
	public class InvalidChallengeException : Exception {
		/// <summary>
		/// The invalid challenge id.
		/// </summary>
		public Guid ChallengeId { get; }
		/// <summary>
		/// Constructs a new exception object with the given data.
		/// </summary>
		public InvalidChallengeException(Guid challengeId, string message, Exception? innerException = null) : base(message, innerException) {
			ChallengeId = challengeId;
		}
	}
	/// <summary>
	/// An exception thrown when an challenge was attempted with a key id for which no authentication certificate was found.
	/// </summary>
	public class NoCertificateForKeyIdException : Exception {
		/// <summary>
		/// The key id for which no certificate was found.
		/// </summary>
		public KeyId KeyId { get; }
		/// <summary>
		/// Constructs a new exception object with the given data.
		/// </summary>
		public NoCertificateForKeyIdException(KeyId keyId, string message, Exception? innerException = null) : base(message, innerException) {
			KeyId = keyId;
		}
	}
	/// <summary>
	/// An exception thrown when an challenge completion failed because the signature was invalid.
	/// </summary>
	public class ChallengeCompletionFailedException : Exception {
		/// <summary>
		/// Constructs a new exception object with the given data.
		/// </summary>
		public ChallengeCompletionFailedException(string message, Exception? innerException = null) : base(message, innerException) { }
	}
	/// <summary>
	/// An exception thrown when in a delegated authentication attempt, the upstream token was valid,
	/// but the upstream user id returned by the upstream backend was not registered in the analytics user database.
	/// </summary>
	public class NoUserForUpstreamIdException : Exception {
		/// <summary>
		/// The upstream user id that was not present in any registered user account.
		/// </summary>
		public Guid UpstreamId { get; }
		/// <summary>
		/// Constructs a new exception object with the given data.
		/// </summary>
		public NoUserForUpstreamIdException(Guid upstreamId, string message, Exception? innerException = null) : base(message, innerException) {
			UpstreamId = upstreamId;
		}
	}
	/// <summary>
	/// An exception thrown when in a delegated authentication attempt, the upstream backend rejects the supplied upstream authorization token.
	/// </summary>
	public class UpstreamTokenRejectedException : Exception {
		/// <summary>
		/// Constructs a new exception object with the given data.
		/// </summary>
		public UpstreamTokenRejectedException(string message, Exception? innerException = null) : base(message, innerException) { }
	}
	/// <summary>
	/// An exception thrown when in a delegated authentication attempt, the supplied upstream authorization token could not be checked
	/// against the upstream backend due to an unexpected error or network issues.
	/// </summary>
	public class UpstreamTokenCheckFailedException : Exception {
		/// <summary>
		/// Constructs a new exception object with the given data.
		/// </summary>
		public UpstreamTokenCheckFailedException(string message, Exception? innerException = null) : base(message, innerException) { }
	}
	/// <summary>
	/// An exception thrown when delegated authentication was attempted but no upstream backend was configured for the application.
	/// </summary>
	public class NoUpstreamBackendConfiguredException : Exception {
		/// <summary>
		/// Constructs a new exception object with the given data.
		/// </summary>
		public NoUpstreamBackendConfiguredException(string message, Exception? innerException = null) : base(message, innerException) { }
	}
}
