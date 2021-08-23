using System;
using System.Collections.Generic;
using System.Text;

namespace SGL.Analytics.DTO {
	public record UserRegistrationDTO(string appName, string Username, Dictionary<string, object?> StudySpecificAttributes);
}
