using SGL.Analytics.Backend.Users.Application.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	public interface IKeyAuthChallengeStateHolder {
		Task OpenChallengeAsync(ChallengeState challenge, CancellationToken ct = default);
		Task<ChallengeState?> GetChallengeAsync(Guid challengeId, CancellationToken ct = default);
		Task CloseChallengeAsync(ChallengeState challenge, CancellationToken ct = default);
		Task CleanupTimeoutsAsync(CancellationToken ct = default);
	}
}
