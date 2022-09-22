using System;
using System.Collections.Generic;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	/// <summary>
	/// Provides an interface to decouple the metrics collection form the components that perform the operations meassured by the metrics.
	/// This interface should be implemented by a singleton service class that is injected into the source components.
	/// The components then call the appropriate methods to signal that the associated metrics should be updated.
	/// The implementations then define how these calls should translate to the metrics collection library in use.
	/// </summary>
	public interface IMetricsManager {
		/// <summary>
		/// Allows the implementation to ensure that the appropriate metrics entries for the given application exist.
		/// What metrics are affected by this is defined by the implementation, but typically counters should be ensured to exist, initializing them to zero on creation.
		/// Other metrics type may also be sinsible to cover here, but e.g. timestamp gauges may be better created on use, thus initially providing no value instead of providing Epoch or the startup time, which could both be confusing.
		/// </summary>
		/// <param name="appName">The app for which to ensure existence of relevant metrics objects.</param>
		void EnsureMetricsExist(string appName);
		/// <summary>
		/// Called when an error is caused because an uploaded log file is larger than the size limit and the upload was thus rejected.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleLogFileTooLargeError(string appName);
		/// <summary>
		/// Called when an error is caused because the client specified an application name on access that is not known to the service.
		/// </summary>
		/// <param name="appName">The unknown application name given for the request</param>
		void HandleUnknownAppError(string appName);
		/// <summary>
		/// Called when an error is caused because the client specified an incorrect API token for the specified application.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleIncorrectAppApiTokenError(string appName);
		/// <summary>
		/// Called when an error is caused when attempting to extract claims data from the supplied security token.
		/// No <c>appName</c> parameter is taken, because the triggering component can't know the app name, as it would normally extract it from the token, which is what failed here.
		/// </summary>
		void HandleIncorrectSecurityTokenClaimsError();
		/// <summary>
		/// Called when an error is caused during model state validation due to incorrect request parameters.
		/// No <c>appName</c> parameter is taken, as the appName is part of the model state that was not valid.
		/// Instead, the error message is given to allow implementations to use per error type counters if desired.
		/// </summary>
		void HandleModelStateValidationError(string errorMessage);
		/// <summary>
		/// Called when a problem with cryptographic metadata was encountered.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleCryptoMetadataError(string appName);
		/// <summary>
		/// Called when an unexpected error is caused, i.e. one that is not known ahead of time and thus doesn't have a separate <c>Handle</c>... method.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		/// <param name="ex">The exception object representing the error condition.</param>
		void HandleUnexpectedError(string appName, Exception ex);
		/// <summary>
		/// Called when a warning is issued because an uploaded log file had a conflicting id.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleLogIdConflictWarning(string appName);
		/// <summary>
		/// Called when a warning is issued because an uploaded log file upload was retried.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleLogUploadRetryWarning(string appName);
		/// <summary>
		/// Called when a warning is issued because a log file upload was retried despite the previous upload already having completed.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleRetryingCompletedUploadWarning(string appName);
		/// <summary>
		/// Called when a warning is issued because a retried log file upload changed the file name suffix compared to the last upload attempt.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleUploadRetryChangedSuffixWarning(string appName);
		/// <summary>
		/// Called when a warning is issued because a retried log file upload changed the content encoding compared to the last upload attempt.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleUploadRetryChangedEncodingWarning(string appName);
		/// <summary>
		/// Called when a log file upload completed successfully.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleLogUploadedSuccessfully(string appName);
		/// <summary>
		/// Called when a log file was successfully uploaded, specifying its size.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		/// <param name="size">The size of the uploaded log file content.</param>
		void ObserveIngestedLogFileSize(string appName, long size);
		/// <summary>
		/// Periodically called with the number of log files for each application in the database.
		/// </summary>
		/// <param name="perAppCounts">A dictionary mapping the application names to corresponding numbers of log files.</param>
		void UpdateCollectedLogs(IDictionary<string, int> perAppCounts);
		/// <summary>
		/// Periodically called with the average size of the log files for each application in the database.
		/// </summary>
		/// <param name="perAppSizes">A dictionary mapping the application names to corresponding average size.</param>
		void UpdateAvgLogSize(IDictionary<string, double> perAppSizes);
		void HandleLogNotFoundError(string appName);
	}

	/// <summary>
	/// Provides a null implementation of <see cref="IMetricsManager"/> where the methods do nothing and thus no metrics are actually collected.
	/// </summary>
	public class NullMetricsManager : IMetricsManager {
		/// <inheritdoc/>
		public void EnsureMetricsExist(string appName) { }

		/// <inheritdoc/>
		public void HandleCryptoMetadataError(string appName) { }
		/// <inheritdoc/>
		public void HandleIncorrectAppApiTokenError(string appName) { }
		/// <inheritdoc/>
		public void HandleIncorrectSecurityTokenClaimsError() { }
		/// <inheritdoc/>
		public void HandleLogFileTooLargeError(string appName) { }
		/// <inheritdoc/>
		public void HandleLogIdConflictWarning(string appName) { }
		/// <inheritdoc/>
		public void HandleLogUploadedSuccessfully(string appName) { }
		/// <inheritdoc/>
		public void HandleLogUploadRetryWarning(string appName) { }
		/// <inheritdoc/>
		public void HandleModelStateValidationError(string errorMessage) { }
		/// <inheritdoc/>
		public void HandleRetryingCompletedUploadWarning(string appName) { }
		/// <inheritdoc/>
		public void HandleUnexpectedError(string appName, Exception ex) { }
		/// <inheritdoc/>
		public void HandleUnknownAppError(string appName) { }
		/// <inheritdoc/>
		public void HandleLogNotFoundError(string appName) { }
		/// <inheritdoc/>
		public void HandleUploadRetryChangedEncodingWarning(string appName) { }
		/// <inheritdoc/>
		public void HandleUploadRetryChangedSuffixWarning(string appName) { }
		/// <inheritdoc/>
		public void ObserveIngestedLogFileSize(string appName, long size) { }
		/// <inheritdoc/>
		public void UpdateAvgLogSize(IDictionary<string, double> perAppSizes) { }
		/// <inheritdoc/>
		public void UpdateCollectedLogs(IDictionary<string, int> perAppCounts) { }
	}
}
