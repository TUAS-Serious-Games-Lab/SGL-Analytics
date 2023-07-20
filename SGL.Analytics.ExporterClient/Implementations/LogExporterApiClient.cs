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
	public class LogExporterApiClient : HttpApiClientBase, ILogExporterApiClient {
		private static readonly MediaTypeWithQualityHeaderValue octetStreamMT = MediaTypeWithQualityHeaderValue.Parse("application/octet-stream");
		private static readonly MediaTypeWithQualityHeaderValue jsonMT = MediaTypeWithQualityHeaderValue.Parse("application/json");
		private static readonly MediaTypeWithQualityHeaderValue pemMT = MediaTypeWithQualityHeaderValue.Parse("application/x-pem-file");
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonOptions.RestOptions);

		public LogExporterApiClient(HttpClient httpClient, AuthorizationData authorization) : base(httpClient, authorization, "/api/analytics/log/v2") { }

		public async Task<Stream> GetLogContentByIdAsync(Guid id, CancellationToken ct = default) {
			var response = await SendRequest(HttpMethod.Get, $"{id}/content", null, req => { }, accept: octetStreamMT, ct).ConfigureAwait(false);
			return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
		}

		public async Task<IEnumerable<Guid>> GetLogIdListAsync(CancellationToken ct = default) {
			using var response = await SendRequest(HttpMethod.Get, "", null, req => { }, accept: jsonMT, ct).ConfigureAwait(false);
			return (await response.Content.ReadFromJsonAsync<List<Guid>>(jsonOptions, ct).ConfigureAwait(false)) ?? Enumerable.Empty<Guid>();
		}
		public async Task<IEnumerable<DownstreamLogMetadataDTO>> GetMetadataForAllLogsAsync(KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var queryParameters = Enumerable.Empty<KeyValuePair<string, string>>();
			if (recipientKeyId != null) {
				queryParameters = new List<KeyValuePair<string, string>> { new("recipient", recipientKeyId.ToString() ?? "") };
			}
			using var response = await SendRequest(HttpMethod.Get, "all", queryParameters, null, req => { }, accept: jsonMT, ct: ct).ConfigureAwait(false);
			return (await response.Content.ReadFromJsonAsync<List<DownstreamLogMetadataDTO>>(jsonOptions, ct).ConfigureAwait(false)) ?? Enumerable.Empty<DownstreamLogMetadataDTO>();
		}

		public async Task<DownstreamLogMetadataDTO> GetLogMetadataByIdAsync(Guid id, KeyId? recipientKeyId = null, CancellationToken ct = default) {
			var queryParameters = Enumerable.Empty<KeyValuePair<string, string>>();
			if (recipientKeyId != null) {
				queryParameters = new List<KeyValuePair<string, string>> { new("recipient", recipientKeyId.ToString() ?? "") };
			}
			using var response = await SendRequest(HttpMethod.Get, $"{id}/metadata", queryParameters, null, req => { }, accept: jsonMT, ct).ConfigureAwait(false);
			return (await response.Content.ReadFromJsonAsync<DownstreamLogMetadataDTO>(jsonOptions, ct).ConfigureAwait(false)) ?? throw new JsonException("Got null from response.");
		}

		public async Task<IReadOnlyDictionary<Guid, EncryptionInfo>> GetKeysForRekeying(KeyId keyId, KeyId targetKeyId, int offset, CancellationToken ct = default) {
			using var response = await SendRequest(HttpMethod.Get, $"rekey/{keyId}", new Dictionary<string, string> {
				["targetKeyId"] = targetKeyId.ToString() ?? throw new ArgumentNullException(nameof(targetKeyId) + "." + nameof(KeyId.ToString)),
				["offset"] = $"{offset}"
			}, null, req => { }, accept: jsonMT, ct);
			return (await response.Content.ReadFromJsonAsync<Dictionary<Guid, EncryptionInfo>>(jsonOptions, ct).ConfigureAwait(false)) ?? throw new JsonException("Got null from response.");
		}

		public async Task PutRekeyedKeys(KeyId keyId, IReadOnlyDictionary<Guid, DataKeyInfo> dataKeys, CancellationToken ct = default) {
			using var requestContent = JsonContent.Create(dataKeys, jsonMT, jsonOptions);
			using var response = await SendRequest(HttpMethod.Put, $"rekey/{keyId}", requestContent, req => { }, accept: jsonMT, ct);
		}

		public async Task GetRecipientCertificates(string appName, CertificateStore certificateStore, CancellationToken ct = default) {
			var queryParameters = new List<KeyValuePair<string, string>> { new("appName", appName) };
			using var response = await SendRequest(HttpMethod.Get, "recipient-certificates", queryParameters, null, req => { }, accept: pemMT, ct);
			await certificateStore.LoadCertificatesFromHttpAsync(response, ct);
		}
	}
}
