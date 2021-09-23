﻿using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Security {
	public interface ILoginService {
		public interface IDelayHandle {
			Task WaitAsync();
		};
		protected class DelayHandle : IDelayHandle {
			private Task delayTask;

			public DelayHandle(Task delayTask) {
				this.delayTask = delayTask;
			}

			public Task WaitAsync() {
				return delayTask;
			}
		}

		IDelayHandle StartFixedFailureDelay(CancellationToken ct = default);
		Task<AuthorizationToken?> LoginAsync<TUserId, TUser>(
			TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync,
			IDelayHandle fixedFailureDelay, CancellationToken ct,
			params (string ClaimType, Func<TUser, string> GetClaimValue)[] additionalClaims);
		Task<AuthorizationToken?> LoginAsync<TUserId, TUser>(
			TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync, CancellationToken ct,
			params (string ClaimType, Func<TUser, string> GetClaimValue)[] additionalClaims) {
			return LoginAsync(userId, providedPlainSecret, lookupUserAsync, getHashedSecret, updateHashedSecretAsync, StartFixedFailureDelay(), ct, additionalClaims);
		}

		Task<AuthorizationToken?> LoginAsync<TUserId, TUser>(
			TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync,
			IDelayHandle fixedFailureDelay,
			params (string ClaimType, Func<TUser, string> GetClaimValue)[] additionalClaims) {
			return LoginAsync(userId, providedPlainSecret, lookupUserAsync, getHashedSecret, updateHashedSecretAsync, fixedFailureDelay, default(CancellationToken), additionalClaims);
		}
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