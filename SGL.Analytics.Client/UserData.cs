using SGL.Analytics.DTO;
using SGL.Analytics.Utilities;

namespace SGL.Analytics.Client {
	public class BaseUserData {
		public string Username { get; set; }

		public BaseUserData(string username) {
			Username = username;
		}

		internal UserRegistrationDTO MakeDTO(string appName, string secret) {
			// Study-specific data are intended to be kept in derived classes.
			// => Map all properties of dynamic type to a dictionary for transmission.
			var studySpecificProperties = DictionaryDataMapping.ToDataMappingDictionary(this);
			studySpecificProperties.Remove("Username");
			return new UserRegistrationDTO(appName, Username, secret, studySpecificProperties);
		}
	}
}
