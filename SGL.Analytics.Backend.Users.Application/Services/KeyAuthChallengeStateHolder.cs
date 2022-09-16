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
	public class KeyAuthChallengeStateHolder : IKeyAuthChallengeStateHolder, IDisposable {
		private AsyncSemaphoreLock lockObj = new AsyncSemaphoreLock();
		private Dictionary<Guid, ChallengeState> openChallenges = new Dictionary<Guid, ChallengeState>();

		public async Task OpenChallengeAsync(ChallengeState challenge, CancellationToken ct = default) {
			using var lockHandle = await lockObj.WaitAsyncWithScopedRelease(ct);
			openChallenges[challenge.ChallengeId] = challenge;
		}

		public async Task<ChallengeState?> GetChallengeAsync(Guid challengeId, CancellationToken ct = default) {
			using var lockHandle = await lockObj.WaitAsyncWithScopedRelease(ct);
			if (openChallenges.TryGetValue(challengeId, out var challenge)) {
				if (challenge.Timeout.ToUniversalTime() <= DateTime.UtcNow) {
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

		public async Task CloseChallengeAsync(ChallengeState challenge, CancellationToken ct = default) {
			using var lockHandle = await lockObj.WaitAsyncWithScopedRelease(ct);
			openChallenges.Remove(challenge.ChallengeId);
		}

		public async Task CleanupTimeoutsAsync(CancellationToken ct = default) {
			using var lockHandle = await lockObj.WaitAsyncWithScopedRelease(ct);
			var timedOutChallenges = openChallenges.Values.Where(c => c.Timeout.ToUniversalTime() > DateTime.UtcNow).ToList();
			ct.ThrowIfCancellationRequested();
			timedOutChallenges.ForEach(c => openChallenges.Remove(c.ChallengeId));
		}

		public void Dispose() {
			lockObj.Dispose();
		}
	}

	public class KeyAuthChallengeStateCleanupService : BackgroundService {
		private readonly ILogger<KeyAuthChallengeStateCleanupService> logger;
		private readonly IKeyAuthChallengeStateHolder stateHolder;

		public KeyAuthChallengeStateCleanupService(ILogger<KeyAuthChallengeStateCleanupService> logger, IKeyAuthChallengeStateHolder stateHolder) {
			this.logger = logger;
			this.stateHolder = stateHolder;
		}

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
