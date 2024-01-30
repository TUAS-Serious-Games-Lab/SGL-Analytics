using Microsoft.Extensions.Logging;
using SGL.Analytics.ExporterClient;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.RekeyingTool {
	public class RekeyingLogic : IAsyncDisposable {
		private HttpClient httpClient;
		private SglAnalyticsExporter sglAnalytics;

		public RekeyingLogic(ILoggerFactory loggerFactory) {
			httpClient = new HttpClient();
			sglAnalytics = new SglAnalyticsExporter(httpClient, config => {
				config.UseLoggerFactory(args => loggerFactory, dispose: false);
			});
		}
		public Uri? BackendBaseUri {
			get => httpClient.BaseAddress;
			set => httpClient.BaseAddress = value;
		}

		public KeyId? AuthenticationKeyId => sglAnalytics.CurrentKeyIds?.AuthenticationKeyId;
		public Certificate? AuthenticationCertificate => sglAnalytics.CurrentKeyCertificates?.AuthenticationCertificate;
		public KeyId? DecryptionKeyId => sglAnalytics.CurrentKeyIds?.DecryptionKeyId;
		public Certificate? DecryptionCertificate => sglAnalytics.CurrentKeyCertificates?.DecryptionCertificate;

		public async Task SetAppNameAsync(string appName, CancellationToken ct) {
			await sglAnalytics.SwitchToApplicationAsync(appName, ct);
		}

		public async Task LoadKeyFile(string fileName, Func<char[]> getPassword, CancellationToken ct) {
			await sglAnalytics.UseKeyFileAsync(fileName, getPassword, ct);
		}

		public async ValueTask DisposeAsync() {
			await sglAnalytics.DisposeAsync();
			httpClient.Dispose();
		}
	}
}
