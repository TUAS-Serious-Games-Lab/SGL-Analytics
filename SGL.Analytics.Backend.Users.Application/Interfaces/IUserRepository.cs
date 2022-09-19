using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	/// <summary>
	/// Specifies the interface for a repository to store <see cref="UserRegistration"/> objects.
	/// </summary>
	public interface IUserRepository {
		/// <summary>
		/// Asynchronously obtains the user registration with the given id if it exists.
		/// </summary>
		/// <param name="id">The unique id of the user registration.</param>
		/// <param name="recipientKeyId">If specified, requests that the recipient key for the encrypted registration properties associated with the given key id is fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The user registration object if the user registration exists, or <see langword="null"/> otherwise.</returns>
		Task<UserRegistration?> GetUserByIdAsync(Guid id, KeyId? recipientKeyId = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the user object with the given username in the application given by name if such a user exists.
		/// </summary>
		/// <param name="username">The per-application unique username of the user.</param>
		/// <param name="appName">The application with which the username is associated.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The user registration object if the user registration exists, or <see langword="null"/> otherwise.</returns>
		Task<UserRegistration?> GetUserByUsernameAndAppNameAsync(string username, string appName, CancellationToken ct = default);
		Task<IEnumerable<UserRegistration>> ListUsersAsync(string appName, KeyId? recipientKeyId = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously creates the given user registration object in the repository.
		/// </summary>
		/// <param name="userReg">The user registration data for the user registration to create.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the created object as its result.</returns>
		Task<UserRegistration> RegisterUserAsync(UserRegistration userReg, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously updates the given user registration object in the repository.
		/// </summary>
		/// <param name="userReg">The updated user registration data.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the updated object as its result.</returns>
		Task<UserRegistration> UpdateUserAsync(UserRegistration userReg, CancellationToken ct = default);

		/// <summary>
		/// Asynchronously obtains the per-application counts of the registered users.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing an application name -> user count dictionary as its result.</returns>
		Task<IDictionary<string, int>> GetUsersCountPerAppAsync(CancellationToken ct = default);
	}
}
