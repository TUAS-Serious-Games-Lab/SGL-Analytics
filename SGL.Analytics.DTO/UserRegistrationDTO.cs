using SGL.Analytics.Utilities;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	public record UserRegistrationDTO(string AppName, string Username, [property: JsonConverter(typeof(ObjectDictionaryJsonConverter))] Dictionary<string, object?> StudySpecificProperties);
}
