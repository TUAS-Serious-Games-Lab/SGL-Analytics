using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	/// <summary>
	/// Specifies the interface for high-level user manager class that manages <see cref="User"/> objects as a high-level representation of
	/// registered users and their associated application-specfic properties.
	/// </summary>
	public interface IUserManager {
		/// <summary>
		/// Asynchronously adds data keys that an exporter client has rekeyed to grant another recipient user access to user registration data
		/// into their corresponding user registration's encryption metadata for the encrypted user properties.
		/// </summary>
		/// <param name="appName">The unique name of the application on which to operate.</param>
		/// <param name="newRecipientKeyId">The key id of the recipient key pair for which the rekeyed data keys grant access.</param>
		/// <param name="dataKeys">The data keys as a dictionary mapping from user ids to the corresponding new data keys.</param>
		/// <param name="exporterDN">The distinguished name of the certificate used for authenticating the current user, for logging purposes.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation.</returns>
		Task AddRekeyedKeysAsync(string appName, KeyId newRecipientKeyId, Dictionary<Guid, DataKeyInfo> dataKeys, string exporterDN, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously retrieves a chunk of encrypted data keys and encryption metadata for a rekeying operation.
		/// </summary>
		/// <param name="appName">The unique name of the application on which to operate.</param>
		/// <param name="recipientKeyId">
		/// The key id for the key-pair the user that performs the rekeying operation.
		/// This indicates that the data keys for this key pair need to be returned.
		/// </param>
		/// <param name="targetKeyId">
		/// The key id of the key-pair to which the operation grants access.
		/// Key material and metadata are returned only for user registrations that don't already have a data key for that key pair.
		/// </param>
		/// <param name="exporterDN">The distinguished name of the certificate used for authenticating the current user, for logging purposes.</param>
		/// <param name="offset">
		/// The number of user property encryption data to skip at the start of the listing, when ordering by user id.
		/// This is used for skipping entries that could not be rekeyed in previous iterations.
		/// </param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation.</returns>
		Task<Dictionary<Guid, EncryptionInfo>> GetKeysForRekeying(string appName, KeyId recipientKeyId, KeyId targetKeyId, string exporterDN, int offset, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the user object with the given id if it exists.
		/// </summary>
		/// <param name="userId">The unique id of the user.</param>
		/// <param name="recipientKeyId">If specified, requests that the recipient key for the encrypted registration properties associated with the given key id is fetched.</param>
		/// <param name="fetchProperties">If true, fetch app-specific properties.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The user object if the user exists, or <see langword="null"/> otherwise.</returns>
		Task<User?> GetUserByIdAsync(Guid userId, KeyId? recipientKeyId = null, bool fetchProperties = false, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the user object with the given username in the application given by name if such a user exists.
		/// </summary>
		/// <param name="username">The per-application unique username of the user.</param>
		/// <param name="appName">The application with which the username is associated.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The user object if the user exists, or <see langword="null"/> otherwise.</returns>
		Task<User?> GetUserByUsernameAndAppNameAsync(string username, string appName, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously lists the ids of the users registered in the application indicated by <paramref name="appName"/>.
		/// </summary>
		/// <param name="appName">The unique name of the application on which to operate.</param>
		/// <param name="exporterDN">The distinguished name of the certificate used for authenticating the current user, for logging purposes.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the enumeration of user ids upon success.</returns>
		Task<IEnumerable<Guid>> ListUserIdsAsync(string appName, string exporterDN, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously lists the user data of the users registered in the application indicated by <paramref name="appName"/>.
		/// </summary>
		/// <param name="appName">The unique name of the application on which to operate.</param>
		/// <param name="recipientKeyId">Indicates that data keys for this recipient key-pair for the encrypted user properties shall be included in the data.</param>
		/// <param name="exporterDN">The distinguished name of the certificate used for authenticating the current user, for logging purposes.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the enumeration of users upon success.</returns>
		Task<IEnumerable<User>> ListUsersAsync(string appName, KeyId? recipientKeyId, string exporterDN, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously opens a session using delegated authentication by validating the given upstream authorization header <paramref name="authHeader"/>
		/// with the trusted upstream backend configured in <paramref name="app"/> and the looking up a user registration with the resulting user id as the upstream user id.
		/// </summary>
		/// <param name="app">The application for which the authentication shall be attempted.</param>
		/// <param name="authHeader">The upstream authorization header provided by the client and to be passed to the upstream backend.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>
		/// A task object representing the operation, providing the response DTO to pass to the client,
		/// containing user ids and the session authorization token for the opened analytics session.
		/// </returns>
		Task<DelegatedLoginResponseDTO> OpenSessionFromUpstreamAsync(ApplicationWithUserProperties app, string authHeader, CancellationToken ct = default);

		/// <summary>
		/// Asynchronously registers a user with the registration data from the given <see cref="UserRegistrationDTO"/> reveived from the client.
		/// </summary>
		/// <param name="userRegistration">Contains the data for the user that is being created.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the created user object as its result upon success.</returns>
		Task<User> RegisterUserAsync(UserRegistrationDTO userRegistration, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously updates the data of the given user.
		/// </summary>
		/// <param name="user">The user to update the data of.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the updated user object as its result upon success.</returns>
		Task<User> UpdateUserAsync(User user, CancellationToken ct = default);
	}
}
