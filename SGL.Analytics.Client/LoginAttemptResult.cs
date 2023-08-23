using SGL.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	/// <summary>
	/// Describes the result of an attempted login operation.
	/// </summary>
	public enum LoginAttemptResult {
		/// <summary>
		/// The login couldn't be completed due to a network issue or an unexpected error.
		/// This can happen if the backend is not available / malfunctioning or when the client doesn't have an internet connection.
		/// </summary>
		NetworkProblem,
		/// <summary>
		/// <see cref="SglAnalytics.TryLoginWithStoredCredentialsAsync"/> failed because no stored credentials were present, or
		/// <see cref="SglAnalytics.TryLoginWithUpstreamDelegationAsync(Func{CancellationToken, Task{AuthorizationData}}, CancellationToken)"/>
		/// failed because no user account associated with the upstream user id of the supplied upstream authorization token was present.
		/// </summary>
		CredentialsNotAvailable,
		/// <summary>
		/// The login attempt failed due to invalid credentials, i.e. an incorrect username or password, an invalid device token,
		/// or an upstream authorization token that was rejected by the upstream backend.
		/// </summary>
		Failed,
		/// <summary>
		/// The login operation was completed successfully.
		/// </summary>
		Completed
	}
}
