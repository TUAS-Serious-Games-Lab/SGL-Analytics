using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
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

		IDelayHandle StartFixedFailureDelay();
		Task<string?> LoginAsync<TUserId, TUser>(
			TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync, IDelayHandle fixedFailureDelay);
		Task<string?> LoginAsync<TUserId, TUser>(
			TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync) {
			return LoginAsync(userId, providedPlainSecret, lookupUserAsync, getHashedSecret, updateHashedSecretAsync, StartFixedFailureDelay());
		}
	}
}
