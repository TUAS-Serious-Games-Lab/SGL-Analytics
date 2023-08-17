using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Asn1.Ocsp;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SGL.Analytics.Client {

	/// <summary>
	/// An implementation of <see cref="IUserRegistrationClient"/> that uses REST API calls to communicate with the user registration backend.
	/// </summary>
	public class UserRegistrationRestClient : HttpApiClientBase, IUserRegistrationClient {
		private readonly string appName;
		private readonly string appApiToken;
		private static readonly Uri userRegistrationApiRoute = new Uri("/api/analytics/user/v1", UriKind.Relative);
		private readonly MediaTypeWithQualityHeaderValue pemMT = new MediaTypeWithQualityHeaderValue("application/x-pem-file");
		private readonly MediaTypeWithQualityHeaderValue jsonMT = new MediaTypeWithQualityHeaderValue("application/json");
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonOptions.RestOptions);

		/// <summary>
		/// Creates a client object that uses the given <see cref="HttpClient"/> and its associated <see cref="HttpClient.BaseAddress"/> to communicate with the backend at that address.
		/// </summary>
		/// <param name="httpClient">The <see cref="HttpClient"/> to use for requests to the backend.
		/// The <see cref="HttpClient.BaseAddress"/> of the client needs to be set to the base URI of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		/// <param name="appName">The technical name of the application used to identify it in the backend.</param>
		/// <param name="appApiToken">The API token for the application to authenticate it with the backend.</param>
		public UserRegistrationRestClient(HttpClient httpClient, string appName, string appApiToken) : base(httpClient, null, "/api/analytics/user/v1/") {
			this.appName = appName;
			this.appApiToken = appApiToken;
		}

		private void addApiTokenHeader(HttpRequestMessage request) {
			request.Headers.Add("App-API-Token", appApiToken);
		}

		/// <inheritdoc/>
		public Guid? AuthorizedUserId { get; set; } = null;

		/// <inheritdoc/>
		public event AsyncEventHandler<UserAuthenticatedEventArgs>? UserAuthenticated;

		/// <summary>
		/// The clock tolerance for <see cref="IApiClient.Authorization"/>.
		/// It is subtracted from the received expiry time to account for potential clock differences between the client and the server.
		/// </summary>
		public TimeSpan AuthorizationExpiryClockTolerance { get; set; } = TimeSpan.FromMinutes(5);

		/// <inheritdoc/>
		public async Task LoadRecipientCertificatesAsync(CertificateStore targetCertificateStore, CancellationToken ct = default) {
			using var response = await SendRequest(HttpMethod.Get, "recipient-certificates",
				new Dictionary<string, string> { ["appName"] = appName },
				null, addApiTokenHeader, pemMT, ct, authenticated: false);
			await targetCertificateStore.LoadCertificatesFromHttpAsync(response, ct);
		}

		/// <inheritdoc/>
		public async Task<LoginResponseDTO> LoginUserAsync(LoginRequestDTO loginDTO, CancellationToken ct = default) {
			if (loginDTO.AppName != appName) {
				throw new ArgumentException("AppName of passed DTO doesn't match appName of REST client.", nameof(loginDTO));
			}
			if (loginDTO.AppApiToken != appApiToken) {
				throw new ArgumentException("AppApiToken of passed DTO doesn't match appApiToken of REST client.", nameof(loginDTO));
			}
			try {
				using var response = await SendRequest(HttpMethod.Post, "login", JsonContent.Create(loginDTO, jsonMT, jsonOptions),
					_ => { }, jsonMT, ct, authenticated: false);
				var result = (await response.Content.ReadFromJsonAsync<LoginResponseDTO>(jsonOptions)) ?? throw new JsonException("Got null from response.");
				DateTime? expiry = result.TokenExpiry;
				Guid? userId = result.UserId;
				if (!(expiry.HasValue && userId.HasValue)) {
					// New backend should provide decoded expiry and userId. If it doesn't decode it ourself and complete response DTO:
					try {
						var token = (new JwtSecurityTokenHandler()).ReadJwtToken(result.Token.Value);
						expiry ??= token.ValidTo.ToUniversalTime() - AuthorizationExpiryClockTolerance;
						userId ??= Guid.TryParse(token.Claims.FirstOrDefault(c => c.Type == "userid")?.Value, out var uId) ? uId : Guid.Empty;
					}
					catch {
						expiry ??= DateTime.UtcNow.AddMinutes(5);
						userId ??= Guid.Empty;
					}
					result = new LoginResponseDTO(result.Token, userId, expiry);
				}
				Authorization = new AuthorizationData(result.Token, expiry.Value);
				AuthorizedUserId = userId;

				await (UserAuthenticated?.InvokeAllAsync(this, new UserAuthenticatedEventArgs(Authorization.Value, AuthorizedUserId.Value)) ?? Task.CompletedTask);
				return result;
			}
			catch (HttpApiResponseException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) {
				throw new LoginFailedException();
			}
			catch (LoginErrorException) { throw; }
			catch (Exception ex) {
				throw new LoginErrorException(ex);
			}
		}

		/// <inheritdoc/>
		public async Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userDTO, CancellationToken ct = default) {
			if (userDTO.AppName != appName) {
				throw new ArgumentException("AppName of passed DTO doesn't match appName of REST client.", nameof(userDTO));
			}
			try {
				using var response = await SendRequest(HttpMethod.Post, "",
					JsonContent.Create(userDTO, new MediaTypeHeaderValue("application/json"), jsonOptions),
					addApiTokenHeader, jsonMT, ct, authenticated: false);
				var result = response != null ? await response.Content.ReadFromJsonAsync<UserRegistrationResultDTO>(jsonOptions) : null;
				if (result == null) {
					throw new UserRegistrationResponseException("Did not receive a valid response for the user registration request.");
				}
				return result;
			}
			catch (HttpApiResponseException ex) when (ex.StatusCode == HttpStatusCode.Conflict && userDTO.Username != null) {
				throw new UsernameAlreadyTakenException(userDTO.Username, ex);
			}
			catch (Exception) {
				throw;
			}
		}

		public async Task<DelegatedLoginResponseDTO> OpenSessionFromUpstream(AuthorizationData upstreamAuthToken, CancellationToken ct = default) {
			try {
				using var response = await SendRequest(HttpMethod.Post, "open-session-from-upstream",
					new Dictionary<string, string> { ["appName"] = appName }, JsonContent.Create(
						new UpstreamSessionRequestDTO(appName, appApiToken, upstreamAuthToken.Token.ToString() ?? throw new NullReferenceException()),
						jsonMT, jsonOptions), addApiTokenHeader, jsonMT, ct, authenticated: false);
				var result = (await response.Content.ReadFromJsonAsync<DelegatedLoginResponseDTO>(jsonOptions, ct)) ?? throw new JsonException("Got null from response.");
				Authorization = new AuthorizationData(result.Token, result.TokenExpiry ?? throw new LoginErrorException("Token expiry time missing.", new NullReferenceException()));
				AuthorizedUserId = result.UserId ?? throw new LoginErrorException("User ID missing.", new NullReferenceException());
				await (UserAuthenticated?.InvokeAllAsync(this, new UserAuthenticatedEventArgs(Authorization.Value, AuthorizedUserId.Value)) ?? Task.CompletedTask);
				return result;
			}
			catch (HttpApiResponseException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) {
				throw new LoginFailedException();
			}
			catch (HttpApiResponseException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
				throw new NoDelegatedUserException();
			}
			catch (Exception ex) {
				throw new LoginErrorException(ex);
			}
		}
	}
}
