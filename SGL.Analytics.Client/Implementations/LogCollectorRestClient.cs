using SGL.Analytics.DTO;
using SGL.Analytics.Utilities;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
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
		public LogCollectorRestClient() : this(SGLAnalytics.DefaultBackendBaseUri) { }
		public LogCollectorRestClient(Uri backendServerBaseUri) : this(backendServerBaseUri, new Uri("api/AnalyticsLog", UriKind.Relative)) { }
		public LogCollectorRestClient(Uri backendServerBaseUri, Uri logCollectorApiEndpoint) {
			this.backendServerBaseUri = backendServerBaseUri;
			this.logCollectorApiEndpoint = logCollectorApiEndpoint;
			this.logCollectorApiFullUri = new Uri(backendServerBaseUri, logCollectorApiEndpoint);
		}

		public async Task UploadLogFileAsync(string appName, string appAPIToken, AuthorizationToken authToken, ILogStorage.ILogFile logFile) {
			try {
				using (var stream = logFile.OpenReadRaw()) {
					var content = new StreamContent(stream);
					content.Headers.MapDtoProperties(new LogMetadataDTO(logFile.ID, logFile.CreationTime, logFile.EndTime));
					content.Headers.Add("App-API-Token", appAPIToken);
					content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
					var request = new HttpRequestMessage(HttpMethod.Post, logCollectorApiFullUri);
					request.Content = content;
					request.Headers.Authorization = authToken.ToHttpHeaderValue();
					var response = await httpClient.SendAsync(request);
					if (response.StatusCode == HttpStatusCode.Unauthorized && response.Headers.WwwAuthenticate.Count > 0) {
						throw new LoginRequiredException();
					}
					response.EnsureSuccessStatusCode();
				}
			}
			catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) {
				throw new UnauthorizedException(ex);
			}
			catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge) {
				throw new FileTooLargeException(ex);
			}
			catch {
				throw;
			}
		}
	}
}