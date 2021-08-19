using SGL.Analytics.DTO;
using System;
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

		// TODO: Make base URI and API endpoint URI configurable.
		public LogCollectorRestClient() : this(new Uri("http://localhost:5001/")) { }
		public LogCollectorRestClient(Uri backendServerBaseUri) : this(backendServerBaseUri, new Uri("api/AnalyticsLog", UriKind.Relative)) { }
		public LogCollectorRestClient(Uri backendServerBaseUri, Uri logCollectorApiEndpoint) {
			this.backendServerBaseUri = backendServerBaseUri;
			this.logCollectorApiEndpoint = logCollectorApiEndpoint;
			this.logCollectorApiFullUri = new Uri(backendServerBaseUri, logCollectorApiEndpoint);
		}


		public async Task UploadLogFileAsync(string appName, string appAPIToken, Guid userID, ILogStorage.ILogFile logFile) {
			using (var stream = logFile.OpenReadRaw()) {
				var content = new StreamContent(stream);
				content.Headers.MapObjectProperties(new LogMetadataDTO(appName, userID, logFile.ID, logFile.CreationTime, logFile.EndTime));
				content.Headers.Add("App-API-Token", appAPIToken);
				var response = await httpClient.PostAsync(logCollectorApiFullUri, content);
			}
		}
	}
}