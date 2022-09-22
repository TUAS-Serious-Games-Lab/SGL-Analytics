using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto;
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
		public (KeyId AuthenticationKeyId, KeyId DecryptionKeyId)? CurrentKeyIds { get; private set; } = null;
		public (Certificate AuthenticationCertificate, Certificate DecryptionCertificate)? CurrentKeyCertificates { get; private set; } = null;
		public string? CurrentAppName { get; private set; } = null;

		public async Task UseKeyFileAsync(TextReader reader, string sourceName, Func<char[]> getPassword, CancellationToken ct = default) {
			var pemReader = new PemObjectReader(reader, getPassword);
			var result = await Task.Run(() => ReadKeyFile(pemReader, sourceName, ct), ct);
			authenticationKeyPair = result.AuthenticationKeyPair;
			recipientKeyPair = result.RecipientKeyPair;
			CurrentKeyIds = (result.AuthenticationKeyId, result.RecipientKeyId);
			CurrentKeyCertificates = (result.AuthenticationCertificate, result.RecipientCertificate);
			var args = new SglAnalyticsExporterConfiguratorFactoryArguments(httpClient, LoggerFactory, randomGenerator, configurator.CustomArgumentFactories);
			authenticator = configurator.Authenticator.Factory(args, authenticationKeyPair);
		}

		public async Task SwitchToApplicationAsync(string appName, CancellationToken ct = default) {
			CurrentAppName = appName;
			// Create per-app state eagerly, if not cached:
			await GetPerAppStateAsync(ct);
		}

		public IAsyncEnumerable<(LogFileMetadata Metadata, Stream Content)> GetDecryptedLogFilesAsync(Func<DownstreamLogMetadataDTO, bool> filter, [EnumeratorCancellation] CancellationToken ct = default) {
			throw new NotImplementedException();
		}

		public IAsyncEnumerable<UserRegistrationData> GetDecryptedUserRegistrationsAsync(Func<UserMetadataDTO, bool> filter, [EnumeratorCancellation] CancellationToken ct = default) {
			throw new NotImplementedException();
		}
	}
}
