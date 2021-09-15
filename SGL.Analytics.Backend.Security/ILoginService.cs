using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Security {
	public interface ILoginService {

		Task<string?> LoginAsync<TUserId, TUser>(
			TUserId userId, string providedPlainSecret,
			Func<TUserId, Task<TUser?>> lookupUserAsync,
			Func<TUser, string> getHashedSecret,
			Func<TUser, string, Task> updateHashedSecretAsync);
	}
}
