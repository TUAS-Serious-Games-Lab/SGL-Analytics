using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	/// <summary>
	/// Encapsulates the metadata about an exportable log file.
	/// </summary>
	public class UserRegistrationData {
		/// <summary>
		/// The unique id identifying the user.
		/// </summary>
		public Guid UserId { get; private set; }
		/// <summary>
		/// The Username for the user, can be the string form of the id if none was given during the registration.
		/// </summary>
		public string Username { get; private set; }

		/// <summary>
		/// The unencrypted application-/study-specific properties of the user registration.
		/// </summary>
		public IReadOnlyDictionary<string, object?> StudySpecificProperties { get; private set; }
		/// <summary>
		/// A decrypted view of the encrypted application-/study-specific properties of the user registration.
		/// If the user has no encrypted properties, an empty dictionary is provided.
		/// A <see langword="null"/> value indicates that the data could not be decrypted or the decrypted data couldn't be parsed as valid JSON.
		/// </summary>
		public IReadOnlyDictionary<string, object?>? DecryptedStudySpecificProperties { get; private set; }

		internal UserRegistrationData(Guid userId, string username, IReadOnlyDictionary<string, object?> studySpecificProperties, IReadOnlyDictionary<string, object?>? decryptedStudySpecificProperties) {
			UserId = userId;
			Username = username;
			StudySpecificProperties = studySpecificProperties;
			DecryptedStudySpecificProperties = decryptedStudySpecificProperties;
		}
	}
}
