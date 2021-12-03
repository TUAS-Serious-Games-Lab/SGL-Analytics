using System;
using System.Collections.Generic;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	public interface IMetricsManager {
		void EnsureMetricsExist(string appName);
		void HandleUnknownAppError(string appName);
		void HandleIncorrectAppApiTokenError(string appName);
		void HandleNonexistentUsernameError(string appName);
		void HandleNonexistentUserIdError(string appName);
		void HandleIncorrectUserSecretError(string appName);
		void HandleUserIdAppMismatchError(string appName);
		void HandleUserPropertyValiidationError(string appName);
		void HandleConcurrencyConflictError(string appName);
		void HandleUniquenessConflictError(string appName);
		void HandleUsernameAlreadyTakenError(string appName);
		void HandleUnexpectedError(string appName, Exception ex);
		void HandleSuccessfulLogin(string appName);
		void HandleSuccessfulRegistration(string appName);
		void UpdateRegisteredUsers(IDictionary<string, int> perAppCounts);
	}
	public class NullMetricsManager : IMetricsManager {
		public void EnsureMetricsExist(string appName) { }
		public void HandleConcurrencyConflictError(string appName) { }
		public void HandleIncorrectAppApiTokenError(string appName) { }
		public void HandleIncorrectUserSecretError(string appName) { }
		public void HandleNonexistentUserIdError(string appName) { }
		public void HandleNonexistentUsernameError(string appName) { }
		public void HandleSuccessfulLogin(string appName) { }
		public void HandleSuccessfulRegistration(string appName) { }
		public void HandleUnexpectedError(string appName, Exception ex) { }
		public void HandleUniquenessConflictError(string appName) { }
		public void HandleUnknownAppError(string appName) { }
		public void HandleUserIdAppMismatchError(string appName) { }
		public void HandleUsernameAlreadyTakenError(string appName) { }
		public void HandleUserPropertyValiidationError(string appName) { }
		public void UpdateRegisteredUsers(IDictionary<string, int> perAppCounts) { }
	}
}
