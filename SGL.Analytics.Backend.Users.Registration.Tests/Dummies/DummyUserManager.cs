using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Registration.Tests.Dummies {
	public class DummyUserManager : IUserManager {
		private IApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions> appRepo;
		private Dictionary<Guid, User> users = new();
		private int nextPropDefId = 1;

		private void assignPropDefIds(ApplicationWithUserProperties app) {
			foreach (var propDef in app.UserProperties) {
				if (propDef.Id == 0) propDef.Id = nextPropDefId++;
			}
		}

		public DummyUserManager(IApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions> appRepo, IEnumerable<ApplicationWithUserProperties> apps) {
			this.appRepo = appRepo;
			foreach (var app in apps) {
				assignPropDefIds(app);
				appRepo.AddApplicationAsync(app).Wait();
			}
		}

		public async Task<User?> GetUserByIdAsync(Guid userId, KeyId? recipientKeyId = null, bool fetchProperties = false, CancellationToken ct = default) {
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
			var app = await appRepo.GetApplicationByNameAsync(userRegistrationData.AppName);
			if (app == null) {
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

		public Task<IEnumerable<Guid>> ListUserIdsAsync(string appName, string exporterDN, CancellationToken ct) {
			return Task.FromResult(users.Values.Where(u => u.App.Name == appName).Select(u => u.Id).ToList().AsEnumerable());
		}

		public Task<IEnumerable<User>> ListUsersAsync(string appName, KeyId? recipientKeyId, string exporterDN, CancellationToken ct) {
			return Task.FromResult(users.Values.Where(u => u.App.Name == appName).ToList().AsEnumerable());
		}

		public Task AddRekeyedKeysAsync(string appName, KeyId newRecipientKeyId, Dictionary<Guid, DataKeyInfo> dataKeys, string exporterDN, CancellationToken ct) {
			throw new NotImplementedException();
		}
	}
}
