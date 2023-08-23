using SGL.Utilities;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data provided about a user registration to an exporter client.
	/// </summary>
	public class UserMetadataDTO {
		/// <summary>
		/// The unique id identifying the user.
		/// </summary>
		public Guid UserId { get; private set; }

		/// <summary>
		/// The Username for the user, can be the string form of the id if none was given during the registration.
		/// </summary>
		[PlainName(allowBrackets: true)]
		[StringLength(64, MinimumLength = 1)]
		public string Username { get; private set; }
		/// <summary>
		/// A dictionary containing application-/study-specific properties that are stored with the user registration.
		/// Although the DTO can store quite arbitrary data, as the entry values can again be dictionaries or lists, the properties are validated by the backend against the defined properties for the application.
		/// </summary>
		[JsonConverter(typeof(ObjectDictionaryJsonConverter))]
		public Dictionary<string, object?> StudySpecificProperties { get; private set; }
		/// <summary>
		/// Contains the encrypted application-/study-specific user registration properties as encrypted, gzipped JSON.
		/// The used encryption mode and required key material is described by <see cref="PropertyEncryptionInfo"/>.
		/// </summary>
		public byte[]? EncryptedProperties { get; private set; }
		/// <summary>
		/// Decribes how <see cref="EncryptedProperties"/> is encrypted and contains the required key material, e.g. encrypted data keys.
		/// </summary>
		public EncryptionInfo? PropertyEncryptionInfo { get; private set; }

		/// <summary>
		/// Constructs a <see cref="UserMetadataDTO"/> with the given data.
		/// </summary>
		public UserMetadataDTO(Guid userId, string username, Dictionary<string, object?> studySpecificProperties,
				byte[]? encryptedProperties, EncryptionInfo? propertyEncryptionInfo) {
			UserId = userId;
			Username = username;
			StudySpecificProperties = studySpecificProperties;
			EncryptedProperties = encryptedProperties;
			PropertyEncryptionInfo = propertyEncryptionInfo;
		}
	}
}
