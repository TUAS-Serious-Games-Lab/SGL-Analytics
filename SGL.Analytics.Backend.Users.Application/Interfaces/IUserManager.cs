using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	public interface IUserManager {
		Task<User?> GetUserByIdAsync(Guid userId);
		Task<User> RegisterUserAsync(UserRegistrationDTO userRegistration);
		Task<User> UpdateUserAsync(User user);
	}
}
