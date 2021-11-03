using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Security {
	/// <summary>
	/// Specifies the interface for a login service, that authenicates users using credentials in the form of a userid and a secret string.
	/// If the authentication is successful, it issues an <see cref="AuthorizationToken"/> for the user.
	/// Otherwise, implementations should wait for a fixed delay timer, started at the beginning of the authentication, in all failure paths to prevent timing attacks to reveal if the user id or the secret was incorrect.
	/// This delay timer is represented by an <see cref="IDelayHandle"/> and started using <see cref="StartFixedFailureDelay(CancellationToken)"/>.
	///
	/// The interface specifies four overloads of <c>LoginAsync</c> to make both, the cancellation token and a pre-started <see cref="IDelayHandle"/> independently optional.
	/// However, implementations only need to provide the fully specified overload and <see cref="StartFixedFailureDelay(CancellationToken)"/>.
	///
	/// The user and secret management is wired up using delegates to minimize coupling to specific user management environments.
	/// </summary>
	public interface ILoginService {
		/// <summary>
		/// Represents a handle for a fixed delay timer for which implementations should wait on failure to prevent timing attacks.
		/// </summary>
		public interface IDelayHandle {
			/// <summary>
			/// Asynchronously wait for the fixed delay to expire.
			/// </summary>
			/// <returns>A <see cref="Task"/> representing the delay.</returns>
			Task WaitAsync();
		};

		/// <summary>
		/// A default implementation for <see cref="IDelayHandle"/>.
		/// </summary>
		protected class DelayHandle : IDelayHandle {
			private Task delayTask;

			/// <summary>
			/// Wraps a task representing the fixed delay in the delay handle.
			/// </summary>
			/// <param name="delayTask">A task representing the fixed delay time, usually created using <see cref="Task.Delay(TimeSpan, CancellationToken)"/> with an appropriate time.</param>
			public DelayHandle(Task delayTask) {
				this.delayTask = delayTask;
			}

			/// <summary>
			/// Asynchronously waits for the delay time by returning the wrapped task.
			/// </summary>
			/// <returns>The wrapped delay task, that was passed into the constructor.</returns>
			public Task WaitAsync() {
				return delayTask;
			}
		}

		/// <summary>
		/// Starts a fixed delay timer and returns a it as an <see cref="IDelayHandle"/>.
		/// </summary>
		/// <param name="ct"></param>
		/// <returns></returns>
		/// <remarks>
		/// Implementations should usually just wrap the result of calling <see cref="Task.Delay(TimeSpan, CancellationToken)"/> with an appropriate delay time and <c>ct</c> in a <see cref="DelayHandle"/>.
		/// The appropriate time depends on what work the implementation has to perform and may also be made configurable, but it should be significantly longer than worst case estimates for the time needed to process the longest running failure path.
		///
		/// This method is separated out because code consuming a login service may need to cover additional operations in this delay time.
		/// For eample, if the code needs to lookup a user login domain before looking up the user, it might not want to reveal whether a failed login is from the login domain, from the user id or from the secret.
		/// It can then configure a longer delay for the implementation, start the delay time before looking up the login domain, perform that lookup, and then call a <c>LoginAsync</c> overload that takes an <see cref="IDelayHandle"/>.
		/// If the login domain lookup fails, it needs to await <see cref="IDelayHandle.WaitAsync"/> to ensure the failure delay.
		/// If it succeeds, but the login service fails, the process as a whole will return the error after the same amount ouf time.
		///
		/// Note that the success case should simply not await <see cref="IDelayHandle.WaitAsync"/> to avoid an unnecessary slow down of legitimate users.
		/// </remarks>
		IDelayHandle StartFixedFailureDelay(CancellationToken ct = default);

		/// <summary>
		/// Asynchronously perform a login operation with the given credentials, using the given delegates to access the user management.
		/// </summary>
		/// <typeparam name="TUserId">The data type used for the user id (e.g. <see cref="string"/>, <see cref="Guid"/>, <see cref="int"/>).</typeparam>
		/// <typeparam name="TUser">The type used for encapsulating user data.</typeparam>
		/// <param name="userId">The user id specified by the client attempting to login.</param>
		/// <param name="providedPlainSecret">The plaintext secret provided by the client attempting to login.</param>
		/// <param name="lookupUserAsync">A delegate that asynchronously looks up the user data object using the user id, must return <see langword="null"/> if no such user was found.</param>
		/// <param name="getHashedSecret">A delegate to obtain the hashed login secret from the user data object.</param>
		/// <param name="updateHashedSecretAsync">A delegate to update the login secret hash if the login is successful but the secret needed to be rehashed (due to updated hashing settings).</param>
		/// <param name="fixedFailureDelay">A fixed delay handle to be awaited in case of login failure to prevent timing attacks.</param>
		/// <param name="ct">A <see cref="CancellationToken"/> that cancels the login operation when the token is cancelled.</param>
		/// <param name="additionalClaims">A collection of claim type names and corresponding value getters to specify claims that should be issued in the <see cref="AuthorizationToken"/> in addition to the claims normally issued by the implementation.</param>
		/// <returns>
		/// A task representing the asynchronous operation.
		/// It provides an <see cref="AuthorizationToken"/> as its result if the login was successful.
		/// If the login failed it has a <see langword="null"/> result after the fixed delay time has expired.
		/// </returns>
		Task<AuthorizationToken?> LoginAsync<TUserId, TUser>(
			TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync,
			IDelayHandle fixedFailureDelay, CancellationToken ct,
			params (string ClaimType, Func<TUser, string> GetClaimValue)[] additionalClaims);

		/// <summary>
		/// An overload of <c>LoginAsync</c> where no <see cref="IDelayHandle"/> is given and <see cref="StartFixedFailureDelay(CancellationToken)"/> is called at the start instead.
		/// See the full overload for further details.
		/// </summary>
		Task<AuthorizationToken?> LoginAsync<TUserId, TUser>(
			TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync, CancellationToken ct,
			params (string ClaimType, Func<TUser, string> GetClaimValue)[] additionalClaims) {
			return LoginAsync(userId, providedPlainSecret, lookupUserAsync, getHashedSecret, updateHashedSecretAsync, StartFixedFailureDelay(), ct, additionalClaims);
		}

		/// <summary>
		/// An overload of <c>LoginAsync</c> where no <see cref="CancellationToken"/> is given and the default token is used with the effect that this operation can not be cancelled.
		/// See the full overload for further details.
		/// </summary>
		Task<AuthorizationToken?> LoginAsync<TUserId, TUser>(
			TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync,
			IDelayHandle fixedFailureDelay,
			params (string ClaimType, Func<TUser, string> GetClaimValue)[] additionalClaims) {
			return LoginAsync(userId, providedPlainSecret, lookupUserAsync, getHashedSecret, updateHashedSecretAsync, fixedFailureDelay, default(CancellationToken), additionalClaims);
		}

		/// <summary>
		/// An overload of <c>LoginAsync</c> where neither a <see cref="CancellationToken"/> nor an <see cref="IDelayHandle"/> are given.
		/// The operation therefore automatically calls <see cref="StartFixedFailureDelay(CancellationToken)"/> at the beginning and can not be cancelled.
		/// See the full overload for further details.
		/// </summary>
		Task<AuthorizationToken?> LoginAsync<TUserId, TUser>(
			TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync,
			params (string ClaimType, Func<TUser, string> GetClaimValue)[] additionalClaims) {
			return LoginAsync(userId, providedPlainSecret, lookupUserAsync, getHashedSecret, updateHashedSecretAsync, StartFixedFailureDelay(), default(CancellationToken), additionalClaims);
		}
	}
}
