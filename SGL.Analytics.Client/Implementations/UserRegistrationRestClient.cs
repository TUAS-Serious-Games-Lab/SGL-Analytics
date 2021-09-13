using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {

	public static class UserRegistrationRestClientExtensions {
		public static IServiceCollection AddUserRegistrationRestClient(this IServiceCollection services, IConfiguration config) {
			services.Configure<UserRegistrationRestClientOptions>(config.GetSection(UserRegistrationRestClientOptions.UserRegistrationRestClient));
			services.AddScoped<IUserRegistrationClient, UserRegistrationRestClient>();
			return services;
		}
	}

	public class UserRegistrationRestClientOptions {
		public const string UserRegistrationRestClient = "UserRegistrationRestClient";
		public const string BackendServerBaseUriDefault = "http://localhost:5001/";
		public const string UserRegistrationApiRouteDefault = "api/AnalyticsUser";
		public const string LoginApiRouteDefault = "api/AnalyticsUser/login";

		public Uri BackendServerBaseUri { get; set; } = new Uri(BackendServerBaseUriDefault);
		public Uri UserRegistrationApiRoute { get; set; } = new Uri(UserRegistrationApiRouteDefault, UriKind.Relative);
		public Uri LoginApiRoute { get; set; } = new Uri(LoginApiRouteDefault, UriKind.Relative);
	}

	public class UserRegistrationRestClient : IUserRegistrationClient {
		private readonly HttpClient httpClient = new();
		private Uri userRegistrationApiRoute;
		private Uri loginApiRoute;

		// TODO: Replace default URL with registered URL of Prod backend when available.
		public UserRegistrationRestClient() : this(new Uri(UserRegistrationRestClientOptions.BackendServerBaseUriDefault)) { }
		public UserRegistrationRestClient(Uri backendServerBaseUri) :
			this(backendServerBaseUri,
				new Uri(UserRegistrationRestClientOptions.UserRegistrationApiRouteDefault, UriKind.Relative),
				new Uri(UserRegistrationRestClientOptions.LoginApiRouteDefault, UriKind.Relative)
				) { }
		public UserRegistrationRestClient(UserRegistrationRestClientOptions options) :
			this(options.BackendServerBaseUri, options.UserRegistrationApiRoute, options.LoginApiRoute) { }
		public UserRegistrationRestClient(IOptions<UserRegistrationRestClientOptions> options) : this(options.Value) { }
		public UserRegistrationRestClient(Uri backendServerBaseUri, Uri userRegistrationApiRoute, Uri loginApiRoute) {
			httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SGL.Analytics.Client", null));
			httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			httpClient.BaseAddress = backendServerBaseUri;
			this.userRegistrationApiRoute = userRegistrationApiRoute;
			this.loginApiRoute = loginApiRoute;
		}

		public async Task<LoginResponseDTO> LoginUserAsync(LoginRequestDTO loginDTO) {
			var options = new JsonSerializerOptions() { WriteIndented = true };
			var content = JsonContent.Create(loginDTO, new MediaTypeHeaderValue("application/json"), options);
			var response = await httpClient.PostAsync(loginApiRoute, content);
			if (response is null) throw new LoginErrorException("Did not receive a valid response for the login request.");
			try {
				response.EnsureSuccessStatusCode();
			}
			catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden) {
				throw new LoginFailedException();
			}
			catch (Exception ex) {
				throw new LoginErrorException(ex);
			}
			var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>(options);
			return result ?? throw new LoginErrorException("Did not receive a valid response for the login request.");
		}

		public async Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userDTO, string appAPIToken) {
			var options = new JsonSerializerOptions() { WriteIndented = true };
			var content = JsonContent.Create(userDTO, new MediaTypeHeaderValue("application/json"), options);
			content.Headers.Add("App-API-Token", appAPIToken);
			var response = await httpClient.PostAsync(userRegistrationApiRoute, content);
			if (response is null) throw new UserRegistrationResponseException("Did not receive a valid response for the user registration request.");
			if (response.StatusCode == HttpStatusCode.Conflict) throw new UsernameAlreadyTakenException(userDTO.Username);
			response.EnsureSuccessStatusCode();
			var result = await response.Content.ReadFromJsonAsync<UserRegistrationResultDTO>(options);
			return result ?? throw new UserRegistrationResponseException("Did not receive a valid response for the user registration request.");
		}
	}
}
