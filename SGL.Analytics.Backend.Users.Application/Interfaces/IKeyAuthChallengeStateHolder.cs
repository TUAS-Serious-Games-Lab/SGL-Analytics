using SGL.Analytics.Backend.Users.Application.Services;
using SGL.Analytics.Backend.Users.Application.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	/// <summary>
	/// Specifies the interface for a singleton service that stores the volatile state for key-based challenge authentication operations.
	/// </summary>
	public interface IKeyAuthChallengeStateHolder {
		/// <summary>
		/// Used when a challenge is opened to asynchronously add its state to the holder service.
		/// </summary>
		/// <param name="challenge">The challenge data to store.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation.</returns>
		Task OpenChallengeAsync(ChallengeState challenge, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves the challenge with the given id <paramref name="challengeId"/> when it exists, or returns null otherwise.
		/// </summary>
		/// <param name="challengeId">The id of the challenge to fetch.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation.</returns>
		Task<ChallengeState?> GetChallengeAsync(Guid challengeId, CancellationToken ct = default);
		/// <summary>
		/// Used when a challenge is completed to asynchronously close it, i.e. to remove its state from the holder service.
		/// </summary>
		/// <param name="challenge">The id of the challenge to close.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation.</returns>
		Task CloseChallengeAsync(ChallengeState challenge, CancellationToken ct = default);
		/// <summary>
		/// Periodically called by <see cref="KeyAuthChallengeStateCleanupService"/> to cleanup states of timed-out challenges.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation.</returns>
		Task CleanupTimeoutsAsync(CancellationToken ct = default);
	}
}
