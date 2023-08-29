using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Values;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Services {
	/// <summary>
	/// An implementation of <see cref="IKeyAuthChallengeStateHolder"/> that holds the state objects in-memory in the singleton service object
	/// and performs operations under an asynchronously obtained lock.
	/// </summary>
	public class KeyAuthChallengeStateHolder : IKeyAuthChallengeStateHolder, IDisposable {
		private AsyncSemaphoreLock lockObj = new AsyncSemaphoreLock();
		private Dictionary<Guid, ChallengeState> openChallenges = new Dictionary<Guid, ChallengeState>();

		/// <inheritdoc/>
		public async Task OpenChallengeAsync(ChallengeState challenge, CancellationToken ct = default) {
			using var lockHandle = await lockObj.WaitAsyncWithScopedRelease(ct);
			openChallenges[challenge.ChallengeId] = challenge;
		}

		/// <inheritdoc/>
		public async Task<ChallengeState?> GetChallengeAsync(Guid challengeId, CancellationToken ct = default) {
			using var lockHandle = await lockObj.WaitAsyncWithScopedRelease(ct);
			if (openChallenges.TryGetValue(challengeId, out var challenge)) {
				if (challenge.Timeout.ToUniversalTime() >= DateTime.UtcNow) {
					return challenge;
				}
				else {
					openChallenges.Remove(challenge.ChallengeId);
					return null;
				}
			}
			else {
				return null;
			}
		}

		/// <inheritdoc/>
		public async Task CloseChallengeAsync(ChallengeState challenge, CancellationToken ct = default) {
			using var lockHandle = await lockObj.WaitAsyncWithScopedRelease(ct);
			openChallenges.Remove(challenge.ChallengeId);
		}

		/// <inheritdoc/>
		public async Task CleanupTimeoutsAsync(CancellationToken ct = default) {
			using var lockHandle = await lockObj.WaitAsyncWithScopedRelease(ct);
			var timedOutChallenges = openChallenges.Values.Where(c => c.Timeout.ToUniversalTime() > DateTime.UtcNow).ToList();
			ct.ThrowIfCancellationRequested();
			timedOutChallenges.ForEach(c => openChallenges.Remove(c.ChallengeId));
		}

		/// <summary>
		/// Cleans up disposable resources used by the service object.
		/// </summary>
		public void Dispose() {
			lockObj.Dispose();
		}
	}

	/// <summary>
	/// A <see cref="BackgroundService"/> that periodically cleans-up expired challenge state objects from <see cref="IKeyAuthChallengeStateHolder"/>.
	/// </summary>
	public class KeyAuthChallengeStateCleanupService : BackgroundService {
		private readonly ILogger<KeyAuthChallengeStateCleanupService> logger;
		private readonly IKeyAuthChallengeStateHolder stateHolder;

		/// <summary>
		/// Constructs the service object, injecting the required dependencies.
		/// </summary>
		public KeyAuthChallengeStateCleanupService(ILogger<KeyAuthChallengeStateCleanupService> logger, IKeyAuthChallengeStateHolder stateHolder) {
			this.logger = logger;
			this.stateHolder = stateHolder;
		}

		/// <summary>
		/// Asynchronously runs the main loop of the background service.
		/// </summary>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			while (!stoppingToken.IsCancellationRequested) {
				logger.LogDebug("Performing challenge state cleanup...");
				await stateHolder.CleanupTimeoutsAsync(stoppingToken);
				logger.LogDebug("Finished challenge state cleanup.");
				await Task.Delay(TimeSpan.FromMinutes(5));
			}
		}
	}
}
