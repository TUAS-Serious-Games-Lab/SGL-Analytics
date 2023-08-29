using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	/// <summary>
	/// Encapsulates options for queries on <see cref="IUserRepository"/>.
	/// </summary>
	public class UserQueryOptions {
		/// <summary>
		/// If true, indicates that the (non-encrypted, datastore-mapped) properties of each user registration shall be fetched.
		/// </summary>
		public bool FetchProperties { get; set; } = false;
		/// <summary>
		/// If true, indicates that all recipient data keys for the encrypted user properties for each user registration shall be fetched.
		/// </summary>
		public bool FetchRecipientKeys { get; set; } = false;
		/// <summary>
		/// If set, indicates that recipient data keys for the encrypted user properties for the given recipient key id shall be fetched for each user registration.
		/// </summary>
		public KeyId? FetchRecipientKey { get; set; } = null;
		/// <summary>
		/// If set, limits the number of results to return.
		/// </summary>
		public int Limit { get; set; } = 0;
		/// <summary>
		/// If set, indicates that the given number of results shall be skipped at the start.
		/// </summary>
		public int Offset { get; set; } = 0;
		/// <summary>
		/// Indicates the sorting order for the results.
		/// </summary>
		public UserQuerySortCriteria Ordering { get; set; } = UserQuerySortCriteria.Unordered;
		/// <summary>
		/// If true, indicates that the objects are fetched for updating and
		/// need to be tracked internally to support calling an update method on them to save changes.
		/// </summary>
		public bool ForUpdating { get; set; } = false;
	}
	/// <summary>
	/// Describes sorting criteria for query results in <see cref="IUserRepository"/>.
	/// </summary>
	public enum UserQuerySortCriteria {
		/// <summary>
		/// Specify no ordering criteria, the ordering is implementation-defined or unspecified.
		/// </summary>
		Unordered,
		/// <summary>
		/// Order the results by user id.
		/// </summary>
		UserId
	}
	/// <summary>
	/// Specifies the interface for a repository to store <see cref="UserRegistration"/> objects.
	/// </summary>
	public interface IUserRepository {
		/// <summary>
		/// Asynchronously obtains the user registration with the given id if it exists.
		/// </summary>
		/// <param name="id">The unique id of the user registration.</param>
		/// <param name="queryOptions">A class that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The user registration object if the user registration exists, or <see langword="null"/> otherwise.</returns>
		Task<UserRegistration?> GetUserByIdAsync(Guid id, UserQueryOptions? queryOptions = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the user registrations for multiple given ids.
		/// </summary>
		/// <param name="ids">The unique ids of the user registrations.</param>
		/// <param name="queryOptions">An object that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the user registrations found with the given ids.</returns>
		Task<IEnumerable<UserRegistration>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> ids, UserQueryOptions? queryOptions = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the user object with the given username in the application given by name if such a user exists.
		/// </summary>
		/// <param name="username">The per-application unique username of the user.</param>
		/// <param name="appName">The application with which the username is associated.</param>
		/// <param name="queryOptions">A class that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The user registration object if the user registration exists, or <see langword="null"/> otherwise.</returns>
		Task<UserRegistration?> GetUserByUsernameAndAppNameAsync(string username, string appName, UserQueryOptions? queryOptions = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains the user registration with the given <see cref="UserRegistration.BasicFederationUpstreamUserId"/> if it exists.
		/// </summary>
		/// <param name="upstreamUserId">
		/// The <see cref="UserRegistration.BasicFederationUpstreamUserId"/> of the user registration to find.
		/// This is the user id of the upstream account with which the analytics user account is associated.
		/// </param>
		/// <param name="appName">The application with which the user is associated.</param>
		/// <param name="queryOptions">A class that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The user registration object if the user registration exists, or <see langword="null"/> otherwise.</returns>
		Task<UserRegistration?> GetUserByBasicFederationUpstreamUserIdAsync(Guid upstreamUserId, string appName, UserQueryOptions? queryOptions = null, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously obtains user registrations for a given application.
		/// </summary>
		/// <param name="appName">The unique name of the application for which to fetch the user registrations.</param>
		/// <param name="notForKeyId">
		/// If set, only fetches user registrations that don't have a data key (for their encrypted user properties)
		/// for the recipient key-pair with the given key id.
		/// </param>
		/// <param name="queryOptions">An object that encapsulates options for querying methods, e.g. whether related entities should be fetched.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the fetched objects as its result.</returns>
		Task<IEnumerable<UserRegistration>> ListUsersAsync(string appName, KeyId? notForKeyId = null, UserQueryOptions? queryOptions = null, CancellationToken ct = default);
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
		/// <summary>
		/// Asynchronously save updates that were made to the given <see cref="UserRegistration"/> objects.
		/// </summary>
		/// <param name="userRegs">The objects to update.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the updated objects as its result.</returns>
		Task<IList<UserRegistration>> UpdateUsersAsync(IList<UserRegistration> userRegs, CancellationToken ct = default);
	}
}
