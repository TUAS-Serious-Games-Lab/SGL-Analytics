using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	/// <summary>
	/// An implementation of <see cref="ILogCollectorClient"/> that uses REST API calls to contact the log collector backend.
	/// </summary>
	public class LogCollectorRestClient : ILogCollectorClient {
		private readonly static HttpClient httpClient = new();
		private Uri backendServerBaseUri;
		private Uri logCollectorApiEndpoint;
		private Uri logCollectorApiFullUri;

		static LogCollectorRestClient() {
			httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SGL.Analytics.Client", null));
			httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		}

		// TODO: Support configuration of URIs through general configuration system.

		/// <summary>
		/// Creates a client object that uses <see cref="SGLAnalytics.DefaultBackendBaseUri"/> as the backend server URI.
		/// </summary>
		public LogCollectorRestClient() : this(SGLAnalytics.DefaultBackendBaseUri) { }
		/// <summary>
		/// Creates a client object that uses the given base URI of the backend server and the standard API URI <c>api/analytics/log/v1</c>.
		/// </summary>
		/// <param name="backendServerBaseUri">The base URI of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		public LogCollectorRestClient(Uri backendServerBaseUri) : this(backendServerBaseUri, new Uri("api/analytics/log/v1", UriKind.Relative)) { }

		/// <summary>
		/// Creates a client object that uses the given base URI of the backend server and the given relative API endpoint below it as the target for the requests.
		/// </summary>
		/// <param name="backendServerBaseUri">The base URI of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		/// <param name="logCollectorApiEndpoint">The relative URI under <paramref name="backendServerBaseUri"/> to the API endpoint, e.g. <c>api/analytics/log</c>.</param>
		public LogCollectorRestClient(Uri backendServerBaseUri, Uri logCollectorApiEndpoint) {
			this.backendServerBaseUri = backendServerBaseUri;
			this.logCollectorApiEndpoint = logCollectorApiEndpoint;
			this.logCollectorApiFullUri = new Uri(backendServerBaseUri, logCollectorApiEndpoint);
		}

		public Task LoadRecipientCertificates(string appName, string appAPIToken, AuthorizationToken authToken, CertificateStore targetCertificateStore) {
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public async Task UploadLogFileAsync(string appName, string appAPIToken, AuthorizationToken authToken, ILogStorage.ILogFile logFile) {
			using (var stream = logFile.OpenReadRaw()) {
				var content = new StreamContent(stream);
				LogMetadataDTO dto = new LogMetadataDTO(logFile.ID, logFile.CreationTime, logFile.EndTime, logFile.Suffix, logFile.Encoding);
				Validator.ValidateObject(dto, new ValidationContext(dto), true);
				content.Headers.MapDtoProperties(dto);
				content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var request = new HttpRequestMessage(HttpMethod.Post, logCollectorApiFullUri);
				request.Content = content;
				request.Headers.Add("App-API-Token", appAPIToken);
				request.Headers.Authorization = authToken.ToHttpHeaderValue();
				request.Version = HttpVersion.Version20;
				var response = await httpClient.SendAsync(request);
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