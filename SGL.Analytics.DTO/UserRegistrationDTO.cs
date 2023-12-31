using SGL.Utilities;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Validation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the client to the server when a client attempts to register a user.
	/// </summary>
	public class UserRegistrationDTO {
		/// <summary>
		/// The unique technical name of the client application performing the registration.
		/// </summary>
		[PlainName]
		[StringLength(128, MinimumLength = 1)]
		public string AppName { get; private set; }
		/// <summary>
		/// A username that can optionally be used by the client application.
		/// If it is left out, the application should perform logins using the user id obtained from the <see cref="UserRegistrationResultDTO"/> returned from the registration API call.
		/// If it is specified, both options, user id as well as username can be used later.
		/// </summary>
		[PlainName(allowBrackets: true)]
		[StringLength(64, MinimumLength = 1)]
		public string? Username { get; private set; }
		/// <summary>
		/// A secret string for the user, used to authenticate them later, when logging-in.
		/// This can be an auto-generated random string or a user-specified password, depending on the application.
		/// If it is null, a federated user account shall be registered, where authentication is done by handing a token to a configured upstream backend for verification.
		/// </summary>
		[StringLength(128, MinimumLength = 8)]
		public string? Secret { get; private set; }
		/// <summary>
		/// If set, indicates that the user registration shall use delegated authentication instead of direct credentials.
		/// It then contains an <c>Authorization</c> header value to pass to the upstream backend for authentication.
		/// </summary>
		public string? UpstreamAuthorizationHeader { get; private set; }
		/// <summary>
		/// A dictionary containing application-/study-specific properties that should be stored with the user registration.
		/// Although the DTO can store quite arbitrary data, as the entry values can again be dictionaries or lists, the properties are validated by the backend against the defined properties for the application (as indicated by <see cref="AppName"/>).
		/// Only those registrations are accepted where all submitted properties are defined in the backend with a matching type for the value and all required properties in the backend are present in the submitted DTO.
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
		/// Creates a new DTO with the given data and without encrypted properties and without upstream delegation.
		/// </summary>
		/// <param name="appName">The unique technical name of the client application performing the registration.</param>
		/// <param name="username"> A username that can optionally be used by the client application.</param>
		/// <param name="secret">A secret string for the user, used to authenticate them later, when logging-in.</param>
		/// <param name="studySpecificProperties">A dictionary containing application-/study-specific properties that should be stored with the user registration.</param>
		/// <remarks>
		/// Kept for backwards compatibility, use the full overload for full functionality.
		/// </remarks>
		public UserRegistrationDTO([PlainName][StringLength(128, MinimumLength = 1)] string appName,
			[PlainName(allowBrackets: true)][StringLength(64, MinimumLength = 1)] string? username,
			[StringLength(128, MinimumLength = 8)] string? secret,
			Dictionary<string, object?> studySpecificProperties) :
			this(appName, username, secret, null, studySpecificProperties, null, null) { }

		/// <summary>
		/// Creates a new DTO with the given data.
		/// </summary>
		/// <param name="appName">The unique technical name of the client application performing the registration.</param>
		/// <param name="username"> A username that can optionally be used by the client application.</param>
		/// <param name="secret">A secret string for the user, used to authenticate them later, when logging-in.</param>
		/// <param name="upstreamAuthorizationHeader">
		/// If set, indicates that the user registration shall use delegated authentication instead of direct credentials.
		/// It then contains an <c>Authorization</c> header value to pass to the upstream backend for authentication.
		/// </param>
		/// <param name="studySpecificProperties">A dictionary containing application-/study-specific properties that should be stored with the user registration.</param>
		/// <param name="encryptedProperties">The encrypted app-specific properties for the user registration.</param>
		/// <param name="propertyEncryptionInfo">Information about how <paramref name="encryptedProperties"/> is encrypted and associated key material.</param>
		[JsonConstructor]
		public UserRegistrationDTO([PlainName][StringLength(128, MinimumLength = 1)] string appName,
			[PlainName(allowBrackets: true)][StringLength(64, MinimumLength = 1)] string? username,
			[StringLength(128, MinimumLength = 8)] string? secret, string? upstreamAuthorizationHeader,
			Dictionary<string, object?> studySpecificProperties, byte[]? encryptedProperties, EncryptionInfo? propertyEncryptionInfo) =>
			(AppName, Username, Secret, UpstreamAuthorizationHeader, StudySpecificProperties, EncryptedProperties, PropertyEncryptionInfo) =
			(appName, username, secret, upstreamAuthorizationHeader, studySpecificProperties, encryptedProperties, propertyEncryptionInfo);

		/// <summary>
		/// Deconstructs the DTO into the contained data.
		/// </summary>
		/// <param name="appName">The unique technical name of the client application performing the registration.</param>
		/// <param name="username"> A username that can optionally be used by the client application.</param>
		/// <param name="secret">A secret string for the user, used to authenticate them later, when logging-in.</param>
		/// <param name="upstreamAuthorizationToken">
		/// If set, indicates that the user registration shall use delegated authentication instead of direct credentials.
		/// It then contains an <c>Authorization</c> header value to pass to the upstream backend for authentication.
		/// </param>
		/// <param name="studySpecificProperties">A dictionary containing application-/study-specific properties that should be stored with the user registration.</param>
		/// <param name="encryptedProperties">The encrypted app-specific properties for the user registration.</param>
		/// <param name="propertyEncryptionInfo">Information about how <paramref name="encryptedProperties"/> is encrypted and associated key material.</param>
		public void Deconstruct(out string appName, out string? username, out string? secret, out string? upstreamAuthorizationToken,
			out Dictionary<string, object?> studySpecificProperties, out byte[]? encryptedProperties, out EncryptionInfo? propertyEncryptionInfo) {
			appName = AppName;
			username = Username;
			secret = Secret;
			upstreamAuthorizationToken = UpstreamAuthorizationHeader;
			studySpecificProperties = StudySpecificProperties;
			encryptedProperties = EncryptedProperties;
			propertyEncryptionInfo = PropertyEncryptionInfo;
		}
	}
}
