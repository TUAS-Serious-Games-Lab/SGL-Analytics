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
	public class UserRegistrationResponseException : Exception {
		public UserRegistrationResponseException(string? message) : base(message) { }
		public UserRegistrationResponseException(string? message, Exception? innerException) : base(message, innerException) { }
	}

	public class UsernameAlreadyTakenException : Exception {
		public string Username { get; }
		public UsernameAlreadyTakenException(string username) : base($"The username '{username}' is already taken.") {
			Username = username;
		}
		public UsernameAlreadyTakenException(string username, Exception? innerException) : base($"The username '{username}' is already taken.", innerException) {
			Username = username;
		}
	}

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

		public Uri BackendServerBaseUri { get; set; } = new Uri(BackendServerBaseUriDefault);
		public Uri UserRegistrationApiRoute { get; set; } = new Uri(UserRegistrationApiRouteDefault, UriKind.Relative);
	}

	public class UserRegistrationRestClient : IUserRegistrationClient {
		private readonly HttpClient httpClient = new();
		private Uri userRegistrationApiRoute;

		// TODO: Replace default URL with registered URL of Prod backend when available.
		public UserRegistrationRestClient() : this(new Uri(UserRegistrationRestClientOptions.BackendServerBaseUriDefault)) { }
		public UserRegistrationRestClient(Uri backendServerBaseUri) : this(backendServerBaseUri, new Uri(UserRegistrationRestClientOptions.UserRegistrationApiRouteDefault, UriKind.Relative)) { }
		public UserRegistrationRestClient(UserRegistrationRestClientOptions options) : this(options.BackendServerBaseUri, options.UserRegistrationApiRoute) { }
		public UserRegistrationRestClient(IOptions<UserRegistrationRestClientOptions> options) : this(options.Value) { }
		public UserRegistrationRestClient(Uri backendServerBaseUri, Uri userRegistrationApiRoute) {
			httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SGL.Analytics.Client", null));
			httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			httpClient.BaseAddress = backendServerBaseUri;
			this.userRegistrationApiRoute = userRegistrationApiRoute;
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
