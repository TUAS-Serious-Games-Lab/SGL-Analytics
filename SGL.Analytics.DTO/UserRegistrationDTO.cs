using SGL.Utilities;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the client to the server when a client attempts to registers a user.
	/// </summary>
	public record UserRegistrationDTO([PlainName][StringLength(128, MinimumLength = 1)] string AppName, [PlainName][StringLength(64, MinimumLength = 1)] string? Username, [StringLength(128, MinimumLength = 8)] string Secret, [property: JsonConverter(typeof(ObjectDictionaryJsonConverter))] Dictionary<string, object?> StudySpecificProperties);
}
