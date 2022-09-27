using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Analytics.DTO;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System.Runtime.CompilerServices;

namespace SGL.Analytics.ExporterClient {
	public partial class SglAnalyticsExporter : IAsyncDisposable {
		public SglAnalyticsExporter(HttpClient httpClient, Action<ISglAnalyticsExporterConfigurator> configuration) {
			this.httpClient = httpClient;
			configuration(configurator);
			var loggerFactoryBootstrapArgs = new SglAnalyticsExporterConfiguratorFactoryArguments(httpClient, NullLoggerFactory.Instance, randomGenerator, configurator.CustomArgumentFactories);
			LoggerFactory = configurator.LoggerFactory.Factory(loggerFactoryBootstrapArgs);
			logger = LoggerFactory.CreateLogger<SglAnalyticsExporter>();
		}

		/// <summary>
		/// The <see cref="ILoggerFactory"/> object that this client uses for logging.
		/// </summary>
		public ILoggerFactory LoggerFactory { get; }
		public (KeyId AuthenticationKeyId, KeyId DecryptionKeyId)? CurrentKeyIds { get; private set; } = null;
		public (Certificate AuthenticationCertificate, Certificate DecryptionCertificate)? CurrentKeyCertificates { get; private set; } = null;
		public string? CurrentAppName { get; private set; } = null;

		public async Task UseKeyFileAsync(string filePath, Func<char[]> getPassword, CancellationToken ct = default) {
			using var file = File.OpenText(filePath);
			await UseKeyFileAsync(file, filePath, getPassword, ct).ConfigureAwait(false);
		}
		public async Task UseKeyFileAsync(TextReader reader, string sourceName, Func<char[]> getPassword, CancellationToken ct = default) {
			var pemReader = new PemObjectReader(reader, getPassword);
			var result = await Task.Run(() => ReadKeyFile(pemReader, sourceName, ct), ct).ConfigureAwait(false);
			var authFactoryargs = new SglAnalyticsExporterConfiguratorFactoryArguments(httpClient, LoggerFactory, randomGenerator, configurator.CustomArgumentFactories);

			using var lockHandle = await stateLock.WaitAsyncWithScopedRelease(ct).ConfigureAwait(false); // Hold lock till end of method as we mutate state.
			authenticationKeyPair = result.AuthenticationKeyPair;
			recipientKeyPair = result.RecipientKeyPair;
			CurrentKeyIds = (result.AuthenticationKeyId, result.RecipientKeyId);
			CurrentKeyCertificates = (result.AuthenticationCertificate, result.RecipientCertificate);
			authenticator = configurator.Authenticator.Factory(authFactoryargs, authenticationKeyPair);
			await ClearPerAppStatesAsync(); // Clear cached per-app states as they may have clients authenticated using a different key pair.
		}

		public async Task SwitchToApplicationAsync(string appName, CancellationToken ct = default) {
			using (var lockHandle = await stateLock.WaitAsyncWithScopedRelease(ct).ConfigureAwait(false)) { // Hold lock till as we mutate state.
				CurrentAppName = appName;
			}
			// Create per-app state eagerly, if not cached:
			await GetPerAppStateAsync(ct).ConfigureAwait(false);
		}

		public async Task<IAsyncEnumerable<(LogFileMetadata Metadata, Stream? Content)>> GetDecryptedLogFilesAsync(Func<ILogFileQuery, ILogFileQuery> query, CancellationToken ct = default) {
			if (CurrentKeyIds == null) {
				throw new InvalidOperationException("No current key id.");
			}
			if (recipientKeyPair == null) {
				throw new InvalidOperationException("No current decryption key pair.");
			}
			var queryParams = (LogFileQuery)query(new LogFileQuery());
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			return GetDecryptedLogFilesAsyncImpl(perAppState, CurrentKeyIds.Value.DecryptionKeyId, recipientKeyPair, queryParams, ct);
		}
		public async Task GetDecryptedLogFilesAsync(ILogFileSink sink, Func<ILogFileQuery, ILogFileQuery> query, CancellationToken ct = default) {
			var logs = await GetDecryptedLogFilesAsync(query, ct).ConfigureAwait(false);
			await foreach (var (metadata, content) in logs.ConfigureAwait(false).WithCancellation(ct)) {
				try {
					logger.LogTrace("Procesing log file {id} from user {userId}.", metadata.LogFileId, metadata.UserId);
					await sink.ProcessLogFileAsync(metadata, content, ct).ConfigureAwait(false);
				}
				catch (Exception ex) {
					logger.LogError(ex, "Encountered error while procesing log file {id} from user {userId}.", metadata.LogFileId, metadata.UserId);
					throw;
				}
				finally {
					await (content?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
				}
			}
		}
		public async Task<IAsyncEnumerable<UserRegistrationData>> GetDecryptedUserRegistrationsAsync(Func<IUserRegistrationQuery, IUserRegistrationQuery> query, CancellationToken ct = default) {
			if (CurrentKeyIds == null) {
				throw new InvalidOperationException("No current key id.");
			}
			if (recipientKeyPair == null) {
				throw new InvalidOperationException("No current decryption key pair.");
			}
			var queryParams = (UserRegistrationQuery)query(new UserRegistrationQuery());
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			return GetDecryptedUserRegistrationsAsyncImpl(perAppState, CurrentKeyIds.Value.DecryptionKeyId, recipientKeyPair, queryParams, ct);
		}
		public async Task GetDecryptedUserRegistrationsAsync(IUserRegistrationSink sink, Func<IUserRegistrationQuery, IUserRegistrationQuery> query, CancellationToken ct = default) {
			var users = await GetDecryptedUserRegistrationsAsync(query, ct);
			await foreach (var user in users.ConfigureAwait(false).WithCancellation(ct)) {
				try {
					logger.LogTrace("Processing user registration {id}.", user.UserId);
					await sink.ProcessUserRegistrationAsync(user, ct).ConfigureAwait(false);
				}
				catch (Exception ex) {
					logger.LogError(ex, "Encountered error while processing user registration {id}.", user.UserId);
					throw;
				}
			}
		}

		public async Task<IEnumerable<LogFileMetadata>> GetLogFileMetadataAsync(Func<ILogFileQuery, ILogFileQuery> query, CancellationToken ct = default) {
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			var queryParams = (LogFileQuery)query(new LogFileQuery());
			var logClient = perAppState.LogExporterApiClient;
			var metaDTOs = await logClient.GetMetadataForAllLogsAsync(ct: ct).ConfigureAwait(false);
			metaDTOs = queryParams.ApplyTo(metaDTOs);
			return metaDTOs.Select(mdto => ToMetadata(mdto)).ToList();
		}
	}
}
