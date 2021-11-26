using SGL.Analytics.Backend.Users.Application.Model;
using SGL.Analytics.DTO;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	/// <summary>
	/// Specifies the interface for high-level user manager class that manages <see cref="User"/> objects as a high-level representation of
	/// registered users and their associated application-specfic properties.
	/// </summary>
	public interface IUserManager {
		/// <summary>
		/// Asynchronously obtains the user object with the given id if it exists.
		/// </summary>
		/// <param name="userId">The unique id of the user.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The user object if the user exists, or <see langword="null"/> otherwise.</returns>
		Task<User?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
		Task<User?> GetUserByUsernameAndAppNameAsync(string username, string appName, CancellationToken ct = default);
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
