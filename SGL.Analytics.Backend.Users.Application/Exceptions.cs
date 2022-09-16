using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application {
	public class InvalidChallengeException : Exception {
		public Guid ChallengeId { get; }
		public InvalidChallengeException(Guid challengeId, string message, Exception? innerException = null) : base(message, innerException) {
			ChallengeId = challengeId;
		}
	}
	public class NoCertificateForKeyIdException : Exception {
		public KeyId KeyId { get; }
		public NoCertificateForKeyIdException(KeyId keyId, string message, Exception? innerException = null) : base(message, innerException) { }
	}
	public class ChallengeCompletionFailedException : Exception {
		public ChallengeCompletionFailedException(string message, Exception? innerException = null) : base(message, innerException) { }
	}
}
