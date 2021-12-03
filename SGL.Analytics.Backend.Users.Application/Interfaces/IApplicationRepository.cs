using SGL.Analytics.Backend.Domain.Entity;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	/// <summary>
	/// Specifies the interface for a repository to store <see cref="ApplicationWithUserProperties"/> objects.
	/// </summary>
	public interface IApplicationRepository {
		/// <summary>
		/// Asynchronously obtains the application with the given name if it exists.
		/// </summary>
		/// <param name="appName">The unique technical name of the application.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the following result: The application object if the application exists, or <see langword="null"/> otherwise.</returns>
		Task<ApplicationWithUserProperties?> GetApplicationByNameAsync(string appName, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously creates the given application object in the repository.
		/// </summary>
		/// <param name="app">The application data for the application to create.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the created object as its result.</returns>
		Task<ApplicationWithUserProperties> AddApplicationAsync(ApplicationWithUserProperties app, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously updates the given application object in the repository.
		/// </summary>
		/// <param name="app">The updated application data.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the updated object as its result.</returns>
		Task<ApplicationWithUserProperties> UpdateApplicationAsync(ApplicationWithUserProperties app, CancellationToken ct = default);

		/// <summary>
		/// Asynchronously obtains a list of all registered applications.
		/// </summary>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providing the list as its result.</returns>
		Task<IList<ApplicationWithUserProperties>> ListApplicationsAsync(CancellationToken ct = default);
	}
}
