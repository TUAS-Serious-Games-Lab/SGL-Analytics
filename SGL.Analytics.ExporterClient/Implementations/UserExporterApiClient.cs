using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public class UserExporterApiClient : HttpApiClientBase, IUserExporterApiClient {
		private static readonly MediaTypeWithQualityHeaderValue jsonMT = MediaTypeWithQualityHeaderValue.Parse("application/json");
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonOptions.RestOptions);

		public UserExporterApiClient(HttpClient httpClient, AuthorizationData authorization) : base(httpClient, authorization, "/api/analytics/user/v1") { }

		public async Task<IEnumerable<Guid>> GetUserIdListAsync(CancellationToken ct = default) {
			using var response = await SendRequest(HttpMethod.Get, "", null, req => { }, accept: jsonMT, ct);
			return (await response.Content.ReadFromJsonAsync<List<Guid>>(jsonOptions, ct)) ?? Enumerable.Empty<Guid>();
		}

		public async Task<IEnumerable<UserMetadataDTO>> GetMetadataForAllUsersAsync(KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var queryParameters = Enumerable.Empty<KeyValuePair<string, string>>();
			if (recipientKeyId != null) {
				queryParameters = new List<KeyValuePair<string, string>> { new("recipient", recipientKeyId.ToString() ?? "") };
			}
			using var response = await SendRequest(HttpMethod.Get, "all", queryParameters, null, req => { }, accept: jsonMT, ct: ct);
			return (await response.Content.ReadFromJsonAsync<List<UserMetadataDTO>>(jsonOptions, ct)) ?? Enumerable.Empty<UserMetadataDTO>();
		}

		public async Task<UserMetadataDTO> GetUserMetadataByIdAsync(Guid id, KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var queryParameters = Enumerable.Empty<KeyValuePair<string, string>>();
			if (recipientKeyId != null) {
				queryParameters = new List<KeyValuePair<string, string>> { new("recipient", recipientKeyId.ToString() ?? "") };
			}
			using var response = await SendRequest(HttpMethod.Get, $"{id}", queryParameters, null, req => { }, accept: jsonMT, ct);
			return (await response.Content.ReadFromJsonAsync<UserMetadataDTO>(jsonOptions, ct)) ?? throw new JsonException("Got null from response.");
		}
	}
}
