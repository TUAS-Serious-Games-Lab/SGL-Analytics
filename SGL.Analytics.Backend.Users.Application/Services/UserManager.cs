using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Services {
	public class UserManager : IUserManager {
		private IApplicationRepository appRepo;
		private IUserRepository userRepo;
		private ILogger<UserManager> logger;

		public UserManager(IApplicationRepository appRepo, IUserRepository userRepo, ILogger<UserManager> logger) {
			this.appRepo = appRepo;
			this.userRepo = userRepo;
			this.logger = logger;
		}

		public async Task<User?> GetUserById(Guid userId) {
			var userReg = await userRepo.GetUserByIdAsync(userId);
			if (userReg is null) return null;
			return new User(userReg);
		}

		public async Task<User> RegisterUserAsync(UserRegistrationDTO userRegDTO) {
			var app = await appRepo.GetApplicationByNameAsync(userRegDTO.AppName);
			if (app is null) {
				throw new ApplicationDoesNotExistException(userRegDTO.AppName);
			}
			var userReg = UserRegistration.Create(app, userRegDTO.Username);
			User user = new User(userReg);
			foreach (var prop in userRegDTO.StudySpecificProperties) {
				user.AppSpecificProperties[prop.Key] = prop.Value;
			}
			IUserRegistrationWrapper userWrap = user;
			userWrap.StoreAppPropertiesToUnderlying();
			userWrap.Underlying = await userRepo.RegisterUserAsync(userReg);
			userWrap.LoadAppPropertiesFromUnderlying();
			return user;
		}

		public async Task<User> UpdateUserAsync(User user) {
			IUserRegistrationWrapper userWrap = user;
			userWrap.StoreAppPropertiesToUnderlying();
			userWrap.Underlying = await userRepo.UpdateUserAsync(userWrap.Underlying);
			userWrap.LoadAppPropertiesFromUnderlying();
			return user;
		}
	}
}
