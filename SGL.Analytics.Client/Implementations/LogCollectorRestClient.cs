using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace SGL.Analytics.Client {
	/// <summary>
	/// An implementation of <see cref="ILogCollectorClient"/> that uses REST API calls to contact the log collector backend.
	/// </summary>
	public class LogCollectorRestClient : ILogCollectorClient {
		private readonly HttpClient httpClient;
		private static readonly Uri logApiRoute = new Uri("/api/analytics/log/v2", UriKind.Relative);
		private static readonly Uri recipientsApiRoute = new Uri("/api/analytics/log/v2/recipient-certificates", UriKind.Relative);
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		/// <summary>
		/// Creates a client object that uses the given <see cref="HttpClient"/> and its associated <see cref="HttpClient.BaseAddress"/> to communicate with the backend at that address.
		/// </summary>
		/// <param name="httpClient">The <see cref="HttpClient"/> to use for requests to the backend.
		/// The <see cref="HttpClient.BaseAddress"/> of the client needs to be set to the base URI of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		public LogCollectorRestClient(HttpClient httpClient) {
			if (httpClient.BaseAddress == null) {
				throw new ArgumentNullException($"{nameof(httpClient)}.{nameof(HttpClient.BaseAddress)}");
			}
			this.httpClient = httpClient;
		}

		/// <inheritdoc/>
		public async Task LoadRecipientCertificatesAsync(string appName, string appAPIToken, CertificateStore targetCertificateStore) {
			var query = HttpUtility.ParseQueryString("");
			query.Add("appName", appName);
			var uriBuilder = new UriBuilder(new Uri(httpClient.BaseAddress ??
				throw new ArgumentNullException($"{nameof(httpClient)}.{nameof(HttpClient.BaseAddress)}"),
				recipientsApiRoute));
			uriBuilder.Query = query.ToString();
			using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
			request.Headers.Add("App-API-Token", appAPIToken);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-pem-file"));
			request.Version = HttpVersion.Version20;
			await targetCertificateStore.LoadCertificatesFromHttpAsync(httpClient, request);
		}

		/// <inheritdoc/>
		public async Task UploadLogFileAsync(string appName, string appAPIToken, AuthorizationToken authToken, LogMetadataDTO metadata, Stream content) {
			if (httpClient.BaseAddress == null) {
				throw new ArgumentNullException($"{nameof(httpClient)}.{nameof(HttpClient.BaseAddress)}");
			}
			using (var multipartContent = new MultipartFormDataContent()) {
				var contentObj = new StreamContent(content);
				contentObj.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var metadataObj = JsonContent.Create(metadata, MediaTypeHeaderValue.Parse("application/json"), jsonOptions);
				multipartContent.Add(metadataObj, "metadata");
				multipartContent.Add(contentObj, "content");
				using var request = new HttpRequestMessage(HttpMethod.Post, logApiRoute);
				request.Content = multipartContent;
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