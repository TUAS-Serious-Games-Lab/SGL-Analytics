using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.EndToEnd;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	/// <summary>
	/// When applied to a property in a class derived from <see cref="BaseUserData"/>, indicates that the property shall be submitted and stored in unencrypted form,
	/// this is in contrast to the default of storing properties in the end-to-end encrypted properties of the user registration, which happens for all unmarked properties.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class UnencryptedUserPropertyAttribute : Attribute { }

	/// <summary>
	/// Acts as the base class for user data classes provided by applications for the user registration.
	///	The properties of derived classes are mapped for transport using <see cref="DictionaryDataMapping.ToDataMappingDictionary(object)"/>
	///	and must also be defined in the application registration in the backend.
	/// </summary>
	public class BaseUserData {
		/// <summary>
		/// The username to identify the user.
		/// </summary>
		public string? Username { get; set; }

		/// <summary>
		/// Instaniates the base class object using the given username.
		/// </summary>
		/// <param name="username">The username to store in the object.</param>
		public BaseUserData(string? username) {
			Username = username;
		}

		/// <summary>
		/// Instaniates the base class object without a username.
		/// </summary>
		public BaseUserData() {
			Username = null;
		}

		internal (Dictionary<string, object?> Plain, Dictionary<string, object?> Encrypted) BuildUserProperties() {
			// Study-specific data are intended to be kept in derived classes.
			// => Map all properties of dynamic type to a dictionary for transmission.
			var studySpecificProperties = DictionaryDataMapping.ToDataMappingDictionary(this, prop => prop.GetCustomAttributes<UnencryptedUserPropertyAttribute>().Any());
			var encryptedProperties = DictionaryDataMapping.ToDataMappingDictionary(this, prop => !prop.GetCustomAttributes<UnencryptedUserPropertyAttribute>().Any());
			studySpecificProperties.Remove(nameof(Username));
			encryptedProperties.Remove(nameof(Username));
			return (studySpecificProperties, encryptedProperties);
		}
	}
}
