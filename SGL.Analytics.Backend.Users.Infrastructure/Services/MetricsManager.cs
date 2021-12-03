using Prometheus;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Utilities.PrometheusNet;
using System;
using System.Collections.Generic;

namespace SGL.Analytics.Backend.Users.Infrastructure.Services {
	public class MetricsManager : IMetricsManager {
		private static readonly Gauge registeredUsers = Metrics.CreateGauge("sgla_registered_users", "Number of users registered with SGL Analytics User Registration service.", "app");
		private static readonly Counter loginCounter = Metrics.CreateCounter("sgla_logins_total", "Number of successful logins performed by SGL Analytics, labeled by app.", "app");
		private static readonly Gauge lastSuccessfulLoginTime = Metrics.CreateGauge("sgla_last_successful_login_time_seconds", "Unix timestamp of the last successful login for the labeled app (in UTC).", "app");
		private static readonly Gauge lastRegistrationTime = Metrics.CreateGauge("sgla_last_registration_time_seconds", "Unix timestamp of the last user registration for the labeled app (in UTC).", "app");
		private static readonly Counter errorCounter = Metrics.CreateCounter("sgla_errors_total", "Number of service-level errors encountered by SGL Analytics, labeled by error type and app.", "type", "app");
		private const string ERROR_NONEXISTENT_USERNAME = "Nonexistent username";
		private const string ERROR_NONEXISTENT_USERID = "Nonexistent username";
		private const string ERROR_INCORRECT_USER_SECRET = "Incorrect user secret";
		private const string ERROR_UNKNOWN_APP = "Unknown app";
		private const string ERROR_INCORRECT_APP_API_TOKEN = "Incorrect app API token";
		private const string ERROR_USERID_APP_MISMATCH = "Userid does not belong to app";
		private const string ERROR_USER_PROP_VALIDATION_FAILED = "User property validation failed";
		private const string ERROR_CONCURRENCY_CONFLICT = "Concurrency conflict";
		private const string ERROR_UNIQUENESS_CONFLICT = "Uniqueness conflict";
		private const string ERROR_USERNAME_ALREADY_TAKEN = "Username is already taken";

		public void HandleConcurrencyConflictError(string appName) {
			errorCounter.WithLabels(ERROR_CONCURRENCY_CONFLICT, appName).Inc();
		}

		public void HandleUniquenessConflictError(string appName) {
			errorCounter.WithLabels(ERROR_UNIQUENESS_CONFLICT, appName).Inc();
		}

		public void HandleIncorrectAppApiTokenError(string appName) {
			errorCounter.WithLabels(ERROR_INCORRECT_APP_API_TOKEN, appName).Inc();
		}

		public void HandleIncorrectUserSecretError(string appName) {
			errorCounter.WithLabels(ERROR_INCORRECT_USER_SECRET, appName).Inc();
		}

		public void HandleNonexistentUserIdError(string appName) {
			errorCounter.WithLabels(ERROR_NONEXISTENT_USERID, appName).Inc();
		}

		public void HandleNonexistentUsernameError(string appName) {
			errorCounter.WithLabels(ERROR_NONEXISTENT_USERNAME, appName).Inc();
		}

		public void HandleSuccessfulLogin(string appName) {
			loginCounter.WithLabels(appName).Inc();
			lastSuccessfulLoginTime.WithLabels(appName).IncToCurrentTimeUtc();
		}

		public void HandleSuccessfulRegistration(string appName) {
			lastRegistrationTime.WithLabels(appName).IncToCurrentTimeUtc();
		}

		public void HandleUnexpectedError(string appName, Exception ex) {
			errorCounter.WithLabels(ex.GetType().FullName ?? "Unknown", appName).Inc();
		}

		public void HandleUnknownAppError(string appName) {
			errorCounter.WithLabels(ERROR_UNKNOWN_APP, appName).Inc();
		}

		public void HandleUserIdAppMismatchError(string appName) {
			errorCounter.WithLabels(ERROR_USERID_APP_MISMATCH, appName).Inc();
		}

		public void HandleUsernameAlreadyTakenError(string appName) {
			errorCounter.WithLabels(ERROR_USERNAME_ALREADY_TAKEN, appName).Inc();
		}

		public void HandleUserPropertyValiidationError(string appName) {
			errorCounter.WithLabels(ERROR_USER_PROP_VALIDATION_FAILED, appName).Inc();
		}

		public void UpdateRegisteredUsers(IDictionary<string, int> perAppCounts) {
			registeredUsers.UpdateLabeledValues(perAppCounts);
		}
	}
}
