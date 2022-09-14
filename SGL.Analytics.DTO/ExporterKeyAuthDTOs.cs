using SGL.Utilities;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGL.Analytics.DTO {
	public class ExporterKeyAuthRequestDTO {
		[PlainName]
		[StringLength(128, MinimumLength = 1)]
		public string AppName { get; private set; }
		public KeyId KeyId { get; private set; }

		public ExporterKeyAuthRequestDTO(string appName, KeyId keyId) {
			AppName = appName;
			KeyId = keyId;
		}
	}
	public class ExporterKeyAuthChallengeDTO {
		public Guid ChallengeId { get; private set; }
		[MaxLength(64 * 1024)]
		public byte[] ChallengeBytes { get; private set; }
		//TODO Change to enum of supported digests?
		public string DigestAlgorithmToUse { get; private set; }

		public ExporterKeyAuthChallengeDTO(Guid challengeId, byte[] challengeBytes, string digestAlgorithmToUse) {
			ChallengeId = challengeId;
			ChallengeBytes = challengeBytes;
			DigestAlgorithmToUse = digestAlgorithmToUse;
		}
	}

	public class ExporterKeyAuthSignatureDTO {
		public Guid ChallengeId { get; private set; }

		/// <summary>
		/// Signature over the sequence
		/// <list type="number">
		/// <item><description>the bytes of <see cref="ExporterKeyAuthChallengeDTO.ChallengeId"/></description></item>
		/// <item><description>the bytes of <see cref="ExporterKeyAuthRequestDTO.KeyId"/></description></item>
		/// <item><description>the bytes in <see cref="ExporterKeyAuthChallengeDTO.ChallengeBytes"/></description></item>
		/// </list>
		///	using <see cref="ExporterKeyAuthChallengeDTO.DigestAlgorithmToUse"/>,
		///	with the appropriate signature algotihm for the key identified by <see cref="ExporterKeyAuthRequestDTO.KeyId"/>.
		/// </summary>
		[MaxLength(64 * 1024)]
		public byte[] Signature { get; private set; }

		public ExporterKeyAuthSignatureDTO(Guid challengeId, byte[] signature) {
			ChallengeId = challengeId;
			Signature = signature;
		}
	}

	public class ExporterKeyAuthResponseDTO {
		public AuthorizationToken Token { get; private set; }

		public ExporterKeyAuthResponseDTO(AuthorizationToken token) {
			Token = token;
		}
	}
}
