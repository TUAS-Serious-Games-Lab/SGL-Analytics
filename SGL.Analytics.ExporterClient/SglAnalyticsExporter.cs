using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System.Runtime.CompilerServices;

namespace SGL.Analytics.ExporterClient {
	public partial class SglAnalyticsExporter {
		public SglAnalyticsExporter(HttpClient httpClient, Action<ISglAnalyticsExporterConfigurator> configuration) {
			this.httpClient = httpClient;
			configuration(configurator);
			mainSyncContext = configurator.SynchronizationContextGetter();
			var loggerFactoryBootstrapArgs = new SglAnalyticsExporterConfiguratorFactoryArguments(httpClient, NullLoggerFactory.Instance, randomGenerator, configurator.CustomArgumentFactories);
			LoggerFactory = loggerFactoryBootstrapArgs.LoggerFactory;
		}

		/// <summary>
		/// The <see cref="ILoggerFactory"/> object that this client uses for logging.
		/// </summary>
		public ILoggerFactory LoggerFactory { get; }
		public (KeyId AuthenticationKey, KeyId DecryptionKey, Certificate AuthenticationCertificate, Certificate DecryptionCertificate)? CurrentKeys { get; private set; } = null;
		public string? CurrentAppName { get; private set; } = null;

		public async Task UseKeyFileAsync(TextReader reader, string sourceName, CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		public async Task SwitchToApplicationAsync(string appName, CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		public async IAsyncEnumerable<(LogFileMetadata Metadata, Stream Content)> GetDecryptedLogFilesAsync(Func<DownstreamLogMetadataDTO, bool> filter, [EnumeratorCancellation] CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		public async IAsyncEnumerable<UserRegistrationData> GetDecryptedUserRegistrationsAsync(Func<UserMetadataDTO, bool> filter, [EnumeratorCancellation] CancellationToken ct = default) {
			throw new NotImplementedException();
		}
	}
}
