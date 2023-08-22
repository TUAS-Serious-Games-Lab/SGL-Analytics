using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Exceptions {
	/// <summary>
	/// An exception type thrown when invalid cryptographic metadata are encountered.
	/// </summary>
	public class InvalidCryptographicMetadataException : Exception {
		/// <summary>
		/// Creates a new exception object with the given data.
		/// </summary>
		public InvalidCryptographicMetadataException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}

	/// <summary>
	/// An exception type thrown when an object with encrypted data is submitted without supplying the required key material.
	/// </summary>
	public class MissingRecipientDataKeysForEncryptedDataException : InvalidCryptographicMetadataException {
		/// <summary>
		/// Creates a new exception object with the given data.
		/// </summary>
		public MissingRecipientDataKeysForEncryptedDataException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}

	/// <summary>
	/// An exception type thrown when an object with encrypted data is submitted without valid encryption metadata.
	/// </summary>
	public class EncryptedDataWithoutEncryptionMetadataException : InvalidCryptographicMetadataException {
		/// <summary>
		/// Creates a new exception object with the given data.
		/// </summary>
		public EncryptedDataWithoutEncryptionMetadataException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
}
