using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.Crypto.Signatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Values {
	public class ChallengeState {
		public Guid ChallengeId { get; private set; }
		public string AppName { get; private set; }
		public KeyId KeyId { get; private set; }
		public SignatureDigest DigestAlgorithm { get; private set; }
		public byte[] ChallengeBytes { get; private set; }
		public DateTime Timeout { get; private set; }

		public ChallengeState(Guid challengeId, string appName, KeyId keyId, SignatureDigest digestAlgorithm, byte[] challengeBytes, DateTime timeout) {
			ChallengeId = challengeId;
			AppName = appName;
			KeyId = keyId;
			DigestAlgorithm = digestAlgorithm;
			ChallengeBytes = challengeBytes;
			Timeout = timeout;
		}
	}
}
