using SGL.Analytics.Utilities;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the data transferred from the client to the server when a client attempts to registers a user.
	/// </summary>
	public record UserRegistrationDTO(string AppName, string Username, string Secret, [property: JsonConverter(typeof(ObjectDictionaryJsonConverter))] Dictionary<string, object?> StudySpecificProperties);
}
