using SGL.Utilities;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the client to the server when a client attempts to registers a user.
	/// </summary>
	public record UserRegistrationDTO {
		[PlainName]
		[StringLength(128, MinimumLength = 1)]
		public string AppName { get; init; }
		[PlainName(allowBrackets: true)]
		[StringLength(64, MinimumLength = 1)]
		public string? Username { get; init; }
		[StringLength(128, MinimumLength = 8)]
		public string Secret { get; init; }
		[JsonConverter(typeof(ObjectDictionaryJsonConverter))]
		public Dictionary<string, object?> StudySpecificProperties { get; init; }

		public UserRegistrationDTO([PlainName][StringLength(128, MinimumLength = 1)] string appName,
			[PlainName(allowBrackets: true)][StringLength(64, MinimumLength = 1)] string? username,
			[StringLength(128, MinimumLength = 8)] string secret, Dictionary<string, object?> studySpecificProperties) =>
			(AppName, Username, Secret, StudySpecificProperties) = (appName, username, secret, studySpecificProperties);

		public void Deconstruct(out string appName, out string? username, out string secret, out Dictionary<string, object?> studySpecificProperties) {
			appName = AppName;
			username = Username;
			secret = Secret;
			studySpecificProperties = StudySpecificProperties;
		}
	}
}
