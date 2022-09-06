using System;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	/// <summary>
	/// The interface that root data storages need to implement.
	/// </summary>
	public interface IRootDataStore {
		/// <summary>
		/// Gets or sets the id of the registered user.
		/// If no user is registerd or the root data store implementation only uses the username, the property contains <see langword="null"/>.
		/// </summary>
		Guid? UserID { get; set; }
		/// <summary>
		/// Gets or sets the registered user's login secret used for authentication with the backend.
		/// If no user is registerd, the property contains <see langword="null"/>.
		/// </summary>
		string? UserSecret { get; set; }

		/// <summary>
		/// Gets or sets the registered user's username if one was used for registration.
		/// If no user is registerd or the registation was done without a username, the property contains <see langword="null"/>.
		/// </summary>
		string? Username { get; set; }

		/// <summary>
		/// Asynchronously writes the current data to disk to make them peristent.
		/// </summary>
		/// <returns>A task indicating the completion of the write operation.</returns>
		Task SaveAsync();
	}
}
