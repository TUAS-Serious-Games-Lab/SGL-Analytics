using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class FakeUserRegistrationClient : IUserRegistrationClient {
		public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.NoContent;
		public Dictionary<Guid, UserRegistrationDTO> RegistrationData { get; } = new();
		public List<UserRegistrationResultDTO> RegistrationResults { get; } = new();

		public async Task<LoginResponseDTO> LoginUserAsync(LoginRequestDTO loginDTO) {
			await Task.CompletedTask;
			return new LoginResponseDTO("");
		}

		public async Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userDTO, string appAPIToken) {
			await Task.CompletedTask;
			var resp = new HttpResponseMessage(StatusCode);
			resp.EnsureSuccessStatusCode();
			var result = new UserRegistrationResultDTO(Guid.NewGuid());
			RegistrationResults.Add(result);
			RegistrationData[result.UserId] = userDTO;
			return result;
		}
	}
}
