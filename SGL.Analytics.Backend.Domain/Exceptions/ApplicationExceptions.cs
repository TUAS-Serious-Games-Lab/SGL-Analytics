using System;

namespace SGL.Analytics.Backend.Domain.Exceptions {
	/// <summary>
	/// The exception type thrown by management logic services if the application associated with an operation does not exist.
	/// </summary>
	public class ApplicationDoesNotExistException : Exception {
		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public ApplicationDoesNotExistException(string appName, Exception? innerException = null) : base($"The given application with name '{appName}' does not exist.", innerException) {
			AppName = appName;
		}

		/// <summary>
		/// The supplied technical name of the application that didn't exist.
		/// </summary>
		public string AppName { get; set; }
	}
	/// <summary>
	/// The exception type thrown by management logic services if the application API token given for a request didn't match the indicated application.
	/// </summary>
	public class ApplicationApiTokenMismatchException : Exception {
		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public ApplicationApiTokenMismatchException(string appName, string appApiToken, Exception? innerException = null) : base($"The given application API token didn't match the given app '{appName}'.", innerException) {
			AppName = appName;
			AppApiToken = appApiToken;
		}

		/// <summary>
		/// The supplied technical name of the application.
		/// </summary>
		public string AppName { get; set; }
		/// <summary>
		/// The incorrect API token.
		/// </summary>
		public string AppApiToken { get; }
	}

	public class MissingRecipientDataKeysForEncryptedDataException : Exception {
		public MissingRecipientDataKeysForEncryptedDataException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
}
