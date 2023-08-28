using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	/// <summary>
	/// Specifies the interface for a callback component that consumes retrieved and decrypted user registration data for further processing.
	/// </summary>
	public interface IUserRegistrationSink {
		/// <summary>
		/// Is called when the client has retrieved and decrypted a user registration record and passes the data along for asynchronous processing.
		/// </summary>
		/// <param name="userRegistrationData">The user data to process.</param>
		/// <param name="ct">A <see cref="CancellationToken"/> that is cancelled when the retrieval and processing method if cancelled.</param>
		/// <returns>A task object representing the asynchronous operation.</returns>
		Task ProcessUserRegistrationAsync(UserRegistrationData userRegistrationData, CancellationToken ct);
	}
}
