using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;

namespace SGL.Analytics.Client {
	/// <summary>
	/// An implementation of <see cref="ILogCollectorClient"/> that uses REST API calls to contact the log collector backend.
	/// </summary>
	public class LogCollectorRestClient : ILogCollectorClient {
		private readonly HttpClient httpClient;
		private static readonly Uri logApiRoute = new Uri("/api/analytics/log/v1", UriKind.Relative);
		private static readonly Uri recipientsApiRoute = new Uri("/api/analytics/log/v1/recipient-certificates", UriKind.Relative);

		/// <summary>
		/// Creates a client object that uses the given base URI of the backend server and the standard API URI <c>api/analytics/log/v1</c>.
		/// </summary>
		/// <param name="httpClient">The <see cref="HttpClient"/> to use for requests to the backend.
		/// The <see cref="HttpClient.BaseAddress"/> of the client needs to be set to the base URI of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		public LogCollectorRestClient(HttpClient httpClient) {
			this.httpClient = httpClient;
		}

		/// <inheritdoc/>
		public async Task LoadRecipientCertificatesAsync(string appName, string appAPIToken, CertificateStore targetCertificateStore) {
			var query = HttpUtility.ParseQueryString("");
			query.Add("appName", appName);
			var uriBuilder = new UriBuilder(new Uri(httpClient.BaseAddress, recipientsApiRoute));
			uriBuilder.Query = query.ToString();
			using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
			request.Headers.Add("App-API-Token", appAPIToken);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-pem-file"));
			request.Version = HttpVersion.Version20;
			await targetCertificateStore.LoadCertificatesFromHttpAsync(httpClient, request);
		}

		/// <inheritdoc/>
		public async Task UploadLogFileAsync(string appName, string appAPIToken, AuthorizationToken authToken, ILogStorage.ILogFile logFile) {
			using (var stream = logFile.OpenReadRaw()) {
				var content = new StreamContent(stream);
				LogMetadataDTO dto = new LogMetadataDTO(logFile.ID, logFile.CreationTime, logFile.EndTime, logFile.Suffix, logFile.Encoding);
				Validator.ValidateObject(dto, new ValidationContext(dto), true);
				content.Headers.MapDtoProperties(dto);
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				using var request = new HttpRequestMessage(HttpMethod.Post, logApiRoute);
				request.Content = content;
				request.Headers.Add("App-API-Token", appAPIToken);
				request.Headers.Authorization = authToken.ToHttpHeaderValue();
				request.Version = HttpVersion.Version20;
				using var response = await httpClient.SendAsync(request);
				if (response.StatusCode == HttpStatusCode.Unauthorized && response.Headers.WwwAuthenticate.Count > 0) {
					throw new LoginRequiredException();
				}
				try {
					response.EnsureSuccessStatusCode();
				}
				catch (HttpRequestException ex) when (response?.StatusCode == HttpStatusCode.Unauthorized) {
					throw new UnauthorizedException(ex);
				}
				catch (HttpRequestException ex) when (response?.StatusCode == HttpStatusCode.RequestEntityTooLarge) {
					throw new FileTooLargeException(ex);
				}
				catch {
					throw;
				}
			}
		}
	}
}