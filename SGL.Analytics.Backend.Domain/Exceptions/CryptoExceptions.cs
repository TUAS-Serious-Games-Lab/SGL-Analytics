using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Exceptions {
	public class InvalidCryptographicMetadataException : Exception {
		public InvalidCryptographicMetadataException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}

	public class MissingRecipientDataKeysForEncryptedDataException : InvalidCryptographicMetadataException {
		public MissingRecipientDataKeysForEncryptedDataException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}

	public class EncryptedDataWithoutEncryptionMetadataException : InvalidCryptographicMetadataException {
		public EncryptedDataWithoutEncryptionMetadataException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
}
