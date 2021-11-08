using SGL.Analytics.DTO;
using SGL.Utilities;

namespace SGL.Analytics.Client {

	/// <summary>
	/// Acts as the base class for user data classes provided by applications for the user registration.
	///	The properties of derived classes are mapped for transport using <see cref="DictionaryDataMapping.ToDataMappingDictionary(object)"/>
	///	and must also be defined in the application registration in the backend.
	/// </summary>
	public class BaseUserData {
		/// <summary>
		/// The username to identify the user.
		/// </summary>
		public string Username { get; set; }

		/// <summary>
		/// Instaniates the base class object using the given username.
		/// </summary>
		/// <param name="username">The username to store in the object.</param>
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
