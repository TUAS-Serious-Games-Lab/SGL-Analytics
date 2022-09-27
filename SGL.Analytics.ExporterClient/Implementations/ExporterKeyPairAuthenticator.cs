using Microsoft.Extensions.Logging;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Keys;
using SGL.Utilities.Crypto.Signatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public class ExporterKeyPairAuthenticator : IExporterAuthenticator {
		private readonly HttpClient httpClient;
		private readonly KeyPair keyPair;
		private readonly ILogger<ExporterKeyPairAuthenticator> logger;
		private readonly RandomGenerator randomGenerator;
		private readonly MediaTypeHeaderValue? jsonContentType = new MediaTypeHeaderValue("application/json");
		private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonOptions.RestOptions);

		public ExporterKeyPairAuthenticator(HttpClient httpClient, KeyPair keyPair, ILogger<ExporterKeyPairAuthenticator> logger, RandomGenerator randomGenerator) {
			this.httpClient = httpClient;
			this.keyPair = keyPair;
			this.logger = logger;
			this.randomGenerator = randomGenerator;
		}

		public async Task<AuthorizationData> AuthenticateAsync(string appName, CancellationToken ct = default) {
			var openRequest = new HttpRequestMessage(HttpMethod.Post, "api/analytics/user/v1/exporter-key-auth/open-challenge");
			var requestDto = new ExporterKeyAuthRequestDTO(appName, keyPair.Public.CalculateId());
			openRequest.Content = JsonContent.Create(requestDto, jsonContentType, jsonOptions);

			var openResponse = await httpClient.SendAsync(openRequest, ct).ConfigureAwait(false);
			openResponse.EnsureSuccessStatusCode();
			var challengeDto = await openResponse.Content.ReadFromJsonAsync<ExporterKeyAuthChallengeDTO>(jsonOptions, ct).ConfigureAwait(false);
			if (challengeDto == null) {
				throw new InvalidDataException("Received null JSON from server.");
			}

			var signatureContent = ExporterKeyAuthSignatureDTO.ConstructContentToSign(requestDto, challengeDto);
			var signatureGenerator = new SignatureGenerator(keyPair.Private, challengeDto.DigestAlgorithmToUse, randomGenerator);
			signatureGenerator.ProcessBytes(signatureContent);
			var signature = signatureGenerator.Sign();
			var signatureDto = new ExporterKeyAuthSignatureDTO(challengeDto.ChallengeId, signature);
			var completeRequest = new HttpRequestMessage(HttpMethod.Post, "api/analytics/user/v1/exporter-key-auth/complete-challenge");
			completeRequest.Content = JsonContent.Create(signatureDto, jsonContentType, jsonOptions);

			var completeResponse = await httpClient.SendAsync(completeRequest, ct).ConfigureAwait(false);
			completeResponse.EnsureSuccessStatusCode();
			var responseDto = await completeResponse.Content.ReadFromJsonAsync<ExporterKeyAuthResponseDTO>(jsonOptions, ct).ConfigureAwait(false);
			if (responseDto == null) {
				throw new InvalidDataException("Received null JSON from server.");
			}
			return new AuthorizationData(responseDto.Token, responseDto.TokenExpiry.ToUniversalTime());
		}
	}
}
