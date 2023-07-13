using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Analytics.DTO;
using SGL.Analytics.ExporterClient.Values;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

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
			var keyFile = await KeyFile.LoadAsync(reader, sourceName, getPassword, logger, ct).ConfigureAwait(false);
			await UseKeyFileAsync(keyFile, ct).ConfigureAwait(false);
		}
		public async Task UseKeyFileAsync(KeyFile keyFile, CancellationToken ct = default) {
			var authFactoryargs = new SglAnalyticsExporterConfiguratorFactoryArguments(httpClient, LoggerFactory, randomGenerator, configurator.CustomArgumentFactories);

			using var lockHandle = await stateLock.WaitAsyncWithScopedRelease(ct).ConfigureAwait(false); // Hold lock till end of method as we mutate state.
			authenticationKeyPair = keyFile.AuthenticationKeyPair;
			recipientKeyPair = keyFile.RecipientKeyPair;
			CurrentKeyIds = (keyFile.AuthenticationKeyId, keyFile.RecipientKeyId);
			CurrentKeyCertificates = (keyFile.AuthenticationCertificate, keyFile.RecipientCertificate);
			authenticator = configurator.Authenticator.Factory(authFactoryargs, authenticationKeyPair);
			await ClearPerAppStatesAsync(); // Clear cached per-app states as they may have clients authenticated using a different key pair.
			if (CurrentKeyCertificates.Value.AuthenticationCertificate.SubjectDN.Equals(CurrentKeyCertificates.Value.DecryptionCertificate.SubjectDN)) {
				logger.LogDebug("Using auth key {authKeyId} and decryption key {recipientKeyId} with common distinguished name {dn}.",
					CurrentKeyIds?.AuthenticationKeyId, CurrentKeyIds?.DecryptionKeyId, CurrentKeyCertificates?.AuthenticationCertificate.SubjectDN);
			}
			else {
				logger.LogDebug("Using auth key {authKeyId} with distinguished name {authDN} and decryption key {recipientKeyId} with distinguished name {recipientDN}.",
					CurrentKeyIds?.AuthenticationKeyId, CurrentKeyCertificates?.AuthenticationCertificate.SubjectDN, CurrentKeyIds?.DecryptionKeyId, CurrentKeyCertificates?.DecryptionCertificate.SubjectDN);
			}
		}

		public async Task SwitchToApplicationAsync(string appName, CancellationToken ct = default) {
			using (var lockHandle = await stateLock.WaitAsyncWithScopedRelease(ct).ConfigureAwait(false)) { // Hold lock till as we mutate state.
				CurrentAppName = appName;
			}
			// Create per-app state eagerly, if not cached:
			await GetPerAppStateAsync(ct).ConfigureAwait(false);
			logger.LogDebug("Working with application {appName}.", appName);
		}

		public async Task<(LogFileMetadata Metadata, Stream? Content)> GetDecryptedLogFileByIdAsync(Guid logFileId, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			var keyId = CurrentKeyIds!.Value.DecryptionKeyId;
			logger.LogDebug("Getting metadata and content for log file {id}...", logFileId);
			var metadataTask = perAppState.LogExporterApiClient.GetLogMetadataByIdAsync(logFileId, keyId, ct);
			var contentTask = perAppState.LogExporterApiClient.GetLogContentByIdAsync(logFileId, ct);
			var metaDto = await metadataTask;
			var encryptedContent = await contentTask;
			var keyDecryptor = new KeyDecryptor(recipientKeyPair!);
			var content = DecryptLogFile(encryptedContent, keyId, metaDto, keyDecryptor, ct);
			var metadata = ToMetadata(metaDto);
			return (metadata, content);
		}
		public async Task<IAsyncEnumerable<(LogFileMetadata Metadata, Stream? Content)>> GetDecryptedLogFilesAsync(Func<ILogFileQuery, ILogFileQuery> query, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var queryParams = (LogFileQuery)query(new LogFileQuery());
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			return GetDecryptedLogFilesAsyncImpl(perAppState, CurrentKeyIds!.Value.DecryptionKeyId, recipientKeyPair!, queryParams, ct);
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
		public async Task<UserRegistrationData> GetDecryptedUserRegistrationByIdAsync(Guid userId, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			var keyId = CurrentKeyIds!.Value.DecryptionKeyId;
			var metaDto = await perAppState.UserExporterApiClient.GetUserMetadataByIdAsync(userId, keyId, ct);
			var keyDecryptor = new KeyDecryptor(recipientKeyPair!);
			var decryptedProps = await DecryptUserProperties(keyId, keyDecryptor, metaDto, ct).ConfigureAwait(false);
			return new UserRegistrationData(metaDto.UserId, metaDto.Username, metaDto.StudySpecificProperties, decryptedProps);
		}
		public async Task<IAsyncEnumerable<UserRegistrationData>> GetDecryptedUserRegistrationsAsync(Func<IUserRegistrationQuery, IUserRegistrationQuery> query, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var queryParams = (UserRegistrationQuery)query(new UserRegistrationQuery());
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			return GetDecryptedUserRegistrationsAsyncImpl(perAppState, CurrentKeyIds!.Value.DecryptionKeyId, recipientKeyPair!, queryParams, ct);
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

		public async IAsyncEnumerable<LogFileEntry> ParseLogEntriesAsync(Stream fileContent, [EnumeratorCancellation] CancellationToken ct = default) {
			await foreach (var entry in JsonSerializer.DeserializeAsyncEnumerable<LogEntry>(fileContent, JsonOptions.LogEntryOptions, ct).ConfigureAwait(false).WithCancellation(ct)) {
				if (entry != null) {
					if (entry.Metadata.EntryType is LogEntry.LogEntryType.Event) {
						yield return new EventLogFileEntry(entry.Metadata.Channel, entry.Metadata.TimeStamp, (Dictionary<string, object?>)entry.Payload, entry.Metadata.EventType ?? "");
					}
					else if (entry.Metadata.EntryType is LogEntry.LogEntryType.Snapshot) {
						yield return new SnapshotLogFileEntry(entry.Metadata.Channel, entry.Metadata.TimeStamp, (Dictionary<string, object?>)entry.Payload, entry.Metadata.ObjectID);
					}
					else {
						logger.LogWarning("Read LogEntry with unknown entry type while parsing log file.");
						yield return new LogFileEntry(entry.Metadata.Channel, entry.Metadata.TimeStamp, (Dictionary<string, object?>)entry.Payload);
					}
				}
				else {
					logger.LogWarning("Read null value instead of LogEntry while parsing log file.");
				}
			}
		}

		public async Task RekeyLogFilesForRecipientKey(KeyId keyIdToGrantAccessTo, ICertificateValidator keyCertValidator, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			var logClient = perAppState.LogExporterApiClient;
			var certStore = new CertificateStore(keyCertValidator, LoggerFactory.CreateLogger<CertificateStore>());
			await logClient.GetRecipientCertificates(perAppState.AppName, certStore, ct);
			var cert = certStore.GetCertificateByKeyId(keyIdToGrantAccessTo);
			if (cert == null) {
				logger.LogError("Target key {targetKey} for rekeying operation not found in fetched certificate store.", keyIdToGrantAccessTo);
				throw new Exception("KeyId not found!");
			}
			var origKeyDict = await logClient.GetKeysForRekeying(CurrentKeyIds!.Value.DecryptionKeyId, ct);
			var keyEncryptor = new KeyEncryptor(new[] { cert.PublicKey }, randomGenerator,
				allowSharedMessageKeyPair: false); // As we don't have the original private key for the shared message public key,
												   // we need to create a separate message public key for the new recipient,
												   // even if they are using the same curve parameters.
			var keyDecryptor = new KeyDecryptor(recipientKeyPair!);
			var perFileTasks = Task.WhenAll(origKeyDict.Select(logFileInfo => Task.Run<(Guid LogId, DataKeyInfo? DataKeyInfo)>(() => {
				var decryptedKey = keyDecryptor.DecryptKey(logFileInfo.Value);
				if (decryptedKey == null) {
					logger.LogWarning("Couldn't decrypt data key for log file {fileId} for rekeying operation.", logFileInfo.Key);
					return (LogId: logFileInfo.Key, DataKeyInfo: null);
				}
				var (recipientKeys, sharedMsgPubKey) = keyEncryptor.EncryptDataKey(decryptedKey);
				Debug.Assert(sharedMsgPubKey == null);
				return (LogId: logFileInfo.Key, DataKeyInfo: recipientKeys[keyIdToGrantAccessTo]);
			}, ct)));
			var perFileResults = await perFileTasks;
			var resultMap = perFileResults.Where(res => res.DataKeyInfo != null).ToDictionary(res => res.LogId, res => res.DataKeyInfo!);
			await logClient.PutRekeyedKeys(keyIdToGrantAccessTo, resultMap, ct);
		}
		public async Task RekeyUserRegistrationsForRecipientKey(KeyId keyIdToGrantAccessTo, ICertificateValidator keyCertValidator, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			var usersClient = perAppState.UserExporterApiClient;
			var certStore = new CertificateStore(keyCertValidator, LoggerFactory.CreateLogger<CertificateStore>());
			await usersClient.GetRecipientCertificates(perAppState.AppName, certStore, ct);
			var cert = certStore.GetCertificateByKeyId(keyIdToGrantAccessTo);
			if (cert == null) {
				logger.LogError("Target key {targetKey} for rekeying operation not found in fetched certificate store.", keyIdToGrantAccessTo);
				throw new Exception("KeyId not found!");
			}
			var origKeyDict = await usersClient.GetKeysForRekeying(CurrentKeyIds!.Value.DecryptionKeyId, ct);
			var keyEncryptor = new KeyEncryptor(new[] { cert.PublicKey }, randomGenerator,
				allowSharedMessageKeyPair: false); // As we don't have the original private key for the shared message public key,
												   // we need to create a separate message public key for the new recipient,
												   // even if they are using the same curve parameters.
			var keyDecryptor = new KeyDecryptor(recipientKeyPair!);
			var perFileTasks = Task.WhenAll(origKeyDict.Select(userInfo => Task.Run<(Guid UserId, DataKeyInfo? DataKeyInfo)>(() => {
				var decryptedKey = keyDecryptor.DecryptKey(userInfo.Value);
				if (decryptedKey == null) {
					logger.LogWarning("Couldn't decrypt data key for user registration {userId} for rekeying operation.", userInfo.Key);
					return (UserId: userInfo.Key, DataKeyInfo: null);
				}
				var (recipientKeys, sharedMsgPubKey) = keyEncryptor.EncryptDataKey(decryptedKey);
				Debug.Assert(sharedMsgPubKey == null);
				return (UserId: userInfo.Key, DataKeyInfo: recipientKeys[keyIdToGrantAccessTo]);
			}, ct)));
			var perFileResults = await perFileTasks;
			var resultMap = perFileResults.Where(res => res.DataKeyInfo != null).ToDictionary(res => res.UserId, res => res.DataKeyInfo!);
			await usersClient.PutRekeyedKeys(keyIdToGrantAccessTo, resultMap, ct);
		}
	}
}
