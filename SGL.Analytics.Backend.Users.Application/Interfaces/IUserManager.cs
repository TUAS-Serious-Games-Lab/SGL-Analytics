using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	public interface IUserManager {
		Task<User?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
		Task<User> RegisterUserAsync(UserRegistrationDTO userRegistration, CancellationToken ct = default);
		Task<User> UpdateUserAsync(User user, CancellationToken ct = default);
	}
}
