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
		private CACertTrustValidator? dstCertValidator;
		private readonly ILoggerFactory loggerFactory;

		public RekeyingLogic(ILoggerFactory loggerFactory) {
			this.loggerFactory = loggerFactory;
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
		public async Task LoadSignerCertificateAsync(string path, bool ignoreValidityPeriod, CancellationToken ct = default) {
			using var pemBuffer = new MemoryStream();
			using (var fileStream = File.OpenRead(path)) {
				await fileStream.CopyToAsync(pemBuffer, ct);
			}
			pemBuffer.Position = 0;
			using var pemReader = new StreamReader(pemBuffer);
			dstCertValidator = new CACertTrustValidator(pemReader, path, ignoreValidityPeriod,
				loggerFactory.CreateLogger<CACertTrustValidator>(), loggerFactory.CreateLogger<CertificateStore>());
		}

		public async Task<CertificateStore> LoadLogRecipientCertsAsync(CancellationToken ct) {
			if (dstCertValidator == null) {
				throw new InvalidOperationException("No signer certificate for validation.");
			}
			var certStore = new CertificateStore(dstCertValidator, loggerFactory.CreateLogger<CertificateStore>());
			await sglAnalytics.LoadLogFileRecipientCertificatesAsync(certStore, ct);
			return certStore;
		}
		public async Task<CertificateStore> LoadUserRegRecipientCertsAsync(CancellationToken ct) {
			if (dstCertValidator == null) {
				throw new InvalidOperationException("No signer certificate for validation.");
			}
			var certStore = new CertificateStore(dstCertValidator, loggerFactory.CreateLogger<CertificateStore>());
			await sglAnalytics.LoadUserRegistrationRecipientCertificatesAsync(certStore, ct);
			return certStore;
		}

		public Task<RekeyingOperationResult> RekeyLogFilesAsync(KeyId dstKeyId, CancellationToken ct) {
			if (dstCertValidator == null) {
				throw new InvalidOperationException("No signer certificate for validation.");
			}
			return sglAnalytics.RekeyLogFilesForRecipientKeyAsync(dstKeyId, dstCertValidator, ct);
		}
		public Task<RekeyingOperationResult> RekeyUserRegistrationsAsync(KeyId dstKeyId, CancellationToken ct) {
			if (dstCertValidator == null) {
				throw new InvalidOperationException("No signer certificate for validation.");
			}
			return sglAnalytics.RekeyUserRegistrationsForRecipientKeyAsync(dstKeyId, dstCertValidator, ct);
		}
	}
}
