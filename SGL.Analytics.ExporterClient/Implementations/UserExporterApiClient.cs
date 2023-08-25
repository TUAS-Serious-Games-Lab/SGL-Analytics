using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
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
	/// <summary>
	/// An implementation of <see cref="IUserExporterApiClient"/> that interacts with the backend using REST API calls.
	/// </summary>
	public class UserExporterApiClient : HttpApiClientBase, IUserExporterApiClient {
		private static readonly MediaTypeWithQualityHeaderValue jsonMT = MediaTypeWithQualityHeaderValue.Parse("application/json");
		private static readonly MediaTypeWithQualityHeaderValue pemMT = MediaTypeWithQualityHeaderValue.Parse("application/x-pem-file");
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonOptions.RestOptions);

		/// <summary>
		/// Instantiates the client object with the given underlying HTTP client and session authorization token.
		/// </summary>
		/// <param name="httpClient">
		/// The client using which to make the API requests.
		/// Its <see cref="HttpClient.BaseAddress"/> indicates the backend host.
		/// </param>
		/// <param name="authorization">The session authorization token for the backend.</param>
		public UserExporterApiClient(HttpClient httpClient, AuthorizationData authorization) : base(httpClient, authorization, "/api/analytics/user/v1") { }

		/// <inheritdoc/>
		public async Task<IEnumerable<Guid>> GetUserIdListAsync(CancellationToken ct = default) {
			using var response = await SendRequest(HttpMethod.Get, "", null, req => { }, accept: jsonMT, ct).ConfigureAwait(false);
			return (await response.Content.ReadFromJsonAsync<List<Guid>>(jsonOptions, ct).ConfigureAwait(false)) ?? Enumerable.Empty<Guid>();
		}

		/// <inheritdoc/>
		public async Task<IEnumerable<UserMetadataDTO>> GetMetadataForAllUsersAsync(KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var queryParameters = Enumerable.Empty<KeyValuePair<string, string>>();
			if (recipientKeyId != null) {
				queryParameters = new List<KeyValuePair<string, string>> { new("recipient", recipientKeyId.ToString() ?? "") };
			}
			using var response = await SendRequest(HttpMethod.Get, "all", queryParameters, null, req => { }, accept: jsonMT, ct: ct).ConfigureAwait(false);
			return (await response.Content.ReadFromJsonAsync<List<UserMetadataDTO>>(jsonOptions, ct).ConfigureAwait(false)) ?? Enumerable.Empty<UserMetadataDTO>();
		}

		/// <inheritdoc/>
		public async Task<UserMetadataDTO> GetUserMetadataByIdAsync(Guid id, KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var queryParameters = Enumerable.Empty<KeyValuePair<string, string>>();
			if (recipientKeyId != null) {
				queryParameters = new List<KeyValuePair<string, string>> { new("recipient", recipientKeyId.ToString() ?? "") };
			}
			using var response = await SendRequest(HttpMethod.Get, $"{id}", queryParameters, null, req => { }, accept: jsonMT, ct).ConfigureAwait(false);
			return (await response.Content.ReadFromJsonAsync<UserMetadataDTO>(jsonOptions, ct).ConfigureAwait(false)) ?? throw new JsonException("Got null from response.");
		}

		/// <inheritdoc/>
		public async Task GetRecipientCertificates(string appName, CertificateStore certificateStore, CancellationToken ct) {
			var queryParameters = new List<KeyValuePair<string, string>> { new("appName", appName) };
			using var response = await SendRequest(HttpMethod.Get, "recipient-certificates", queryParameters, null, req => { }, accept: pemMT, ct);
			await certificateStore.LoadCertificatesFromHttpAsync(response, ct);
		}

		/// <inheritdoc/>
		public async Task<IReadOnlyDictionary<Guid, EncryptionInfo>> GetKeysForRekeying(KeyId keyId, KeyId targetKeyId, int offset, CancellationToken ct) {
			using var response = await SendRequest(HttpMethod.Get, $"rekey/{keyId}", new Dictionary<string, string> {
				["targetKeyId"] = targetKeyId.ToString() ?? throw new ArgumentNullException(nameof(targetKeyId) + "." + nameof(KeyId.ToString)),
				["offset"] = $"{offset}"
			}, null, req => { }, accept: jsonMT, ct);
			return (await response.Content.ReadFromJsonAsync<Dictionary<Guid, EncryptionInfo>>(jsonOptions, ct).ConfigureAwait(false)) ?? throw new JsonException("Got null from response.");
		}

		/// <inheritdoc/>
		public async Task PutRekeyedKeys(KeyId keyId, Dictionary<Guid, DataKeyInfo> dataKeys, CancellationToken ct) {
			using var requestContent = JsonContent.Create(dataKeys, jsonMT, jsonOptions);
			using var response = await SendRequest(HttpMethod.Put, $"rekey/{keyId}", requestContent, req => { }, accept: jsonMT, ct);
		}
	}
}
