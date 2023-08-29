using SGL.Analytics.DTO;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	/// <summary>
	/// Specifies the interface for a client object that is used to validate upstream authorization tokens with an upstream backend.
	/// </summary>
	public interface IUpstreamTokenClient {
		/// <summary>
		/// Validate the given authorization token <paramref name="authHeader"/> with the upstream backend to which <paramref name="upstreamBackendUrl"/> points.
		/// </summary>
		/// <param name="appName">The registered name of the application to put into the request DTO passed to the upstream backend.</param>
		/// <param name="appApiToken">The <c>App-API-Token</c> header value to put into the request to the upstream backend.</param>
		/// <param name="upstreamBackendUrl">The full URL to which to POST the validation request.</param>
		/// <param name="authHeader">The authorization header passed from the client to use on the request to the upstream backend.</param>
		/// <param name="ct">A cancellation token to allow cancelling the operation.</param>
		/// <returns>A task object representing the operation, providng the response DTO upon success.</returns>
		/// <exception cref="HttpApiResponseException">When the backend responds with an error code.</exception>
		/// <exception cref="HttpApiRequestFailedException">When the request couldn't be send to the backend or no response was received, e.g. due to a network problem.</exception>
		Task<UpstreamTokenCheckResponse> CheckUpstreamAuthTokenAsync(string appName, string appApiToken, string upstreamBackendUrl, string authHeader, CancellationToken ct = default);
	}
}
