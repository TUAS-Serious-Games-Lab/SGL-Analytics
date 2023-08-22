using SGL.Utilities;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.Crypto.Signatures;
using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// The request data sent from client to server in the first step of the key-pair-based challenge authentication for exporter clients.
	/// </summary>
	public class ExporterKeyAuthRequestDTO {
		/// <summary>
		/// The unique name of the app for which a challenge authentication is initiated.
		/// </summary>
		[PlainName]
		[StringLength(128, MinimumLength = 1)]
		public string AppName { get; private set; }
		/// <summary>
		/// The key id of the user's authentication key-pair.
		/// </summary>
		public KeyId KeyId { get; private set; }

		/// <summary>
		/// Constructs a <see cref="ExporterKeyAuthRequestDTO"/> with the given data.
		/// </summary>
		public ExporterKeyAuthRequestDTO(string appName, KeyId keyId) {
			AppName = appName;
			KeyId = keyId;
		}
	}
	/// <summary>
	/// The response data sent from server to client in the second step of the key-pair-based challenge authentication for exporter clients.
	/// This poses the challenge to the client.
	/// </summary>
	public class ExporterKeyAuthChallengeDTO {
		/// <summary>
		/// The unique id for the challenge that the server issues by this message.
		/// Must be provided when submiting a solution for the challenge.
		/// </summary>
		public Guid ChallengeId { get; private set; }
		/// <summary>
		/// A byte sequence that the server generated randomly as a nonce for the challenge.
		/// </summary>
		[MaxLength(64 * 1024)]
		public byte[] ChallengeBytes { get; private set; }
		/// <summary>
		/// The digest that the server instructs the client to use for signing <see cref="ChallengeBytes"/> (and a few other values).
		/// </summary>
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public SignatureDigest DigestAlgorithmToUse { get; private set; }

		/// <summary>
		/// Constructs a <see cref="ExporterKeyAuthChallengeDTO"/> with the given data.
		/// </summary>
		public ExporterKeyAuthChallengeDTO(Guid challengeId, byte[] challengeBytes, SignatureDigest digestAlgorithmToUse) {
			ChallengeId = challengeId;
			ChallengeBytes = challengeBytes;
			DigestAlgorithmToUse = digestAlgorithmToUse;
		}
	}

	/// <summary>
	/// The reply data sent from client to server in the third step of the key-pair-based challenge authentication for exporter clients.
	/// With this, the client submits the completed challenge.
	/// </summary>
	public class ExporterKeyAuthSignatureDTO {
		/// <summary>
		/// The id of the challenge (issued by the server) for which this provides a solution to prove the client's authenticity.
		/// </summary>
		public Guid ChallengeId { get; private set; }

		/// <summary>
		/// Signature over the sequence
		/// <list type="number">
		/// <item><description>
		/// the bytes of <see cref="ExporterKeyAuthChallengeDTO.ChallengeId"/>,
		/// formatted as <c>00000000-0000-0000-0000-000000000000</c> (format string "D") in UTF-8
		/// </description></item>
		/// <item><description>the bytes of <see cref="ExporterKeyAuthRequestDTO.KeyId"/></description></item>
		/// <item><description>the bytes in <see cref="ExporterKeyAuthChallengeDTO.ChallengeBytes"/></description></item>
		/// <item><description>the bytes of <see cref="ExporterKeyAuthRequestDTO.AppName"/> in UTF-8</description></item>
		/// </list>
		///	using <see cref="ExporterKeyAuthChallengeDTO.DigestAlgorithmToUse"/>,
		///	with the appropriate signature algotihm for the key identified by <see cref="ExporterKeyAuthRequestDTO.KeyId"/>.
		/// </summary>
		[MaxLength(64 * 1024)]
		public byte[] Signature { get; private set; }

		/// <summary>
		/// Constructs a <see cref="ExporterKeyAuthSignatureDTO"/> with the given data.
		/// </summary>
		public ExporterKeyAuthSignatureDTO(Guid challengeId, byte[] signature) {
			ChallengeId = challengeId;
			Signature = signature;
		}

		/// <summary>
		/// Constructs the complete byte sequence that is to be signed by to complete the challenge.
		/// While the core component is <see cref="ExporterKeyAuthChallengeDTO.ChallengeBytes"/>,
		/// other values are from the challenge session are included as a precaution, e.g. against the client having sign arbitrary data.
		/// </summary>
		/// <param name="request">The request for opening the challenge protocol, to allow obtaining the required values.</param>
		/// <param name="challenge">The server-issued challenge, to allow obtaining the required values.</param>
		/// <returns></returns>
		public static byte[] ConstructContentToSign(ExporterKeyAuthRequestDTO request, ExporterKeyAuthChallengeDTO challenge) =>
			Encoding.UTF8.GetBytes(challenge.ChallengeId.ToString("D"))
				.Concat(request.KeyId.Id)
				.Concat(challenge.ChallengeBytes)
				.Concat(Encoding.UTF8.GetBytes(request.AppName))
				.ToArray();
	}

	/// <summary>
	/// The response data sent from server to client in the fourth and final step of the key-pair-based challenge authentication for exporter clients.
	/// This provides the client with the session token for further requests.
	/// </summary>
	public class ExporterKeyAuthResponseDTO {
		/// <summary>
		/// The session authorization token issued by the server for the successfully started session.
		/// </summary>
		public AuthorizationToken Token { get; private set; }

		/// <summary>
		/// The timestamp at which <see cref="Token"/> expires.
		/// </summary>
		public DateTime TokenExpiry { get; private set; }

		/// <summary>
		/// Constructs a <see cref="ExporterKeyAuthResponseDTO"/> with the given data.
		/// </summary>
		public ExporterKeyAuthResponseDTO(AuthorizationToken token, DateTime tokenExpiry) {
			Token = token;
			TokenExpiry = tokenExpiry;
		}
	}
}
