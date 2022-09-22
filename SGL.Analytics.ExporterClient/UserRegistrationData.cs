using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public class UserRegistrationData {
		/// <summary>
		/// The unique id identifying the user.
		/// </summary>
		public Guid UserId { get; private set; }
		/// <summary>
		/// The Username for the user, can be the string form of the id if none was given during the registration.
		/// </summary>
		public string Username { get; private set; }

		public IReadOnlyDictionary<string, object?> StudySpecificProperties { get; private set; }
		public IReadOnlyDictionary<string, object?> EncryptedStudySpecificProperties { get; private set; }

		internal UserRegistrationData(Guid userId, string username, IReadOnlyDictionary<string, object?> studySpecificProperties, IReadOnlyDictionary<string, object?> encryptedStudySpecificProperties) {
			UserId = userId;
			Username = username;
			StudySpecificProperties = studySpecificProperties;
			EncryptedStudySpecificProperties = encryptedStudySpecificProperties;
		}
	}
}
