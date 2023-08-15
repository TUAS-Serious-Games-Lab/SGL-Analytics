using SGL.Analytics.DTO;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.EndToEndTest {
	public class TestUpstreamClient : HttpApiClientBase {
		private readonly MediaTypeWithQualityHeaderValue jsonMT = new MediaTypeWithQualityHeaderValue("application/json");
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonOptions.RestOptions);
		public Guid? AuthorizedUserId { get; set; } = null;

		public TimeSpan AuthorizationExpiryClockTolerance { get; set; } = TimeSpan.FromMinutes(5);

		public TestUpstreamClient(HttpClient httpClient) : base(httpClient, null, "/api/analytics/test/upstream/v1/") { }

		public async Task<LoginResponseDTO> StartSession(string secret, CancellationToken ct = default) {
			using var response = await SendRequest(HttpMethod.Post, "start-session", new StringContent(secret), _ => { },
				jsonMT, ct, authenticated: false);
			var responseDto = (await response.Content.ReadFromJsonAsync<LoginResponseDTO>(jsonOptions, ct)) ?? throw new JsonException("Got null from response.");
			Authorization = new AuthorizationData(responseDto.Token, (responseDto.TokenExpiry ?? DateTime.MaxValue) - AuthorizationExpiryClockTolerance);
			AuthorizedUserId = responseDto.UserId;
			return responseDto;
		}
	}
}
