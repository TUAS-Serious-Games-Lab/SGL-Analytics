using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend.Security;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Services {
	/// <summary>
	/// Implements the functionality required by <see cref="IUserManager"/>.
	/// </summary>
	public class UserManager : IUserManager {
		private IApplicationRepository appRepo;
		private IUserRepository userRepo;
		private ILogger<UserManager> logger;

		/// <summary>
		/// Creates a <see cref="UserManager"/> using the given repository implementation objects and the given logger for diagnostics logging.
		/// </summary>
		/// <param name="appRepo">The application repository to use.</param>
		/// <param name="userRepo">The user registration repository to use.</param>
		/// <param name="logger">A logger to log status, warning and error messages to.</param>
		public UserManager(IApplicationRepository appRepo, IUserRepository userRepo, ILogger<UserManager> logger) {
			this.appRepo = appRepo;
			this.userRepo = userRepo;
			this.logger = logger;
		}

		/// <inheritdoc/>
		public async Task<User?> GetUserByIdAsync(Guid userId, CancellationToken ct = default) {
			var userReg = await userRepo.GetUserByIdAsync(userId, ct);
			if (userReg is null) return null;
			return new User(userReg);
		}

		/// <inheritdoc/>
		public async Task<User?> GetUserByUsernameAndAppNameAsync(string username, string appName, CancellationToken ct = default) {
			var userReg = await userRepo.GetUserByUsernameAndAppNameAsync(username, appName, ct);
			if (userReg is null) return null;
			return new User(userReg);
		}

		/// <inheritdoc/>
		public async Task<User> RegisterUserAsync(UserRegistrationDTO userRegDTO, CancellationToken ct = default) {
			var app = await appRepo.GetApplicationByNameAsync(userRegDTO.AppName, ct);
			if (app is null) {
				logger.LogError("Attempt to register user {username} for non-existent application {appName}.", userRegDTO.Username, userRegDTO.AppName);
				throw new ApplicationDoesNotExistException(userRegDTO.AppName);
			}

			var hashedSecret = SecretHashing.CreateHashedSecret(userRegDTO.Secret);
			var userReg = userRegDTO.Username != null ?
				UserRegistration.Create(app, userRegDTO.Username, hashedSecret) :
				UserRegistration.Create(app, hashedSecret);
			User user = new User(userReg);
			foreach (var prop in userRegDTO.StudySpecificProperties) {
				user.AppSpecificProperties[prop.Key] = prop.Value;
			}
			IUserRegistrationWrapper userWrap = user;
			try {
				userWrap.StoreAppPropertiesToUnderlying();
				userWrap.Underlying = await userRepo.RegisterUserAsync(userReg, ct);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("User registration for user {username} in application {appName} was cancelled.", userRegDTO.Username, userRegDTO.AppName);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "User registration for user {username} in application {appName} failed due to an exception.", userRegDTO.Username, userRegDTO.AppName);
				throw;
			}
			try {
				userWrap.LoadAppPropertiesFromUnderlying();
			}
			catch (Exception ex) {
				logger.LogError(ex, "Reloading properties after registration for user {username} in application {appName} failed due to an exception.", userRegDTO.Username, userRegDTO.AppName);
				throw;
			}
			return user;
		}

		/// <inheritdoc/>
		public async Task<User> UpdateUserAsync(User user, CancellationToken ct = default) {
			IUserRegistrationWrapper userWrap = user;
			try {
				userWrap.StoreAppPropertiesToUnderlying();
				userWrap.Underlying = await userRepo.UpdateUserAsync(userWrap.Underlying, ct);
			}
			catch (OperationCanceledException) {
				logger.LogDebug("Updating user data for user {username} in application {appName} was cancelled.", user.Username, user.App.Name);
				throw;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Updating user data for user {username} in application {appName} failed due to an exception.", user.Username, user.App.Name);
				throw;
			}
			try {
				userWrap.LoadAppPropertiesFromUnderlying();
			}
			catch (Exception ex) {
				logger.LogError(ex, "Reloading properties after user data update for user {username} in application {appName} failed due to an exception.", user.Username, user.App.Name);
				throw;
			}
			return user;
		}
	}
}
