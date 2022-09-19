using SGL.Utilities;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	public class UserMetadataDTO {
		/// <summary>
		/// The unique id identifying the user.
		/// </summary>
		public Guid UserId { get; private set; }

		/// <summary>
		/// The unique technical name of the client application with which the user is assoicated.
		/// </summary>
		[PlainName]
		[StringLength(128, MinimumLength = 1)]
		public string AppName { get; private set; }
		/// <summary>
		/// The Username for the user, can be the string form of the id if none was given during the registration.
		/// </summary>
		[PlainName(allowBrackets: true)]
		[StringLength(64, MinimumLength = 1)]
		public string Username { get; private set; }
		/// <summary>
		/// A dictionary containing application-/study-specific properties that should be stored with the user registration.
		/// Although the DTO can store quite arbitrary data, as the entry values can again be dictionaries or lists, the properties are validated by the backend against the defined properties for the application (as indicated by <see cref="AppName"/>).
		/// Only those registrations are accepted where all submitted properties are defined in the backend with a matching type for the value and all required properties in the backend are present in the submitted DTO.
		/// </summary>
		[JsonConverter(typeof(ObjectDictionaryJsonConverter))]
		public Dictionary<string, object?> StudySpecificProperties { get; private set; }

		public byte[]? EncryptedProperties { get; private set; }
		public EncryptionInfo? PropertyEncryptionInfo { get; private set; }

	}
}
