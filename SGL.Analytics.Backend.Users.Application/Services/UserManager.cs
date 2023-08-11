using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Backend.Security;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Services {
	public class UserManagerOptions {
		public const string UserManager = "UserManager";
		public int RekeyingPagination { get; set; } = 100;
	}

	/// <summary>
	/// Implements the functionality required by <see cref="IUserManager"/>.
	/// </summary>
	public class UserManager : IUserManager {
		private IApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions> appRepo;
		private IUserRepository userRepo;
		private UserManagerOptions options;
		private ILogger<UserManager> logger;

		/// <summary>
		/// Creates a <see cref="UserManager"/> using the given repository implementation objects and the given logger for diagnostics logging.
		/// </summary>
		/// <param name="appRepo">The application repository to use.</param>
		/// <param name="userRepo">The user registration repository to use.</param>
		/// <param name="logger">A logger to log status, warning and error messages to.</param>
		/// <param name="options">Configuration options for the UserManager service.</param>
		public UserManager(IApplicationRepository<ApplicationWithUserProperties, ApplicationQueryOptions> appRepo, IUserRepository userRepo, ILogger<UserManager> logger, IOptions<UserManagerOptions> options) {
			this.appRepo = appRepo;
			this.userRepo = userRepo;
			this.logger = logger;
			this.options = options.Value;
		}

		public async Task AddRekeyedKeysAsync(string appName, KeyId newRecipientKeyId, Dictionary<Guid, DataKeyInfo> dataKeys, string exporterDN, CancellationToken ct) {
			var app = await appRepo.GetApplicationByNameAsync(appName, ct: ct);
			if (app is null) {
				logger.LogError("Attempt to upload rekeyed data keys for non-existent application {appName} for recipient {keyId} by exporter {dn}.", appName, newRecipientKeyId, exporterDN);
				throw new ApplicationDoesNotExistException(appName);
			}
			var queryOptions = new UserQueryOptions {
				ForUpdating = true,
				FetchRecipientKeys = true,
				Ordering = UserQuerySortCriteria.UserId,
				Limit = options.RekeyingPagination
			};
			var userRegs = (await userRepo.GetUsersByIdsAsync(dataKeys.Keys, queryOptions, ct)).ToList();
			logger.LogInformation("Putting {keyCount} rekeyed data keys for recipient {recipientKeyId} into matching user registrations out of {userRegCount} looked-up registrations in application {appName} ...",
				dataKeys.Count, newRecipientKeyId, userRegs.Count, appName);
			using var logScope = logger.BeginScope("Rekey-Put {keyId}", newRecipientKeyId);
			var pendingIds = dataKeys.Keys.ToHashSet();
			foreach (var userReg in userRegs) {
				if (userReg.App.Name != appName) {
					logger.LogError("Attempt to put rekeyed key for user registration {userId} in app {appName1} through a request from app {appName2}.", userReg.Id, userReg.App.Name, appName);
					continue;
				}
				if (dataKeys.TryGetValue(userReg.Id, out var newDataKeyInfo)) {
					if (userReg.PropertyRecipientKeys.Any(rk => rk.RecipientKeyId == newRecipientKeyId)) {
						logger.LogWarning("Attempt to put rekeyed key for recipient {keyId} into user registration {userId} that already has a data key for that recipient.",
							newRecipientKeyId, userReg.Id);
					}
					else {
						userReg.PropertyRecipientKeys.Add(new UserRegistrationPropertyRecipientKey {
							UserId = userReg.Id,
							RecipientKeyId = newRecipientKeyId,
							EncryptionMode = newDataKeyInfo.Mode,
							EncryptedKey = newDataKeyInfo.EncryptedKey,
							UserPropertiesPublicKey = newDataKeyInfo.MessagePublicKey
						});
						logger.LogDebug("Put key for recipient {keyId} on user registration {userId}.", newRecipientKeyId, userReg.Id);
						pendingIds.Remove(userReg.Id);
					}
				}
				else {
					logger.LogWarning("No key for user registration {userId} and recipient {keyId} was provided.", userReg.Id, newRecipientKeyId);
				}
			}
			if (pendingIds.Count > 0) {
				logger.LogWarning("The following user registration ids given by the rekeying uploader were not present: {userIdList}", string.Join(", ", pendingIds));
			}
			await userRepo.UpdateUsersAsync(userRegs, ct);
			logger.LogInformation("... rekeying upload finished.");
		}

		public async Task<Dictionary<Guid, EncryptionInfo>> GetKeysForRekeying(string appName, KeyId recipientKeyId, KeyId targetKeyId, string exporterDN, int offset, CancellationToken ct = default) {
			var queryOptions = new UserQueryOptions {
				ForUpdating = false,
				FetchRecipientKey = recipientKeyId,
				Ordering = UserQuerySortCriteria.UserId,
				Limit = options.RekeyingPagination,
				FetchProperties = true,
				Offset = offset
			};
			var userRegs = await userRepo.ListUsersAsync(appName, notForKeyId: targetKeyId, queryOptions, ct);
			var result = userRegs.Select(u => new User(u)).ToList().ToDictionary(u => u.Id, u => u.PropertyEncryptionInfo);
			return result;
		}

		/// <inheritdoc/>
		public async Task<User?> GetUserByIdAsync(Guid userId, KeyId? recipientKeyId = null, bool fetchProperties = false, CancellationToken ct = default) {
			var queryOptions = new UserQueryOptions { ForUpdating = true, FetchRecipientKey = recipientKeyId, FetchProperties = fetchProperties };
			var userReg = await userRepo.GetUserByIdAsync(userId, queryOptions, ct);
			if (userReg is null) return null;
			return new User(userReg);
		}

		/// <inheritdoc/>
		public async Task<User?> GetUserByUsernameAndAppNameAsync(string username, string appName, CancellationToken ct = default) {
			var queryOptions = new UserQueryOptions { ForUpdating = true };
			var userReg = await userRepo.GetUserByUsernameAndAppNameAsync(username, appName, queryOptions, ct);
			if (userReg is null) return null;
			return new User(userReg);
		}

		/// <inheritdoc/>
		public async Task<IEnumerable<Guid>> ListUserIdsAsync(string appName, string exporterDN, CancellationToken ct) {
			var queryOptions = new UserQueryOptions { ForUpdating = false };
			var userRegs = await userRepo.ListUsersAsync(appName, notForKeyId: null, queryOptions, ct);
			var result = userRegs.Select(u => u.Id).ToList();
			return result;
		}

		/// <inheritdoc/>
		public async Task<IEnumerable<User>> ListUsersAsync(string appName, KeyId? recipientKeyId, string exporterDN, CancellationToken ct) {
			var queryOptions = new UserQueryOptions { ForUpdating = false, FetchRecipientKey = recipientKeyId, FetchProperties = true };
			var userRegs = await userRepo.ListUsersAsync(appName, notForKeyId: null, queryOptions, ct);
			var result = userRegs.Select(u => new User(u)).ToList();
			return result;
		}

		public Task<DelegatedLoginResponseDTO> OpenSessionFromUpstreamAsync(ApplicationWithUserProperties app, string authHeader, CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public async Task<User> RegisterUserAsync(UserRegistrationDTO userRegDTO, CancellationToken ct = default) {
			if (userRegDTO.EncryptedProperties != null) {
				if (userRegDTO.PropertyEncryptionInfo == null) {
					throw new EncryptedDataWithoutEncryptionMetadataException("User registration with encrypted properties is missing the associated encryption metadata.");
				}
				else if (userRegDTO.PropertyEncryptionInfo.DataMode == DataEncryptionMode.Unencrypted) {
					throw new EncryptedDataWithoutEncryptionMetadataException("User registration with encrypted properties has no associated keys. " +
						"The metadata object indicates unencrypted, which is not valid for encrypted properties.");
				}
				else if (!userRegDTO.PropertyEncryptionInfo.DataKeys.Any()) {
					throw new MissingRecipientDataKeysForEncryptedDataException("User registration with encrypted properties is missing the recipient data keys, " +
						"which means the properties could never be decrypted, refusing the registration.");
				}
			}

			var app = await appRepo.GetApplicationByNameAsync(userRegDTO.AppName, ct: ct);
			if (app is null) {
				logger.LogError("Attempt to register user {username} for non-existent application {appName}.", userRegDTO.Username, userRegDTO.AppName);
				throw new ApplicationDoesNotExistException(userRegDTO.AppName);
			}
			UserRegistration userReg;
			User user;
			if (userRegDTO.Secret != null) {
				// Register full user account, including credentials:
				var hashedSecret = SecretHashing.CreateHashedSecret(userRegDTO.Secret);
				userReg = userRegDTO.Username != null ?
				   UserRegistration.Create(app, userRegDTO.Username, hashedSecret,
					   userRegDTO.EncryptedProperties ?? new byte[0], userRegDTO.PropertyEncryptionInfo ?? EncryptionInfo.CreateUnencrypted()) :
				   UserRegistration.Create(app, hashedSecret,
					   userRegDTO.EncryptedProperties ?? new byte[0], userRegDTO.PropertyEncryptionInfo ?? EncryptionInfo.CreateUnencrypted());
				user = new User(userReg);
			}
			else {
				// Register user account using federated authentication against upstream backend:
				throw new NotImplementedException("Federated authentication is not yet implemented!");
			}
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
