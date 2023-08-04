using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class FakeUserRegistrationClient : IUserRegistrationClient {
		public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.NoContent;
		public Dictionary<Guid, UserRegistrationDTO> RegistrationData { get; } = new();
		public List<UserRegistrationResultDTO> RegistrationResults { get; } = new();
		public List<LoginRequestDTO> LoginRequests { get; } = new();
		public List<Certificate> RecipientCertificates { get; set; } = new List<Certificate> { };

		public AuthorizationData? Authorization => new AuthorizationData(new AuthorizationToken("OK"), DateTime.MaxValue);
		public Guid? AuthorizedUserId => Guid.NewGuid();

		public event AsyncEventHandler<AuthorizationExpiredEventArgs>? AuthorizationExpired;
		public event AsyncEventHandler<UserAuthenticatedEventArgs>? UserAuthenticated;

		public Task LoadRecipientCertificatesAsync(CertificateStore targetCertificateStore, CancellationToken ct = default) {
			targetCertificateStore.AddCertificatesWithValidation(RecipientCertificates, nameof(FakeUserRegistrationClient));
			return Task.CompletedTask;
		}

		public async Task<AuthorizationToken> LoginUserAsync(LoginRequestDTO loginDTO, CancellationToken ct = default) {
			await Task.CompletedTask;
			LoginRequests.Add(loginDTO);
			await (UserAuthenticated?.InvokeAllAsync(this, new UserAuthenticatedEventArgs(Authorization!.Value, AuthorizedUserId!.Value), ct) ?? Task.CompletedTask);
			return new AuthorizationToken("OK");
		}

		public Task<LoginResponseDTO> OpenSessionFromUpstream(AuthorizationData upstreamAuthToken, CancellationToken ct = default) {
			// TODO: Implement
			throw new NotImplementedException();
		}

		public async Task<UserRegistrationResultDTO> RegisterUserAsync(UserRegistrationDTO userDTO, AuthorizationData? upstreamAuthToken = null, CancellationToken ct = default) {
			await Task.CompletedTask;
			var resp = new HttpResponseMessage(StatusCode);
			resp.EnsureSuccessStatusCode();
			var result = new UserRegistrationResultDTO(Guid.NewGuid());
			RegistrationResults.Add(result);
			RegistrationData[result.UserId] = userDTO;
			await (UserAuthenticated?.InvokeAllAsync(this, new UserAuthenticatedEventArgs(Authorization!.Value, AuthorizedUserId!.Value), ct) ?? Task.CompletedTask);
			return result;
		}

		public Task SetAuthorizationLockedAsync(AuthorizationData? value, CancellationToken ct = default) {
			throw new NotSupportedException();
		}
	}
}
