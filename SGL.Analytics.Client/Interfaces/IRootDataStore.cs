using System;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	/// <summary>
	/// The interface that root data storages need to implement.
	/// </summary>
	public interface IRootDataStore {
		/// <summary>
		/// Gets or sets the id of the registered user.
		/// If no user is registerd, the property contains <see langword="null"/>.
		/// </summary>
		Guid? UserID { get; set; }
		/// <summary>
		/// Gets or sets the registerd user's login secret used for authentication with the backend.
		/// </summary>
		string UserSecret { get; set; }

		/// <summary>
		/// Gets a data directory where other storages can store files locally.
		/// </summary>
		string DataDirectory { get; }

		/// <summary>
		/// Asynchronously writes the current data to disk to make them peristent.
		/// </summary>
		/// <returns>A task indicating the completion of the write operation.</returns>
		Task SaveAsync();
	}
}
