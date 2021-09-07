using System;

namespace SGL.Analytics.Backend.Domain.Exceptions {
	public class ApplicationDoesNotExistException : Exception {
		public ApplicationDoesNotExistException(string appName, Exception? innerException = null) : base($"The given application with name '{appName}' does not exist.", innerException) {
			AppName = appName;
		}

		public string AppName { get; set; }
	}
}
