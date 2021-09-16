using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Security;
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
	public class DummyUserManager : IUserManager, IApplicationRepository {
		private Dictionary<Guid, User> users = new();
		public Dictionary<string, ApplicationWithUserProperties> Apps { get; } = new();
		private int nextPropDefId = 1;

		private void assignPropDefIds(ApplicationWithUserProperties app) {
			foreach (var propDef in app.UserProperties) {
				if (propDef.Id == 0) propDef.Id = nextPropDefId++;
			}
		}

		public DummyUserManager(IEnumerable<ApplicationWithUserProperties> apps) {
			foreach (var app in apps) {
				assignPropDefIds(app);
				Apps[app.Name] = app;
			}
		}

		public async Task<ApplicationWithUserProperties?> GetApplicationByNameAsync(string appName) {
			await Task.CompletedTask;
			if (Apps.TryGetValue(appName, out var app)) {
				return app;
			}
			else {
				return null;
			}
		}

		public async Task<ApplicationWithUserProperties> AddApplicationAsync(ApplicationWithUserProperties app) {
			if (Apps.ContainsKey(app.Name)) throw new EntityUniquenessConflictException("Application", "Name", app.Name);
			if (app.Id == Guid.Empty) app.Id = Guid.NewGuid();
			assignPropDefIds(app);
			Apps.Add(app.Name, app);
			await Task.CompletedTask;
			return app;
		}

		public async Task<User?> GetUserByIdAsync(Guid userId) {
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
			if (!Apps.TryGetValue(userRegistrationData.AppName, out var app)) {
				throw new ApplicationDoesNotExistException(userRegistrationData.AppName);
			}
			var user = new User(UserRegistration.Create(app!, userRegistrationData.Username, SecretHashing.CreateHashedSecret(userRegistrationData.Secret)));
			foreach (var prop in userRegistrationData.StudySpecificProperties) {
				user.AppSpecificProperties[prop.Key] = prop.Value;
			}
			IUserRegistrationWrapper userWrap = user;
			userWrap.StoreAppPropertiesToUnderlying();
			userWrap.Underlying.Id = Guid.NewGuid();
			userWrap.Underlying.ValidateProperties();
			users.Add(user.Id, user);
			userWrap.LoadAppPropertiesFromUnderlying();
			return user;
		}

		public async Task<User> UpdateUserAsync(User user) {
			await Task.CompletedTask;
			Debug.Assert(users.ContainsKey(user.Id));
			IUserRegistrationWrapper userWrap = user;
			userWrap.StoreAppPropertiesToUnderlying();
			userWrap.Underlying.ValidateProperties();
			users[user.Id] = user;
			userWrap.LoadAppPropertiesFromUnderlying();
			return user;
		}
	}
}
