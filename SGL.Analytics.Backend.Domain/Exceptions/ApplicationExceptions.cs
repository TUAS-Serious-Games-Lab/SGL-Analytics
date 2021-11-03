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
}
