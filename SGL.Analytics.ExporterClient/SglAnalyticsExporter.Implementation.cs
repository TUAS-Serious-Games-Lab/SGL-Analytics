using SGL.Utilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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

		private IAsyncEnumerable<(LogFileMetadata Metadata, Stream Content)> GetDecryptedLogFilesAsyncImpl(PerAppState perAppState, LogFileQuery query, CancellationToken ctOuter, [EnumeratorCancellation] CancellationToken ctInner = default) {
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ctOuter, ctInner);
			var ct = cts.Token;
			throw new NotImplementedException();
		}

		private IAsyncEnumerable<UserRegistrationData> GetDecryptedUserRegistrationsAsyncImpl(PerAppState perAppState, UserRegistrationQuery query, CancellationToken ctOuter, [EnumeratorCancellation] CancellationToken ctInner = default) {
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ctOuter, ctInner);
			var ct = cts.Token;
			throw new NotImplementedException();
		}

	}
}
