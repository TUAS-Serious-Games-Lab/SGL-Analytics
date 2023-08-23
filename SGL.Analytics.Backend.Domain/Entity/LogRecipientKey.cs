using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;

namespace SGL.Analytics.Backend.Domain.Entity {
	/// <summary>
	/// Models an authorized recipient key entry for encrypted log files.
	/// </summary>
	public class LogRecipientKey {
		/// <summary>
		/// The id of the log to which this key belongs.
		/// </summary>
		public Guid LogId { get; set; }
		/// <summary>
		/// The public key id of the recipient for which this recipient key is intended.
		/// </summary>
		public KeyId RecipientKeyId { get; set; } = null!;
		/// <summary>
		/// The encryption mode used for the encrypted data key in <see cref="EncryptedKey"/>.
		/// </summary>
		public KeyEncryptionMode EncryptionMode { get; set; }
		/// <summary>
		/// The data key for the log with id <see cref="LogId"/>, encrypted for <see cref="RecipientKeyId"/> using the mode described by <see cref="EncryptionMode"/>.
		/// </summary>
		public byte[] EncryptedKey { get; set; } = new byte[0];
		/// <summary>
		/// If this recipient key uses <see cref="KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM"/> encryption, stores the per-log public key, unless it uses the shared key in <see cref="LogMetadata.SharedLogPublicKey"/>.
		/// </summary>
		public byte[]? LogPublicKey { get; set; }

		/// <summary>
		/// Converts this entity object to a <see cref="DataKeyInfo"/> object for SGL.Utilities.
		/// </summary>
		/// <returns>A <see cref="DataKeyInfo"/> encapsulating the data of this object.</returns>
		public DataKeyInfo ToDataKeyInfo() => new DataKeyInfo { Mode = EncryptionMode, EncryptedKey = EncryptedKey, MessagePublicKey = LogPublicKey };
	}
}