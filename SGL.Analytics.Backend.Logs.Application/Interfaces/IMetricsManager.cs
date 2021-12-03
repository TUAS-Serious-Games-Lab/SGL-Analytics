using System;
using System.Collections.Generic;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	public interface IMetricsManager {
		void HandleLogFileTooLargeError(string appName);
		void HandleUnknownAppError(string appName);
		void HandleIncorrectAppApiTokenError(string appName);
		void HandleIncorrectSecurityTokenClaimsError();
		void HandleUnexpectedError(string appName, Exception ex);
		void HandleLogIdConflictWarning(string appName);
		void HandleLogUploadRetryWarning(string appName);
		void HandleRetryingCompletedUploadWarning(string appName);
		void HandleUploadRetryChangedSuffixWarning(string appName);
		void HandleUploadRetryChangedEncodingWarning(string appName);
		void HandleLogUploadedSuccessfully(string appName);
		void UpdateCollectedLogs(IDictionary<string, int> perAppCounts);
	}

	public class NullMetricsManager : IMetricsManager {
		public void HandleIncorrectAppApiTokenError(string appName) { }
		public void HandleIncorrectSecurityTokenClaimsError() { }
		public void HandleLogFileTooLargeError(string appName) { }
		public void HandleLogIdConflictWarning(string appName) { }
		public void HandleLogUploadedSuccessfully(string appName) { }
		public void HandleLogUploadRetryWarning(string appName) { }
		public void HandleRetryingCompletedUploadWarning(string appName) { }
		public void HandleUnexpectedError(string appName, Exception ex) { }
		public void HandleUnknownAppError(string appName) { }
		public void HandleUploadRetryChangedEncodingWarning(string appName) { }
		public void HandleUploadRetryChangedSuffixWarning(string appName) { }
		public void UpdateCollectedLogs(IDictionary<string, int> perAppCounts) { }
	}
}
