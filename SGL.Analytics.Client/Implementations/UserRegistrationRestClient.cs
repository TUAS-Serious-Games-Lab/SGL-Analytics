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
		public UserRegistrationResponseException(string? message) : base(message) {}
		public UserRegistrationResponseException(string? message, Exception? innerException) : base(message, innerException) {}
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


	public class UserRegistrationRestClient : IUserRegistrationClient {
		private readonly static HttpClient httpClient = new();
		private Uri backendServerBaseUri;
		private Uri userRegistrationApiEndpoint;
		private Uri userRegistrationApiFullUri;

		static UserRegistrationRestClient() {
			httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SGL.Analytics.Client", null));
			httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		}

		// TODO: Support configuration of URIs through general configuration system.
		public UserRegistrationRestClient() : this(new Uri("http://localhost:5001/")) { }
		public UserRegistrationRestClient(Uri backendServerBaseUri) : this(backendServerBaseUri, new Uri("api/AnalyticsUser", UriKind.Relative)) { }
		public UserRegistrationRestClient(Uri backendServerBaseUri, Uri userRegistrationApiEndpoint) {
			this.backendServerBaseUri = backendServerBaseUri;
			this.userRegistrationApiEndpoint = userRegistrationApiEndpoint;
			this.userRegistrationApiFullUri = new Uri(backendServerBaseUri, userRegistrationApiEndpoint);
		}

		public async Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userDTO, string appAPIToken) {
			var options = new JsonSerializerOptions() { WriteIndented = true };
			var content = JsonContent.Create(userDTO, new MediaTypeHeaderValue("application/json"),options);
			content.Headers.Add("App-API-Token", appAPIToken);
			var response = await httpClient.PostAsync(userRegistrationApiFullUri, content);
			if (response is null) throw new UserRegistrationResponseException("Did not receive a valid response for the user registration request.");
			if (response.StatusCode == HttpStatusCode.Conflict) throw new UsernameAlreadyTakenException(userDTO.Username);
			response.EnsureSuccessStatusCode();
			var result = await response.Content.ReadFromJsonAsync<UserRegistrationResultDTO>(options);
			return result ?? throw new UserRegistrationResponseException("Did not receive a valid response for the user registration request.");
		}
	}
}
