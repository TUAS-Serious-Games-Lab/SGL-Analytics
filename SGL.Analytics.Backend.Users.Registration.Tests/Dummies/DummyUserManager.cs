using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Registration.Tests.Dummies {
	public class DummyUserManager : IUserManager {
		private Dictionary<Guid, User> users = new();
		private Dictionary<string, ApplicationWithUserProperties> apps = new();

		public DummyUserManager(IEnumerable<ApplicationWithUserProperties> apps) {
			foreach (var app in apps) {
				this.apps[app.Name] = app;
			}
		}

		public async Task<User?> GetUserById(Guid userId) {
			await Task.CompletedTask;
			if (users.TryGetValue(userId, out var user)) {
				return user;
			}
			else {
				return null;
			}
		}

		public async Task<User> RegisterUserAsync(UserRegistrationDTO userRegistrationData) {
			await Task.CompletedTask;
			if (users.Values.Count(u => u.Username == userRegistrationData.Username) > 0) {
				throw new EntityUniquenessConflictException("User", "Username", userRegistrationData.Username);
			}
			if (apps.TryGetValue(userRegistrationData.AppName, out var app)) {
				throw new ApplicationDoesNotExistException(userRegistrationData.AppName);
			}
			var user = new User(UserRegistration.Create(app!, userRegistrationData.Username));
			foreach (var prop in userRegistrationData.StudySpecificProperties) {
				user.AppSpecificProperties[prop.Key] = prop.Value;
			}
			IUserRegistrationWrapper userWrap = user;
			userWrap.StoreAppPropertiesToUnderlying();
			userWrap.Underlying.Id = Guid.NewGuid();
			users.Add(user.Id, user);
			userWrap.LoadAppPropertiesFromUnderlying();
			return user;
		}

		public async Task<User> UpdateUserAsync(User user) {
			await Task.CompletedTask;
			Debug.Assert(users.ContainsKey(user.Id));
			IUserRegistrationWrapper userWrap = user;
			userWrap.StoreAppPropertiesToUnderlying();
			users[user.Id] = user;
			userWrap.LoadAppPropertiesFromUnderlying();
			return user;
		}
	}
}
