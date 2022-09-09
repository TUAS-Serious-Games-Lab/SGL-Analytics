using System;
using System.Collections.Generic;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
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
		/// Called when an error is caused because the client attempts to login with a username that does not exist (for the given application).
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleNonexistentUsernameError(string appName);
		/// <summary>
		/// Called when an error is caused because the client attempts to login with a userid that does not exist.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleNonexistentUserIdError(string appName);
		/// <summary>
		/// Called when an error is caused because the client attempts to login with an incorrect user secret for the specified user.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleIncorrectUserSecretError(string appName);
		/// <summary>
		/// Called when an error is caused because the client attempts to login with a userid that is not associated with the given application.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleUserIdAppMismatchError(string appName);
		/// <summary>
		/// Called when an error is caused because the application-specific user properties given in a user registration failed the validation.
		/// This can be the case, e.g. if a property was given, that is not defined for the app, or a required property is missing.
		/// This usually indicates that the application's behavior is not properly in-sync with the app registraiton in the database.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleUserPropertyValiidationError(string appName);
		/// <summary>
		/// Called when an error is caused by a concurrency problem, usually from the database layer.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleConcurrencyConflictError(string appName);
		/// <summary>
		/// Called when an error is caused by a uniqueness constraint violation from the data persistence layer.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleUniquenessConflictError(string appName);
		/// <summary>
		/// Called when an error is caused by an attempted user registration where the specified username is already in use with the given app.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleUsernameAlreadyTakenError(string appName);
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
		/// Called when a login is completed successfully.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleSuccessfulLogin(string appName);
		/// <summary>
		/// Called when a user registration is completed successfully.
		/// </summary>
		/// <param name="appName">The unique name of the app with which the metric is associated.</param>
		void HandleSuccessfulRegistration(string appName);
		/// <summary>
		/// Periodically called with the number of registered users for each application in the database.
		/// </summary>
		/// <param name="perAppCounts">A dictionary mapping the application names to corresponding numbers of users.</param>
		void UpdateRegisteredUsers(IDictionary<string, int> perAppCounts);
	}
	/// <summary>
	/// Provides a null implementation of <see cref="IMetricsManager"/> where the methods do nothing and thus no metrics are actually collected.
	/// </summary>
	public class NullMetricsManager : IMetricsManager {
		/// <inheritdoc/>
		public void EnsureMetricsExist(string appName) { }
		/// <inheritdoc/>
		public void HandleConcurrencyConflictError(string appName) { }

		/// <inheritdoc/>
		public void HandleCryptoMetadataError(string appName) { }

		/// <inheritdoc/>
		public void HandleIncorrectAppApiTokenError(string appName) { }
		/// <inheritdoc/>
		public void HandleIncorrectUserSecretError(string appName) { }
		/// <inheritdoc/>
		public void HandleModelStateValidationError(string errorMessage) { }
		/// <inheritdoc/>
		public void HandleNonexistentUserIdError(string appName) { }
		/// <inheritdoc/>
		public void HandleNonexistentUsernameError(string appName) { }
		/// <inheritdoc/>
		public void HandleSuccessfulLogin(string appName) { }
		/// <inheritdoc/>
		public void HandleSuccessfulRegistration(string appName) { }
		/// <inheritdoc/>
		public void HandleUnexpectedError(string appName, Exception ex) { }
		/// <inheritdoc/>
		public void HandleUniquenessConflictError(string appName) { }
		/// <inheritdoc/>
		public void HandleUnknownAppError(string appName) { }
		/// <inheritdoc/>
		public void HandleUserIdAppMismatchError(string appName) { }
		/// <inheritdoc/>
		public void HandleUsernameAlreadyTakenError(string appName) { }
		/// <inheritdoc/>
		public void HandleUserPropertyValiidationError(string appName) { }
		/// <inheritdoc/>
		public void UpdateRegisteredUsers(IDictionary<string, int> perAppCounts) { }
	}
}
