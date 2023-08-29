using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.DTO;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Services {
	/// <summary>
	/// Implements <see cref="IUpstreamTokenClient"/> using <see cref="HttpApiClientBase"/> ontop of <see cref="HttpClient"/>.
	/// </summary>
	public class UpstreamTokenClient : HttpApiClientBase, IUpstreamTokenClient {
		private readonly MediaTypeWithQualityHeaderValue jsonMT = new MediaTypeWithQualityHeaderValue("application/json");
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonOptions.RestOptions);
		/// <summary>
		/// Constructs the service object with the given <see cref="HttpClient"/>.
		/// </summary>
		public UpstreamTokenClient(HttpClient httpClient) : base(httpClient, null, "") { }

		/// <inheritdoc/>
		public async Task<UpstreamTokenCheckResponse> CheckUpstreamAuthTokenAsync(string appName, string appApiToken, string upstreamBackendUrl, string authHeader, CancellationToken ct = default) {
			var response = await SendRequest(HttpMethod.Post, upstreamBackendUrl, JsonContent.Create(new UpstreamTokenCheckRequest(appName), jsonMT, jsonOptions),
				req => {
					req.Headers.Add("App-API-Token", appApiToken);
					req.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
				},
				accept: jsonMT, ct: ct, authenticated: false);
			var result = (await response.Content.ReadFromJsonAsync<UpstreamTokenCheckResponse>(jsonOptions)) ?? throw new JsonException("Got null from response.");
			return result;
		}
	}
}
