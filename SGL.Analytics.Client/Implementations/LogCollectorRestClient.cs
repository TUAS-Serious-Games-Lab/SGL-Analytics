using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SGL.Analytics.Client {
	/// <summary>
	/// An implementation of <see cref="ILogCollectorClient"/> that uses REST API calls to contact the log collector backend.
	/// </summary>
	public class LogCollectorRestClient : HttpApiClientBase, ILogCollectorClient {
		private readonly string appName;
		private string appApiToken;
		private readonly MediaTypeWithQualityHeaderValue pemMT = new MediaTypeWithQualityHeaderValue("application/x-pem-file");
		private JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonOptions.RestOptions);

		/// <summary>
		/// Creates a client object that uses the given <see cref="HttpClient"/> and its associated <see cref="HttpClient.BaseAddress"/> to communicate with the backend at that address.
		/// </summary>
		/// <param name="httpClient">The <see cref="HttpClient"/> to use for requests to the backend.
		/// The <see cref="HttpClient.BaseAddress"/> of the client needs to be set to the base URI of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.</param>
		public LogCollectorRestClient(HttpClient httpClient, string appName, string appApiToken) :
				base(httpClient, new AuthorizationData(new AuthorizationToken(""), DateTime.MinValue), "/api/analytics/log/v2/") {
			this.appName = appName;
			this.appApiToken = appApiToken;
		}

		private void addApiTokenHeader(HttpRequestMessage request) {
			request.Headers.Add("App-API-Token", appApiToken);
		}

		/// <inheritdoc/>
		public async Task LoadRecipientCertificatesAsync(CertificateStore targetCertificateStore, CancellationToken ct = default) {
			using var response = await SendRequest(HttpMethod.Get, "recipient-certificates",
				new Dictionary<string, string> { ["appName"] = appName },
				null, addApiTokenHeader, pemMT, ct, authenticated: false);
			await targetCertificateStore.LoadCertificatesFromHttpAsync(response, ct);
		}

		/// <inheritdoc/>
		public async Task UploadLogFileAsync(LogMetadataDTO metadata, Stream content, CancellationToken ct = default) {
			using (var multipartContent = new MultipartFormDataContent()) {
				var contentObj = new StreamContent(content);
				contentObj.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
				var metadataObj = JsonContent.Create(metadata, MediaTypeHeaderValue.Parse("application/json"), jsonOptions);
				multipartContent.Add(metadataObj, "metadata");
				multipartContent.Add(contentObj, "content");
				using var response = await SendRequest(HttpMethod.Post, "", multipartContent, addApiTokenHeader, null, ct);
			}
		}

		protected override void MapExceptionForError(HttpRequestMessage request, HttpResponseMessage response) {
			if (response.StatusCode == HttpStatusCode.Unauthorized && response.Headers.WwwAuthenticate.Count > 0) {
				throw new LoginRequiredException();
			}
			try {
				base.MapExceptionForError(request, response);
			}
			catch (HttpApiResponseException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) {
				throw new UnauthorizedException(ex);
			}
			catch (HttpApiResponseException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge) {
				throw new FileTooLargeException(ex);
			}
			catch {
				throw;
			}
		}
	}
}