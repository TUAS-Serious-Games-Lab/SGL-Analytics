using Prometheus;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Utilities.PrometheusNet;
using System;
using System.Collections.Generic;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {
	/// <summary>
	/// Implements <see cref="IMetricsManager"/> using Prometheus-net metrics.
	/// </summary>
	public class MetricsManager : IMetricsManager {
		private static readonly Counter errorCounter = Metrics.CreateCounter("sgla_errors_total", "Number of service-level errors encountered by SGL Analytics, labeled by error type and app.", "type", "app");
		private static readonly Counter warningCounter = Metrics.CreateCounter("sgla_warnings_total", "Number of service-level warnings encountered by SGL Analytics, labeled by warning type and app.", "type", "app");
		private static readonly Gauge lastLogUploadTime = Metrics.CreateGauge("sgla_last_log_upload_time_seconds", "Unix timestamp of the last successful log upload for the labeled app (in UTC).", "app");
		private static readonly Gauge logsCollected = Metrics.CreateGauge("sgla_logs_collected", "The Number of log files already collected by SGL Analytics Log Collector service according to its database.", "app");
		private static readonly Gauge logsAvgSize = Metrics.CreateGauge("sgla_logs_collected_avg_size_bytes", "The average size of the log files already collected by SGL Analytics Log Collector service according to its database.", "app");
		private static readonly Histogram logSizes = Metrics.CreateHistogram("sgla_logs_uploaded_size_bytes",
			"A histogram of the sizes of log files ingested by this SGL Analytics Log Collector service process.",
			new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(512, 2, 20), LabelNames = new[] { "app" } });
		private const string ERROR_LOG_FILE_TOO_LARGE = "Log file too large";
		private const string ERROR_UNKNOWN_APP = "Unknown app";
		private const string ERROR_INVALID_CRYPTO_METADATA = "Invalid cryptographic metadata";
		private const string ERROR_INCORRECT_APP_API_TOKEN = "Incorrect app API token";
		private const string ERROR_INCORRECT_SECURITY_TOKEN_CLAIMS = "Incorrect security token claims";
		private const string ERROR_MODEL_STATE_VALIDATION_FAILED = "Model state validation failed";
		private const string WARNING_LOG_ID_CONFLICT = "Conflicting log id";
		private const string WARNING_LOG_UPLOAD_RETRY = "Retrying failed log upload";
		private const string WARNING_RETRYING_COMPLETED_UPLOAD = "Retrying completed log upload";
		private const string WARNING_UPLOAD_RETRY_CHANGED_SUFFIX = "Upload retry changed file suffix";
		private const string WARNING_UPLOAD_RETRY_CHANGED_ENCODING = "Upload retry changed file encoding";

		/// <summary>
		/// Initializes counter objects not associated with a specific app.
		/// </summary>
		public MetricsManager() {
			errorCounter.WithLabels(ERROR_INCORRECT_SECURITY_TOKEN_CLAIMS, "");
			errorCounter.WithLabels(ERROR_MODEL_STATE_VALIDATION_FAILED, "");
		}

		/// <inheritdoc/>
		public void EnsureMetricsExist(string appName) {
			logsCollected.WithLabels(appName);
			logsAvgSize.WithLabels(appName);
			logSizes.WithLabels(appName);
			errorCounter.WithLabels(ERROR_INCORRECT_APP_API_TOKEN, appName);
			errorCounter.WithLabels(ERROR_LOG_FILE_TOO_LARGE, appName);
			errorCounter.WithLabels(ERROR_INVALID_CRYPTO_METADATA, appName);
			warningCounter.WithLabels(WARNING_LOG_ID_CONFLICT, appName);
			warningCounter.WithLabels(WARNING_LOG_UPLOAD_RETRY, appName);
			warningCounter.WithLabels(WARNING_RETRYING_COMPLETED_UPLOAD, appName);
			warningCounter.WithLabels(WARNING_UPLOAD_RETRY_CHANGED_SUFFIX, appName);
			warningCounter.WithLabels(WARNING_UPLOAD_RETRY_CHANGED_ENCODING, appName);
		}

		/// <inheritdoc/>
		public void HandleIncorrectAppApiTokenError(string appName) {
			errorCounter.WithLabels(ERROR_INCORRECT_APP_API_TOKEN, appName).Inc();
		}

		/// <inheritdoc/>
		public void HandleIncorrectSecurityTokenClaimsError() {
			errorCounter.WithLabels(ERROR_INCORRECT_SECURITY_TOKEN_CLAIMS, "").Inc();
		}

		/// <inheritdoc/>
		public void HandleLogFileTooLargeError(string appName) {
			errorCounter.WithLabels(ERROR_LOG_FILE_TOO_LARGE, appName).Inc();
		}

		/// <inheritdoc/>
		public void HandleLogUploadedSuccessfully(string appName) {
			lastLogUploadTime.WithLabels(appName).IncToCurrentTimeUtc();
		}

		/// <inheritdoc/>
		public void HandleUnexpectedError(string appName, Exception ex) {
			errorCounter.WithLabels(ex.GetType().FullName ?? "unknown", appName).Inc();
		}

		/// <inheritdoc/>
		public void HandleUnknownAppError(string appName) {
			errorCounter.WithLabels(ERROR_UNKNOWN_APP, appName).Inc();
		}

		/// <inheritdoc/>
		public void UpdateCollectedLogs(IDictionary<string, int> perAppCounts) {
			logsCollected.UpdateLabeledValues(perAppCounts);
		}

		/// <inheritdoc/>
		public void HandleLogIdConflictWarning(string appName) {
			warningCounter.WithLabels(WARNING_LOG_ID_CONFLICT, appName).Inc();
		}

		/// <inheritdoc/>
		public void HandleLogUploadRetryWarning(string appName) {
			warningCounter.WithLabels(WARNING_LOG_UPLOAD_RETRY, appName).Inc();
		}

		/// <inheritdoc/>
		public void HandleRetryingCompletedUploadWarning(string appName) {
			warningCounter.WithLabels(WARNING_RETRYING_COMPLETED_UPLOAD, appName).Inc();
		}

		/// <inheritdoc/>
		public void HandleUploadRetryChangedSuffixWarning(string appName) {
			warningCounter.WithLabels(WARNING_UPLOAD_RETRY_CHANGED_SUFFIX, appName).Inc();
		}

		/// <inheritdoc/>
		public void HandleUploadRetryChangedEncodingWarning(string appName) {
			warningCounter.WithLabels(WARNING_UPLOAD_RETRY_CHANGED_ENCODING, appName).Inc();
		}

		/// <inheritdoc/>
		public void HandleModelStateValidationError(string errorMessage) {
			errorCounter.WithLabels(ERROR_MODEL_STATE_VALIDATION_FAILED, "").Inc();
		}

		/// <inheritdoc/>
		public void ObserveIngestedLogFileSize(string appName, long size) {
			logSizes.WithLabels(appName).Observe(size);
		}

		/// <inheritdoc/>
		public void UpdateAvgLogSize(IDictionary<string, double> perAppSizes) {
			logsAvgSize.UpdateLabeledValues(perAppSizes);
		}

		/// <inheritdoc/>
		public void HandleCryptoMetadataError(string appName) {
			errorCounter.WithLabels(ERROR_INVALID_CRYPTO_METADATA, appName).Inc();
		}
	}
}
