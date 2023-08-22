using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	/// <summary>
	/// Specifies the interface for authentication implementations that authenticate an exporter client and its user with the backend
	/// and returns an authorization token.
	/// </summary>
	public interface IExporterAuthenticator {
		/// <summary>
		/// Asynchronously authenticates the client for app <paramref name="appName"/> using credentials present in the implementation object.
		/// </summary>
		/// <param name="appName">The unique name of the app for which to authenticate.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A task representing the operation, returning the authorization data for the session opon success.</returns>
		Task<AuthorizationData> AuthenticateAsync(string appName, CancellationToken ct = default);
	}
}
