using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGL.Analytics.DTO;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	/// <summary>
	/// Provides the <see cref="AddUserRegistrationRestClient(IServiceCollection, IConfiguration)"/> extension method.
	/// </summary>
	public static class UserRegistrationRestClientExtensions {
		/// <summary>
		/// Adds <see cref="UserRegistrationRestClient"/> as the implementation for <see cref="IUserRegistrationClient"/> in the service collection,
		/// using the given <paramref name="config"/> as a config root to retrieve the client configuration under the key <c>UserRegistrationRestClient</c>.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The config root to use.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection AddUserRegistrationRestClient(this IServiceCollection services, IConfiguration config) {
			services.Configure<UserRegistrationRestClientOptions>(config.GetSection(UserRegistrationRestClientOptions.UserRegistrationRestClient));
			services.AddScoped<IUserRegistrationClient, UserRegistrationRestClient>();
			return services;
		}
	}

	/// <summary>
	/// Encapsulates the configuration options for <see cref="UserRegistrationRestClient"/>.
	/// </summary>
	public class UserRegistrationRestClientOptions {
		/// <summary>
		/// The key under which the configuration options are looked up, <c>UserRegistrationRestClient</c>.
		/// </summary>
		public const string UserRegistrationRestClient = "UserRegistrationRestClient";
		/// <summary>
		/// The default relative API route for the user registration, <c>api/analytics/user</c>.
		/// </summary>
		public const string UserRegistrationApiRouteDefault = "api/analytics/user";
		/// <summary>
		/// The default relative API route for the login, <c>api/analytics/user/login</c>.
		/// </summary>
		public const string LoginApiRouteDefault = "api/analytics/user/login";

		/// <summary>
		/// The base URI of the backend server to use, defaults to <see cref="SGLAnalytics.DefaultBackendBaseUri"/>.
		/// </summary>
		public Uri BackendServerBaseUri { get; set; } = SGLAnalytics.DefaultBackendBaseUri;
		/// <summary>
		/// The relative API route to use for user registration, defaults to <see cref="UserRegistrationApiRouteDefault"/>.
		/// </summary>
		public Uri UserRegistrationApiRoute { get; set; } = new Uri(UserRegistrationApiRouteDefault, UriKind.Relative);
		/// <summary>
		/// The relative API route to user for login, defaults to <see cref="LoginApiRouteDefault"/>.
		/// </summary>
		public Uri LoginApiRoute { get; set; } = new Uri(LoginApiRouteDefault, UriKind.Relative);
	}

	/// <summary>
	/// An implementation of <see cref="IUserRegistrationClient"/> that uses REST API calls to communicate with the user registration backend.
	/// </summary>
	public class UserRegistrationRestClient : IUserRegistrationClient {
		private readonly HttpClient httpClient = new();
		private Uri userRegistrationApiRoute;
		private Uri loginApiRoute;

		/// <summary>
		/// Creates a client object that uses <see cref="SGLAnalytics.DefaultBackendBaseUri"/> as the backend server URI.
		/// </summary>
		public UserRegistrationRestClient() : this(SGLAnalytics.DefaultBackendBaseUri) { }
		/// <summary>
		/// Creates a client object that uses the given base URI of the backend server and the standard API URIs, <c>api/analytics/user</c> for registration, and <c>api/analytics/user/login</c> for login.
		/// </summary>
		/// <param name="backendServerBaseUri">The base URI of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		public UserRegistrationRestClient(Uri backendServerBaseUri) :
			this(backendServerBaseUri,
				new Uri(UserRegistrationRestClientOptions.UserRegistrationApiRouteDefault, UriKind.Relative),
				new Uri(UserRegistrationRestClientOptions.LoginApiRouteDefault, UriKind.Relative)
				) { }
		/// <summary>
		/// Creates a client object with the given configuration options.
		/// </summary>
		/// <param name="options">The configuration options to use.</param>
		public UserRegistrationRestClient(UserRegistrationRestClientOptions options) :
			this(options.BackendServerBaseUri, options.UserRegistrationApiRoute, options.LoginApiRoute) { }
		/// <summary>
		/// Creates a client object with the given configuration options.
		/// </summary>
		/// <param name="options">The configuration options to use.</param>
		public UserRegistrationRestClient(IOptions<UserRegistrationRestClientOptions> options) : this(options.Value) { }
		/// <summary>
		/// Creates a client object that uses the given base URI of the backend server and the given relative API endpoints below it as the targets for the requests.
		/// </summary>
		/// <param name="backendServerBaseUri">The base URI of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		/// <param name="userRegistrationApiRoute">The relative URI under <paramref name="backendServerBaseUri"/> to the user registration API endpoint, e.g. <c>api/analytics/user</c>.</param>
		/// <param name="loginApiRoute">The relative URI under <paramref name="backendServerBaseUri"/> to the login API endpoint, e.g. <c>api/analytics/user/login</c>.</param>
		public UserRegistrationRestClient(Uri backendServerBaseUri, Uri userRegistrationApiRoute, Uri loginApiRoute) {
			httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SGL.Analytics.Client", null));
			httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			httpClient.BaseAddress = backendServerBaseUri;
			this.userRegistrationApiRoute = userRegistrationApiRoute;
			this.loginApiRoute = loginApiRoute;
			httpClient.DefaultRequestVersion = HttpVersion.Version20;
		}

		/// <inheritdoc/>
		public async Task<AuthorizationToken> LoginUserAsync(LoginRequestDTO loginDTO) {
			var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
			var content = JsonContent.Create(loginDTO, new MediaTypeHeaderValue("application/json"), options);
			var response = await httpClient.PostAsync(loginApiRoute, content);
			if (response is null) throw new LoginErrorException("Did not receive a valid response for the login request.");
			try {
				response.EnsureSuccessStatusCode();
			}
			catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) {
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
			var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
				WriteIndented = true,
				DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
			};
			var content = JsonContent.Create(userDTO, new MediaTypeHeaderValue("application/json"), options);
			content.Headers.Add("App-API-Token", appAPIToken);
			var response = await httpClient.PostAsync(userRegistrationApiRoute, content);
			if (response is null) throw new UserRegistrationResponseException("Did not receive a valid response for the user registration request.");
			if (response.StatusCode == HttpStatusCode.Conflict && userDTO.Username != null) throw new UsernameAlreadyTakenException(userDTO.Username);
			response.EnsureSuccessStatusCode();
			var result = await response.Content.ReadFromJsonAsync<UserRegistrationResultDTO>(options);
			return result ?? throw new UserRegistrationResponseException("Did not receive a valid response for the user registration request.");
		}
	}
}
