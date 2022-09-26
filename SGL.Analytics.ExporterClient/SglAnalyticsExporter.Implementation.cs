using Microsoft.Extensions.Logging;
using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public partial class SglAnalyticsExporter {
		private SynchronizationContext mainSyncContext;
		private HttpClient httpClient;
		private SglAnalyticsExporterConfigurator configurator = new SglAnalyticsExporterConfigurator();
		private RandomGenerator randomGenerator = new RandomGenerator();
		private KeyPair? authenticationKeyPair;
		private KeyPair? recipientKeyPair;
		private IExporterAuthenticator? authenticator = null;
		private Dictionary<string, PerAppState> perAppStates = new Dictionary<string, PerAppState>();
		private ILogger<SglAnalyticsExporter> logger;

		public async ValueTask DisposeAsync() {
			foreach (var appState in perAppStates.Values) {
				if (configurator.UserApiClient.Dispose) await disposeIfDisposable(appState.UserExporterApiClient);
				if (configurator.LogApiClient.Dispose) await disposeIfDisposable(appState.LogExporterApiClient);
			}
			if (configurator.Authenticator.Dispose && authenticator != null) await disposeIfDisposable(authenticator);
			configurator.CustomArgumentFactories.Dispose();
			if (configurator.LoggerFactory.Dispose) await disposeIfDisposable(LoggerFactory);
		}
		private async Task disposeIfDisposable(object obj) {
			if (obj is IAsyncDisposable ad) {
				await ad.DisposeAsync().AsTask();
			}
			if (obj is IDisposable d) {
				d.Dispose();
			}
		}
		private static (Certificate AuthenticationCertificate, Certificate RecipientCertificate, KeyPair AuthenticationKeyPair, KeyPair RecipientKeyPair,
				 KeyId AuthenticationKeyId, KeyId RecipientKeyId) ReadKeyFile(PemObjectReader reader, string sourceName, CancellationToken ct = default) {
			List<object> pemObjects;
			ct.ThrowIfCancellationRequested();
			try {
				pemObjects = reader.ReadAllObjects().ToList();
			}
			catch (Exception ex) {
				throw new KeyFileException("Error while reading key file {sourceName}.", ex);
			}
			ct.ThrowIfCancellationRequested();
			var keyPairs = pemObjects.OfType<KeyPair>().Concat(pemObjects.OfType<PrivateKey>().Select(privKey => privKey.DeriveKeyPair())).ToDictionary(keyPair => keyPair.Public.CalculateId());
			var certificates = pemObjects.OfType<Certificate>().ToList();
			var authCert = certificates.FirstOrDefault(cert => cert.AllowedKeyUsages.GetValueOrDefault(KeyUsages.NoneDefined).HasFlag(KeyUsages.DigitalSignature));
			var recipientCert = certificates.FirstOrDefault(cert => cert.AllowedKeyUsages.GetValueOrDefault(KeyUsages.NoneDefined).HasFlag(KeyUsages.KeyEncipherment));
			if (authCert == null) {
				throw new KeyFileException($"The given key file {sourceName} does not contain a certificate with DigitalSignature key usage for authentication.");
			}
			if (recipientCert == null) {
				throw new KeyFileException($"The given key file {sourceName} does not contain a certificate with KeyEncipherment key usage for decryption.");
			}
			ct.ThrowIfCancellationRequested();
			var authKeyId = authCert.PublicKey.CalculateId();
			var recipientKeyId = recipientCert.PublicKey.CalculateId();
			var authKeyPair = keyPairs.GetValueOrDefault(authKeyId);
			var recipientKeyPair = keyPairs.GetValueOrDefault(recipientKeyId);
			if (authKeyPair == null) {
				throw new KeyFileException($"The given key file {sourceName} does not contain a key pair for the certififacte with DigitalSignature key usage for authentication.");
			}
			if (recipientKeyPair == null) {
				throw new KeyFileException($"The given key file {sourceName} does not contain a key pair for the certificate with KeyEncipherment key usage for decryption.");
			}
			ct.ThrowIfCancellationRequested();
			return (authCert, recipientCert, authKeyPair, recipientKeyPair, authKeyId, recipientKeyId);
		}

		private class PerAppState {
			internal AuthorizationData AuthData { get; set; }
			internal ILogExporterApiClient LogExporterApiClient { get; set; }
			internal IUserExporterApiClient UserExporterApiClient { get; set; }

			internal PerAppState(AuthorizationData authData, ILogExporterApiClient logExporterApiClient, IUserExporterApiClient userExporterApiClient) {
				AuthData = authData;
				LogExporterApiClient = logExporterApiClient;
				UserExporterApiClient = userExporterApiClient;
			}
		}

		private async Task<PerAppState> GetPerAppStateAsync(CancellationToken ct) {
			if (CurrentAppName == null) {
				throw new InvalidOperationException("No current app selected.");
			}
			string appName = CurrentAppName;
			if (perAppStates.TryGetValue(CurrentAppName, out var currentAppState)) {
				if (!currentAppState.AuthData.Valid) {
					if (authenticator == null) {
						throw new InvalidOperationException("No authenticator present.");
					}
					currentAppState.AuthData = await authenticator.AuthenticateAsync(appName, ct);
					currentAppState.LogExporterApiClient.Authorization = currentAppState.AuthData;
					currentAppState.UserExporterApiClient.Authorization = currentAppState.AuthData;
				}
				return currentAppState;
			}
			else {
				if (authenticator == null) {
					throw new InvalidOperationException("No authenticator present.");
				}
				var authData = await authenticator.AuthenticateAsync(appName, ct);
				if (CurrentKeyIds == null) {
					throw new InvalidOperationException("No current key id.");
				}
				if (CurrentKeyCertificates == null) {
					throw new InvalidOperationException("No current key certificate.");
				}
				var args = new SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments(httpClient, LoggerFactory, randomGenerator, configurator.CustomArgumentFactories,
					appName, authData, CurrentKeyIds.Value.AuthenticationKeyId, CurrentKeyCertificates.Value.AuthenticationCertificate,
					CurrentKeyIds.Value.DecryptionKeyId, CurrentKeyCertificates.Value.DecryptionCertificate);
				var logClient = configurator.LogApiClient.Factory(args);
				var userClient = configurator.UserApiClient.Factory(args);
				PerAppState perAppState = new PerAppState(authData, logClient, userClient);
				perAppStates[appName] = perAppState;
				return perAppState;
			}
		}

		private async IAsyncEnumerable<(LogFileMetadata Metadata, Stream? Content)> GetDecryptedLogFilesAsyncImpl(PerAppState perAppState, KeyId recipientKeyId, KeyPair recipientKeyPair, LogFileQuery query, CancellationToken ctOuter, [EnumeratorCancellation] CancellationToken ctInner = default) {
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ctOuter, ctInner);
			var ct = cts.Token;
			var logClient = perAppState.LogExporterApiClient;
			var metaDTOs = await logClient.GetMetadataForAllLogsAsync(recipientKeyId, ct);
			metaDTOs = query.ApplyTo(metaDTOs);
			var keyDecryptor = new KeyDecryptor(recipientKeyPair);
			var logs = metaDTOs.MapBufferedAsync<DownstreamLogMetadataDTO, (LogFileMetadata Metadata, Stream? Content)>(16, async mdto => {
				var encryptedContent = await logClient.GetLogContentByIdAsync(mdto.LogFileId, ct);
				var metadata = new LogFileMetadata(mdto.LogFileId, mdto.UserId, mdto.CreationTime, mdto.EndTime, mdto.UploadTime, mdto.NameSuffix, mdto.LogContentEncoding, mdto.Size);
				var dataDecryptor = DataDecryptor.FromEncryptionInfo(mdto.EncryptionInfo, keyDecryptor);
				if (dataDecryptor == null) {
					logger.LogError("No recipient key for the current decryption key pair {keyId} for log file {logId}, can't decrypt.", recipientKeyId, mdto.LogFileId);
					return (metadata, null);
				}
				try {
					Stream? content = dataDecryptor.OpenDecryptionReadStream(encryptedContent, 0, leaveOpen: false);
					if (mdto.LogContentEncoding == LogContentEncoding.GZipCompressed) {
						content = new GZipStream(content, CompressionMode.Decompress, leaveOpen: false);
					}
					return (metadata, content);
				}
				catch (Exception ex) {
					logger.LogError(ex, "Couldn't decrypt log file {logId} using key pair {keyId}.", mdto.LogFileId, recipientKeyId);
					return (metadata, null);
				}
			}, ct);
			await foreach (var log in logs.ConfigureAwait(false).WithCancellation(ct)) {
				yield return log;
			}
		}

		private async IAsyncEnumerable<UserRegistrationData> GetDecryptedUserRegistrationsAsyncImpl(PerAppState perAppState, KeyId recipientKeyId, KeyPair recipientKeyPair, UserRegistrationQuery query, CancellationToken ctOuter, [EnumeratorCancellation] CancellationToken ctInner = default) {
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ctOuter, ctInner);
			var ct = cts.Token;
			var userClient = perAppState.UserExporterApiClient;
			var userDTOs = await userClient.GetMetadataForAllUsersAsync(recipientKeyId, ct);
			userDTOs = query.ApplyTo(userDTOs);
			var keyDecryptor = new KeyDecryptor(recipientKeyPair);
			var propertyJsonOptions = new JsonSerializerOptions(JsonOptions.UserPropertiesOptions);
			foreach (var udto in userDTOs) {
				if (udto.EncryptedProperties == null) {
					logger.LogTrace("User registration {id} has no encrypted properties, providing empty properties dictionary.", udto.UserId);
					yield return new UserRegistrationData(udto.UserId, udto.Username, udto.StudySpecificProperties, new Dictionary<string, object?>());
				}
				else if (udto.PropertyEncryptionInfo == null) {
					logger.LogError("No encryption info for encrypted properties on user registration {id}, can't decrypt.", udto.UserId);
					yield return new UserRegistrationData(udto.UserId, udto.Username, udto.StudySpecificProperties, null);
				}
				else {
					UserRegistrationData result;
					try {
						var dataDecryptor = DataDecryptor.FromEncryptionInfo(udto.PropertyEncryptionInfo, keyDecryptor);
						if (dataDecryptor == null) {
							logger.LogError("No recipient key for the current decryption key pair {keyId} for encrypted properties on user registration {id}, can't decrypt.", recipientKeyId, udto.UserId);
							result = new UserRegistrationData(udto.UserId, udto.Username, udto.StudySpecificProperties, null);
						}
						else {
							var decryptedBytes = dataDecryptor.DecryptData(udto.EncryptedProperties, 0);
							using var propStream = new MemoryStream(decryptedBytes, writable: false);
							var props = await JsonSerializer.DeserializeAsync<Dictionary<string, object?>>(propStream, propertyJsonOptions, ct);
							if (props == null) {
								logger.LogError("Read null value from encrypted properties for user registration {id}.", udto.UserId);
							}
							result = new UserRegistrationData(udto.UserId, udto.Username, udto.StudySpecificProperties, props);
						}
					}
					catch (Exception ex) {
						logger.LogError(ex, "Couldn't decrypt and read encrypted properties of user {id} using key pair {keyid}.", udto.UserId, recipientKeyId);
						result = new UserRegistrationData(udto.UserId, udto.Username, udto.StudySpecificProperties, null);
					}
					yield return result;
				}
			}
		}

	}
}
