using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;

namespace SGL.Analytics.Backend.Domain.Entity {
	public class UserRegistrationPropertyRecipientKey {
		/// <summary>
		/// The id of the user registration to which this key belongs.
		/// </summary>
		public Guid UserId { get; set; }
		/// <summary>
		/// The public key id of the recipient for which this recipient key is intended.
		/// </summary>
		public KeyId RecipientKeyId { get; set; }
		/// <summary>
		/// The encryption mode used for the encrypted data key in <see cref="EncryptedKey"/>.
		/// </summary>
		public KeyEncryptionMode EncryptionMode { get; set; }
		/// <summary>
		/// The data key for the encrypted registration properties of the user with id <see cref="UserId"/>, encrypted for <see cref="RecipientKeyId"/> using the mode described by <see cref="EncryptionMode"/>.
		/// </summary>
		public byte[] EncryptedKey { get; set; }
		/// <summary>
		/// If this recipient key uses <see cref="KeyEncryptionMode.ECDH_KDF2_SHA256_AES_256_CCM"/> encryption, stores the per-user public key, unless it uses the shared key in <see cref="UserRegistration.PropertySharedPublicKey"/>.
		/// </summary>
		public byte[]? UserPropertiesPublicKey { get; set; }

		/// <summary>
		/// Converts this entity object to a <see cref="DataKeyInfo"/> object for SGL.Utilities.
		/// </summary>
		/// <returns>A <see cref="DataKeyInfo"/> encapsulating the data of this object.</returns>
		public DataKeyInfo ToDataKeyInfo() => new DataKeyInfo { Mode = EncryptionMode, EncryptedKey = EncryptedKey, MessagePublicKey = UserPropertiesPublicKey };
	}
}