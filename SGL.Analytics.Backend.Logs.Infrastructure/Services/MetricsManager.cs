using Prometheus;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Utilities.PrometheusNet;
using System;
using System.Collections.Generic;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {
	public class MetricsManager : IMetricsManager {
		private static readonly Counter errorCounter = Metrics.CreateCounter("sgla_errors_total", "Number of service-level errors encountered by SGL Analytics, labeled by error type and app.", "type", "app");
		private static readonly Counter warningCounter = Metrics.CreateCounter("sgla_warnings_total", "Number of service-level warnings encountered by SGL Analytics, labeled by warning type and app.", "type", "app");
		private static readonly Gauge lastLogUploadTime = Metrics.CreateGauge("sgla_last_log_upload_time_seconds", "Unix timestamp of the last successful log upload for the labeled app (in UTC).", "app");
		private static readonly Gauge logsCollected = Metrics.CreateGauge("sgla_logs_collected", "Number of log files already collected by SGL Analytics Log Collector service.", "app");
		private const string ERROR_LOG_FILE_TOO_LARGE = "Log file too large";
		private const string ERROR_UNKNOWN_APP = "Unknown app";
		private const string ERROR_INCORRECT_APP_API_TOKEN = "Incorrect app API token";
		private const string ERROR_INCORRECT_SECURITY_TOKEN_CLAIMS = "Incorrect security token claims";
		private const string WARNING_LOG_ID_CONFLICT = "Conflicting log id";
		private const string WARNING_LOG_UPLOAD_RETRY = "Retrying failed log upload";
		private const string WARNING_RETRYING_COMPLETED_UPLOAD = "Retrying completed log upload";
		private const string WARNING_UPLOAD_RETRY_CHANGED_SUFFIX = "Upload retry changed file suffix";
		private const string WARNING_UPLOAD_RETRY_CHANGED_ENCODING = "Upload retry changed file encoding";

		public MetricsManager() {
			errorCounter.WithLabels(ERROR_INCORRECT_SECURITY_TOKEN_CLAIMS, "");
		}

		public void EnsureMetricsExist(string appName) {
			logsCollected.WithLabels(appName);
			errorCounter.WithLabels(ERROR_UNKNOWN_APP, appName);
			errorCounter.WithLabels(ERROR_INCORRECT_APP_API_TOKEN, appName);
			errorCounter.WithLabels(ERROR_LOG_FILE_TOO_LARGE, appName);
			warningCounter.WithLabels(WARNING_LOG_ID_CONFLICT, appName);
			warningCounter.WithLabels(WARNING_LOG_UPLOAD_RETRY, appName);
			warningCounter.WithLabels(WARNING_RETRYING_COMPLETED_UPLOAD, appName);
			warningCounter.WithLabels(WARNING_UPLOAD_RETRY_CHANGED_SUFFIX, appName);
			warningCounter.WithLabels(WARNING_UPLOAD_RETRY_CHANGED_ENCODING, appName);
		}

		public void HandleIncorrectAppApiTokenError(string appName) {
			errorCounter.WithLabels(ERROR_INCORRECT_APP_API_TOKEN, appName).Inc();
		}

		public void HandleIncorrectSecurityTokenClaimsError() {
			errorCounter.WithLabels(ERROR_INCORRECT_SECURITY_TOKEN_CLAIMS, "").Inc();
		}

		public void HandleLogFileTooLargeError(string appName) {
			errorCounter.WithLabels(ERROR_LOG_FILE_TOO_LARGE, appName).Inc();
		}

		public void HandleLogUploadedSuccessfully(string appName) {
			lastLogUploadTime.WithLabels(appName).IncToCurrentTimeUtc();
		}

		public void HandleUnexpectedError(string appName, Exception ex) {
			errorCounter.WithLabels(ex.GetType().FullName ?? "unknown", appName).Inc();
		}

		public void HandleUnknownAppError(string appName) {
			errorCounter.WithLabels(ERROR_UNKNOWN_APP, appName).Inc();
		}

		public void UpdateCollectedLogs(IDictionary<string, int> perAppCounts) {
			logsCollected.UpdateLabeledValues(perAppCounts);
		}

		public void HandleLogIdConflictWarning(string appName) {
			warningCounter.WithLabels(WARNING_LOG_ID_CONFLICT, appName).Inc();
		}

		public void HandleLogUploadRetryWarning(string appName) {
			warningCounter.WithLabels(WARNING_LOG_UPLOAD_RETRY, appName).Inc();
		}

		public void HandleRetryingCompletedUploadWarning(string appName) {
			warningCounter.WithLabels(WARNING_RETRYING_COMPLETED_UPLOAD, appName).Inc();
		}

		public void HandleUploadRetryChangedSuffixWarning(string appName) {
			warningCounter.WithLabels(WARNING_UPLOAD_RETRY_CHANGED_SUFFIX, appName).Inc();
		}

		public void HandleUploadRetryChangedEncodingWarning(string appName) {
			warningCounter.WithLabels(WARNING_UPLOAD_RETRY_CHANGED_ENCODING, appName).Inc();
		}
	}
}
