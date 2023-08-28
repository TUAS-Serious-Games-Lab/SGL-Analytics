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
	/// <summary>
	/// Acts a the central facade class for the data exporting side of SGL Analytics.
	/// It allows users on the receiving side to download and decrypt the data collected by SGL Analytics, log files as well as user registrations.
	/// The client object works on a currently selected application (<see cref="CurrentAppName"/>),
	/// but can easily switch using <see cref="SwitchToApplicationAsync(string, CancellationToken)"/>
	/// if the user has the needed credentials.
	/// </summary>
	public partial class SglAnalyticsExporter : IAsyncDisposable {
		/// <summary>
		/// Instantiates a new exporter client object.
		/// </summary>
		/// <param name="httpClient">The HTTP client to use for communication with the backend.</param>
		/// <param name="configuration">A builder-pattern-style configurator, that configures the behavior and dependencies of the client.</param>
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
		/// <summary>
		/// The key ids for te key pairs of the current user as a tuple, or null if no user is authenticated.
		/// </summary>
		public (KeyId AuthenticationKeyId, KeyId DecryptionKeyId)? CurrentKeyIds { get; private set; } = null;
		/// <summary>
		/// The certificates for te key pairs of the current user as a tuple, or null if no user is authenticated.
		/// </summary>
		public (Certificate AuthenticationCertificate, Certificate DecryptionCertificate)? CurrentKeyCertificates { get; private set; } = null;
		/// <summary>
		/// The application in which the client currently operates.
		/// User <see cref="SwitchToApplicationAsync(string, CancellationToken)"/> to change this.
		/// </summary>
		public string? CurrentAppName { get; private set; } = null;

		/// <summary>
		/// Let the client use the given key file contents from the file identified by <paramref name="filePath"/> for authentication and decryption.
		/// Reads the key file content and then uses the loaded keys like <see cref="UseKeyFileAsync(KeyFile, CancellationToken)"/>.
		/// </summary>
		/// <param name="filePath">The path and filename of the file to load.</param>
		/// <param name="getPassword">Obtains the password / passphrase for the encryped private keys in the key file.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation.</returns>
		public async Task UseKeyFileAsync(string filePath, Func<char[]> getPassword, CancellationToken ct = default) {
			using var file = File.OpenText(filePath);
			await UseKeyFileAsync(file, filePath, getPassword, ct).ConfigureAwait(false);
		}
		/// <summary>
		/// Let the client use the given key file contents from <paramref name="reader"/> for authentication and decryption.
		/// Reads the key file content from <paramref name="reader"/> and then uses the loaded keys like <see cref="UseKeyFileAsync(KeyFile, CancellationToken)"/>.
		/// </summary>
		/// <param name="reader">A reader containing the key file PEM text.</param>
		/// <param name="sourceName">The name of <paramref name="reader"/>, e.g. the underlying file name, for error reporting purposes.</param>
		/// <param name="getPassword">Obtains the password / passphrase for the encryped private keys in the key file.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation.</returns>
		public async Task UseKeyFileAsync(TextReader reader, string sourceName, Func<char[]> getPassword, CancellationToken ct = default) {
			var keyFile = await KeyFile.LoadAsync(reader, sourceName, getPassword, logger, ct).ConfigureAwait(false);
			await UseKeyFileAsync(keyFile, ct).ConfigureAwait(false);
		}
		/// <summary>
		/// Let the client use the given key file contents from <paramref name="keyFile"/> for authentication and decryption.
		/// A key file contains two key-pairs, one for authentication with the backend, and one for decrypting end-to-end encrypted data.
		/// It also contains a certificate for each of these to identify which pair is for which purpose.
		/// </summary>
		/// <param name="keyFile">An object representing the contents of the key file.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation.</returns>
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
		/// <summary>
		/// Switches the client to work with the data storage of the specified application, identified by <paramref name="appName"/>.
		/// </summary>
		/// <param name="appName">The unique name of the application to work with.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation.</returns>
		public async Task SwitchToApplicationAsync(string appName, CancellationToken ct = default) {
			using (var lockHandle = await stateLock.WaitAsyncWithScopedRelease(ct).ConfigureAwait(false)) { // Hold lock till as we mutate state.
				CurrentAppName = appName;
			}
			// Create per-app state eagerly, if not cached:
			await GetPerAppStateAsync(ct).ConfigureAwait(false);
			logger.LogDebug("Working with application {appName}.", appName);
		}
		/// <summary>
		/// Asynchronously downloads and decrypts the log file with the given id from the current application.
		/// Both, the metadata and the content of the log file are returned.
		/// </summary>
		/// <param name="logFileId">The id of the log to retrieve and decrypt.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>
		/// A Task representing the asynchronous operation, provinding the metadata and the decrypted contents.
		/// If the file could not be decrypted, the metadata is still provided, but the content is <see langword="null"/>.
		/// </returns>
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
		/// <summary>
		/// Asynchronously downloads and decrypts the log files from the current application, that fit the query criteria given in <paramref name="query"/>.
		/// The log file data are provided as an <see cref="IAsyncEnumerable{T}"/> over tuples containing corresponding metadata and contents.
		/// If a file couldn't be downloaded or decrypted, the content field is null.
		/// </summary>
		/// <param name="query">A builder function object to construct the query criteria for the log files.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation, providing tuples of metadata and associated contents.</returns>
		public async Task<IAsyncEnumerable<(LogFileMetadata Metadata, Stream? Content)>> GetDecryptedLogFilesAsync(Func<ILogFileQuery, ILogFileQuery> query, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var queryParams = (LogFileQuery)query(new LogFileQuery());
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			return GetDecryptedLogFilesAsyncImpl(perAppState, CurrentKeyIds!.Value.DecryptionKeyId, recipientKeyPair!, queryParams, ct);
		}
		/// <summary>
		/// Asynchronously downloads and decrypts the log files from the current application, that fit the query criteria given in <paramref name="query"/>.
		/// The successfully retrieved log file data are passed to <see cref="ILogFileSink.ProcessLogFileAsync(LogFileMetadata, Stream?, CancellationToken)"/>
		/// on <paramref name="sink"/> for further processing. If a file couldn't be downloaded or decrypted, the content argument is null.
		/// </summary>
		/// <param name="sink">A sink object to which the data shall be passed for further processing.</param>
		/// <param name="query">A builder function object to construct the query criteria for the log files.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation.</returns>
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
		/// <summary>
		/// Asynchronously retrieves the user registration data for the user with the given id from the current application and decrypt the encrypted user properties.
		/// </summary>
		/// <param name="userId">The id of the user for which to retrieve.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation, providing the user data upon success.</returns>
		public async Task<UserRegistrationData> GetDecryptedUserRegistrationByIdAsync(Guid userId, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			var keyId = CurrentKeyIds!.Value.DecryptionKeyId;
			var metaDto = await perAppState.UserExporterApiClient.GetUserMetadataByIdAsync(userId, keyId, ct);
			var keyDecryptor = new KeyDecryptor(recipientKeyPair!);
			var decryptedProps = await DecryptUserProperties(keyId, keyDecryptor, metaDto, ct).ConfigureAwait(false);
			return new UserRegistrationData(metaDto.UserId, metaDto.Username, metaDto.StudySpecificProperties, decryptedProps);
		}
		/// <summary>
		/// Asynchronously retrieve the user registration data from the current application, that fit the query criteria given in <paramref name="query"/> and decrypt the encrypted user properties.
		/// </summary>
		/// <param name="query">A builder function object to construct the query criteria for the user registrations.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation, providing an <see cref="IAsyncEnumerable{UserRegistrationData}"/> with the user data upon success.</returns>
		public async Task<IAsyncEnumerable<UserRegistrationData>> GetDecryptedUserRegistrationsAsync(Func<IUserRegistrationQuery, IUserRegistrationQuery> query, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var queryParams = (UserRegistrationQuery)query(new UserRegistrationQuery());
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			return GetDecryptedUserRegistrationsAsyncImpl(perAppState, CurrentKeyIds!.Value.DecryptionKeyId, recipientKeyPair!, queryParams, ct);
		}
		/// <summary>
		/// Asynchronously retrieve the user registration data from the current application, that fit the query criteria given in <paramref name="query"/> and decrypt the encrypted user properties.
		/// The successfully retrieved data are passed to <see cref="IUserRegistrationSink.ProcessUserRegistrationAsync(UserRegistrationData, CancellationToken)"/>
		/// on <paramref name="sink"/> for further processing.
		/// </summary>
		/// <param name="sink">A sink object to which the data shall be passed for further processing.</param>
		/// <param name="query">A builder function object to construct the query criteria for the user registrations.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation.</returns>
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

		/// <summary>
		/// Asynchronously retrieves the metadata of the log files from the current application, that fit the query criteria given in <paramref name="query"/>.
		/// </summary>
		/// <param name="query">A builder function object to construct the query criteria for the log files.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation, providing an enumerable over the metadata.</returns>
		public async Task<IEnumerable<LogFileMetadata>> GetLogFileMetadataAsync(Func<ILogFileQuery, ILogFileQuery> query, CancellationToken ct = default) {
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			var queryParams = (LogFileQuery)query(new LogFileQuery());
			var logClient = perAppState.LogExporterApiClient;
			var metaDTOs = await logClient.GetMetadataForAllLogsAsync(ct: ct).ConfigureAwait(false);
			metaDTOs = queryParams.ApplyTo(metaDTOs);
			return metaDTOs.Select(mdto => ToMetadata(mdto)).ToList();
		}

		/// <summary>
		/// Asynchronously parses the given analytics log file contents and provides them as a stream of <see cref="LogFileEntry"/> objects.
		/// </summary>
		/// <param name="fileContent">The (decrypted) file content to parse.</param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>An asynchronous enumerable providing the parsed entries.</returns>
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

		/// <summary>
		/// Asynchronously rekeys the log files in the current application to grant access to the user identified by <paramref name="keyIdToGrantAccessTo"/>.
		/// The actual public key of the grantee is obtained from the backend that provides certificates which are validated using <paramref name="keyCertValidator"/>.
		/// </summary>
		/// <param name="keyIdToGrantAccessTo">The public key id of the key pair to which acceess shall be granted.</param>
		/// <param name="keyCertValidator"></param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation, providing an object that represents the result of the operation.</returns>
		/// <remarks>
		/// This iteratively downloads encrypted data keys, decrypts them using the current user's private key,
		/// reencrypts them using the public key indicated by <paramref name="keyIdToGrantAccessTo"/> and
		/// uploads those new encrypted data keys to the backend.
		/// After this, the other user can use those added keys for access.
		/// </remarks>
		public async Task<RekeyingOperationResult> RekeyLogFilesForRecipientKey(KeyId keyIdToGrantAccessTo, ICertificateValidator keyCertValidator, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			var logClient = perAppState.LogExporterApiClient;
			var certStore = new CertificateStore(keyCertValidator, LoggerFactory.CreateLogger<CertificateStore>());
			await logClient.GetRecipientCertificates(perAppState.AppName, certStore, ct);
			var cert = certStore.GetCertificateByKeyId(keyIdToGrantAccessTo);
			if (cert == null) {
				logger.LogError("Target key {targetKey} for rekeying operation not found in fetched certificate store.", keyIdToGrantAccessTo);
				throw new ArgumentException("KeyId not found!", nameof(keyIdToGrantAccessTo));
			}
			IReadOnlyDictionary<Guid, EncryptionInfo> origKeyDict;
			int paginationOffset = 0;
			var result = new RekeyingOperationResult();
			while ((origKeyDict = await logClient.GetKeysForRekeying(CurrentKeyIds!.Value.DecryptionKeyId, keyIdToGrantAccessTo, paginationOffset, ct)).Count > 0) {
				result.TotalToRekey += origKeyDict.Count;
				var keyEncryptor = new KeyEncryptor(new[] { cert.PublicKey }, randomGenerator,
					allowSharedMessageKeyPair: false); // As we don't have the original private key for the shared message public key,
													   // we need to create a separate message public key for the new recipient,
													   // even if they are using the same curve parameters.
				var keyDecryptor = new KeyDecryptor(recipientKeyPair!);
				var perFileTasks = Task.WhenAll(origKeyDict.Where(logFileInfo => logFileInfo.Value.DataMode != DataEncryptionMode.Unencrypted)
					.Select(logFileInfo => Task.Run<(Guid LogId, DataKeyInfo? DataKeyInfo)>(() => {
						var decryptedKey = keyDecryptor.DecryptKey(logFileInfo.Value);
						if (decryptedKey == null) {
							logger.LogWarning("Couldn't decrypt data key for log file {fileId} for rekeying operation.", logFileInfo.Key);
							logger.LogTrace("File uses encryption mode {mode} and the server returned the following keys:\n{keys}", logFileInfo.Value.DataMode,
								string.Join("; ", logFileInfo.Value.DataKeys.Select(dk => $"{dk.Key} => {dk.Value}")));
							return (LogId: logFileInfo.Key, DataKeyInfo: null);
						}
						var (recipientKeys, sharedMsgPubKey) = keyEncryptor.EncryptDataKey(decryptedKey);
						Debug.Assert(sharedMsgPubKey == null);
						logger.LogTrace("Rekeyed data key for log file {fileId}.", logFileInfo.Key);
						return (LogId: logFileInfo.Key, DataKeyInfo: recipientKeys[keyIdToGrantAccessTo]);
					}, ct)));
				var perFileResults = await perFileTasks;
				var skippedUnencrypted = origKeyDict.Count(logFileInfo => logFileInfo.Value.DataMode == DataEncryptionMode.Unencrypted);
				if (skippedUnencrypted > 0) {
					paginationOffset += skippedUnencrypted;
					result.Unencrypted += skippedUnencrypted;
					logger.LogDebug("Skipped {skippedUnencrypted} log files because they are unencrypted.", skippedUnencrypted);
				}
				var skippedError = perFileResults.Count(res => res.DataKeyInfo == null);
				if (skippedError > 0) {
					paginationOffset += skippedError;
					result.SkippedDueToError += skippedError;
					logger.LogWarning("Skipped {skippedError} log files due to errors.", skippedError);
				}
				var resultMap = perFileResults.Where(res => res.DataKeyInfo != null).ToDictionary(res => res.LogId, res => res.DataKeyInfo!);
				await logClient.PutRekeyedKeys(keyIdToGrantAccessTo, resultMap, ct);
				result.Successful += resultMap.Count;
			}
			return result;
		}
		/// <summary>
		/// Asynchronously rekeys the encrypted properties on the user registrations in the current application to grant access to the user identified by <paramref name="keyIdToGrantAccessTo"/>.
		/// The actual public key of the grantee is obtained from the backend that provides certificates which are validated using <paramref name="keyCertValidator"/>.
		/// </summary>
		/// <param name="keyIdToGrantAccessTo">The public key id of the key pair to which acceess shall be granted.</param>
		/// <param name="keyCertValidator"></param>
		/// <param name="ct">A cancellation token to allow cancelling the asynchronous operation.</param>
		/// <returns>A Task representing the asynchronous operation, providing an object that represents the result of the operation.</returns>
		/// <remarks>
		/// This iteratively downloads encrypted data keys, decrypts them using the current user's private key,
		/// reencrypts them using the public key indicated by <paramref name="keyIdToGrantAccessTo"/> and
		/// uploads those new encrypted data keys to the backend.
		/// After this, the other user can use those added keys for access.
		/// </remarks>
		public async Task<RekeyingOperationResult> RekeyUserRegistrationsForRecipientKey(KeyId keyIdToGrantAccessTo, ICertificateValidator keyCertValidator, CancellationToken ct = default) {
			CheckReadyForDecryption();
			var perAppState = await GetPerAppStateAsync(ct).ConfigureAwait(false);
			var usersClient = perAppState.UserExporterApiClient;
			var certStore = new CertificateStore(keyCertValidator, LoggerFactory.CreateLogger<CertificateStore>());
			await usersClient.GetRecipientCertificates(perAppState.AppName, certStore, ct);
			var cert = certStore.GetCertificateByKeyId(keyIdToGrantAccessTo);
			if (cert == null) {
				logger.LogError("Target key {targetKey} for rekeying operation not found in fetched certificate store.", keyIdToGrantAccessTo);
				throw new ArgumentException("KeyId not found!", nameof(keyIdToGrantAccessTo));
			}
			IReadOnlyDictionary<Guid, EncryptionInfo> origKeyDict;
			int paginationOffset = 0;
			var result = new RekeyingOperationResult();
			while ((origKeyDict = await usersClient.GetKeysForRekeying(CurrentKeyIds!.Value.DecryptionKeyId, keyIdToGrantAccessTo, paginationOffset, ct)).Count > 0) {
				result.TotalToRekey += origKeyDict.Count;
				var keyEncryptor = new KeyEncryptor(new[] { cert.PublicKey }, randomGenerator,
					allowSharedMessageKeyPair: false); // As we don't have the original private key for the shared message public key,
													   // we need to create a separate message public key for the new recipient,
													   // even if they are using the same curve parameters.
				var keyDecryptor = new KeyDecryptor(recipientKeyPair!);
				var perUserTasks = Task.WhenAll(origKeyDict.Where(userInfo => userInfo.Value.DataMode != DataEncryptionMode.Unencrypted)
					.Select(userInfo => Task.Run<(Guid UserId, DataKeyInfo? DataKeyInfo)>(() => {
						var decryptedKey = keyDecryptor.DecryptKey(userInfo.Value);
						if (decryptedKey == null) {
							logger.LogWarning("Couldn't decrypt data key for user registration {userId} for rekeying operation.", userInfo.Key);
							return (UserId: userInfo.Key, DataKeyInfo: null);
						}
						var (recipientKeys, sharedMsgPubKey) = keyEncryptor.EncryptDataKey(decryptedKey);
						Debug.Assert(sharedMsgPubKey == null);
						logger.LogTrace("Rekeyed data key for user registration {userId}.", userInfo.Key);
						return (UserId: userInfo.Key, DataKeyInfo: recipientKeys[keyIdToGrantAccessTo]);
					}, ct)));
				var perUserResults = await perUserTasks;
				var skippedUnencrypted = origKeyDict.Count(userInfo => userInfo.Value.DataMode == DataEncryptionMode.Unencrypted);
				if (skippedUnencrypted > 0) {
					paginationOffset += skippedUnencrypted;
					result.Unencrypted += skippedUnencrypted;
					logger.LogDebug("Skipped {skippedUnencrypted} user registrations because they are unencrypted.", skippedUnencrypted);
				}
				var skippedError = perUserResults.Count(res => res.DataKeyInfo == null);
				if (skippedError > 0) {
					paginationOffset += skippedError;
					result.SkippedDueToError += skippedError;
					logger.LogWarning("Skipped {skippedError} user registrations due to errors.", skippedError);
				}
				var resultMap = perUserResults.Where(res => res.DataKeyInfo != null).ToDictionary(res => res.UserId, res => res.DataKeyInfo!);
				await usersClient.PutRekeyedKeys(keyIdToGrantAccessTo, resultMap, ct);
				result.Successful += resultMap.Count;
			}
			return result;
		}
	}
}
