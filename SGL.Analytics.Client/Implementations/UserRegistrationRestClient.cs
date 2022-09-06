using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace SGL.Analytics.Client {

	/// <summary>
	/// An implementation of <see cref="IUserRegistrationClient"/> that uses REST API calls to communicate with the user registration backend.
	/// </summary>
	public class UserRegistrationRestClient : IUserRegistrationClient {
		private readonly HttpClient httpClient;
		private static readonly Uri userRegistrationApiRoute = new Uri("/api/analytics/user/v1", UriKind.Relative);
		private static readonly Uri loginApiRoute = new Uri("/api/analytics/user/v1/login", UriKind.Relative);
		private static readonly Uri recipientsApiRoute = new Uri("/api/analytics/user/v1/recipient-certificates", UriKind.Relative);

		/// <summary>
		/// Creates a client object that uses the given <see cref="HttpClient"/> and its associated <see cref="HttpClient.BaseAddress"/> to communicate with the backend at that address.
		/// </summary>
		/// <param name="httpClient">The <see cref="HttpClient"/> to use for requests to the backend.
		/// The <see cref="HttpClient.BaseAddress"/> of the client needs to be set to the base URI of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		public UserRegistrationRestClient(HttpClient httpClient) {
			if (httpClient.BaseAddress == null) {
				throw new ArgumentNullException($"{nameof(httpClient)}.{nameof(HttpClient.BaseAddress)}");
			}
			this.httpClient = httpClient;
		}

		/// <inheritdoc/>
		public async Task LoadRecipientCertificatesAsync(string appName, string appAPIToken, CertificateStore targetCertificateStore) {
			var fullUri = new Uri(httpClient.BaseAddress ??
				throw new ArgumentNullException($"{nameof(httpClient)}.{nameof(HttpClient.BaseAddress)}"),
				recipientsApiRoute);
			var query = HttpUtility.ParseQueryString(fullUri.Query);
			query.Add("appName", appName);
			var uriBuilder = new UriBuilder(fullUri);
			uriBuilder.Query = query.ToString();
			using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
			request.Headers.Add("App-API-Token", appAPIToken);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-pem-file"));
			request.Version = HttpVersion.Version20;
			await targetCertificateStore.LoadCertificatesFromHttpAsync(httpClient, request);
		}

		/// <inheritdoc/>
		public async Task<AuthorizationToken> LoginUserAsync(LoginRequestDTO loginDTO) {
			if (httpClient.BaseAddress == null) {
				throw new ArgumentNullException($"{nameof(httpClient)}.{nameof(HttpClient.BaseAddress)}");
			}
			var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
			var content = JsonContent.Create(loginDTO, new MediaTypeHeaderValue("application/json"), options);
			using var request = new HttpRequestMessage(HttpMethod.Post, loginApiRoute);
			request.Content = content;
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			request.Version = HttpVersion.Version20;
			using var response = await httpClient.SendAsync(request);
			if (response is null) throw new LoginErrorException("Did not receive a valid response for the login request.");
			try {
				response.EnsureSuccessStatusCode();
			}
			catch (HttpRequestException) when (response?.StatusCode == HttpStatusCode.Unauthorized) {
				throw new LoginFailedException();
			}
			catch (Exception ex) {
				throw new LoginErrorException(ex);
			}
			var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>(options);
			return result?.Token ?? throw new LoginErrorException("Did not receive a valid response for the login request.");
		}

		/// <inheritdoc/>
		public async Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userDTO, string appAPIToken) {
			if (httpClient.BaseAddress == null) {
				throw new ArgumentNullException($"{nameof(httpClient)}.{nameof(HttpClient.BaseAddress)}");
			}
			var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
				WriteIndented = true,
				DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
			};
			var content = JsonContent.Create(userDTO, new MediaTypeHeaderValue("application/json"), options);
			using var request = new HttpRequestMessage(HttpMethod.Post, userRegistrationApiRoute);
			request.Content = content;
			request.Headers.Add("App-API-Token", appAPIToken);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			request.Version = HttpVersion.Version20;
			using var response = await httpClient.SendAsync(request);
			if (response is null) throw new UserRegistrationResponseException("Did not receive a valid response for the user registration request.");
			if (response.StatusCode == HttpStatusCode.Conflict && userDTO.Username != null) throw new UsernameAlreadyTakenException(userDTO.Username);
			response.EnsureSuccessStatusCode();
			var result = await response.Content.ReadFromJsonAsync<UserRegistrationResultDTO>(options);
			return result ?? throw new UserRegistrationResponseException("Did not receive a valid response for the user registration request.");
		}
	}
}
