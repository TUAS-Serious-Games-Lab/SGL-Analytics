using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

		public async Task<ApplicationWithUserProperties?> GetApplicationByNameAsync(string appName, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (Apps.TryGetValue(appName, out var app)) {
				return app;
			}
			else {
				return null;
			}
		}

		public async Task<ApplicationWithUserProperties> AddApplicationAsync(ApplicationWithUserProperties app, CancellationToken ct = default) {
			if (Apps.ContainsKey(app.Name)) throw new EntityUniquenessConflictException("Application", "Name", app.Name);
			if (app.Id == Guid.Empty) app.Id = Guid.NewGuid();
			assignPropDefIds(app);
			ct.ThrowIfCancellationRequested();
			Apps.Add(app.Name, app);
			await Task.CompletedTask;
			return app;
		}

		public async Task<User?> GetUserByIdAsync(Guid userId, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (users.TryGetValue(userId, out var user)) {
				return user;
			}
			else {
				return null;
			}
		}

		public async Task<User?> GetUserByUsernameAndAppNameAsync(string username, string appName, CancellationToken ct = default) {
			await Task.CompletedTask;
			return users.Values.Where(u => u.Username == username && u.App.Name == appName).SingleOrDefault<User?>();
		}

		public async Task<User> RegisterUserAsync(UserRegistrationDTO userRegistrationData, CancellationToken ct = default) {
			await Task.CompletedTask;
			if (userRegistrationData.Username != null && users.Values.Count(u => u.Username == userRegistrationData.Username) > 0) {
				throw new EntityUniquenessConflictException("User", "Username", userRegistrationData.Username);
			}
			if (!Apps.TryGetValue(userRegistrationData.AppName, out var app)) {
				throw new ApplicationDoesNotExistException(userRegistrationData.AppName);
			}

			string hashedSecret = SecretHashing.CreateHashedSecret(userRegistrationData.Secret);
			UserRegistration userReg = userRegistrationData.Username != null ?
				UserRegistration.Create(app!, userRegistrationData.Username, hashedSecret) :
				UserRegistration.Create(app!, hashedSecret);
			var user = new User(userReg);
			foreach (var prop in userRegistrationData.StudySpecificProperties) {
				user.AppSpecificProperties[prop.Key] = prop.Value;
			}
			IUserRegistrationWrapper userWrap = user;
			userWrap.StoreAppPropertiesToUnderlying();
			userWrap.Underlying.Id = Guid.NewGuid();
			userWrap.Underlying.ValidateProperties();
			ct.ThrowIfCancellationRequested();
			users.Add(user.Id, user);
			userWrap.LoadAppPropertiesFromUnderlying();
			return user;
		}

		public async Task<User> UpdateUserAsync(User user, CancellationToken ct = default) {
			await Task.CompletedTask;
			Debug.Assert(users.ContainsKey(user.Id));
			IUserRegistrationWrapper userWrap = user;
			userWrap.StoreAppPropertiesToUnderlying();
			userWrap.Underlying.ValidateProperties();
			ct.ThrowIfCancellationRequested();
			users[user.Id] = user;
			userWrap.LoadAppPropertiesFromUnderlying();
			return user;
		}

		public async Task<ApplicationWithUserProperties> UpdateApplicationAsync(ApplicationWithUserProperties app, CancellationToken ct = default) {
			await Task.CompletedTask;
			Debug.Assert(Apps.ContainsKey(app.Name));
			assignPropDefIds(app);
			ct.ThrowIfCancellationRequested();
			Apps[app.Name] = app;
			return app;
		}
	}
}
