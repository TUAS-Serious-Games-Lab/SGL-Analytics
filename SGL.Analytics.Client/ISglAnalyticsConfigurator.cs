using Microsoft.Extensions.Logging;
using SGL.Utilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace SGL.Analytics.Client {

	/// <summary>
	/// Encapsulates the arguments for the function passed to <see cref="ISglAnalyticsConfigurator.UseDataDirectory(Func{SglAnalyticsConfiguratorDataDirectorySourceArguments, string})"/>.
	/// </summary>
	public class SglAnalyticsConfiguratorDataDirectorySourceArguments {
		/// <summary>
		/// The technical name of the application for which analytics logs are recorded. This is used for identifying the application in the backend.
		/// </summary>
		public string AppName { get; }

		internal SglAnalyticsConfiguratorDataDirectorySourceArguments(string appName) {
			AppName = appName;
		}
	}

	/// <summary>
	/// Encapsulates the arguments made available to factories used in <see cref="ISglAnalyticsConfigurator"/> that don't need an authenticated user session.
	/// </summary>
	public class SglAnalyticsConfiguratorFactoryArguments {
		/// <summary>
		/// The technical name of the application for which analytics logs are recorded. This is used for identifying the application in the backend.
		/// </summary>
		public string AppName { get; }
		/// <summary>
		/// The API token assigned to the application in the backend. This is used as an additional security layer in the communication with the backend.
		/// </summary>
		public string AppApiToken { get; }
		/// <summary>
		/// The <see cref="HttpClient"/> object for the client that component implementations should use for making requests to the backend.
		/// The <see cref="HttpClient.BaseAddress"/> is expected to be set to the base adress of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.
		/// Component implementations need to add their relative API paths and parameters under this.
		/// </summary>
		public HttpClient HttpClient { get; }
		/// <summary>
		/// The local directory that can be used to store SGL Analytics data.
		/// </summary>
		public string DataDirectory { get; }
		/// <summary>
		/// The <see cref="ILoggerFactory"/> that shall be used for diagnostics logging of components.
		/// </summary>
		public ILoggerFactory LoggerFactory { get; }
		/// <summary>
		/// A cryptographic random generator that components can use.
		/// </summary>
		public RandomGenerator Random { get; }

		private ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsConfiguratorFactoryArguments, SglAnalyticsConfiguratorAuthenticatedFactoryArguments> customArgumentFactories;
		/// <summary>
		/// Attempts to obtain a custom argument object of type <typeparamref name="T"/> from the registered custom argument factories.
		/// </summary>
		/// <typeparam name="T">The custom argument type to obtain.</typeparam>
		/// <returns>The created (or cached) object of type <typeparamref name="T"/>, or null if no factory for the type was found.</returns>
		public T? GetCustomArgument<T>() where T : class {
			return customArgumentFactories.GetCustomArgument<T>(this);
		}

		internal SglAnalyticsConfiguratorFactoryArguments(string appName, string appApiToken, HttpClient httpClient, string dataDirectory, ILoggerFactory loggerFactory, RandomGenerator random,
			ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsConfiguratorFactoryArguments, SglAnalyticsConfiguratorAuthenticatedFactoryArguments> customArgumentFactories) {
			AppName = appName;
			AppApiToken = appApiToken;
			HttpClient = httpClient;
			DataDirectory = dataDirectory;
			LoggerFactory = loggerFactory;
			Random = random;
			this.customArgumentFactories = customArgumentFactories;
		}
	}

	/// <summary>
	/// Encapsulates the arguments made available to factories used in <see cref="ISglAnalyticsConfigurator"/> that need an authenticated user session.
	/// </summary>
	public class SglAnalyticsConfiguratorAuthenticatedFactoryArguments : SglAnalyticsConfiguratorFactoryArguments {
		/// <summary>
		/// The authorization token data for the user session.
		/// </summary>
		public AuthorizationData? Authorization { get; }
		/// <summary>
		/// The id of the authenticated user, if applicable.
		/// </summary>
		public Guid? UserId { get; }
		/// <summary>
		/// The username of the authenticated user, if applicable.
		/// </summary>
		public string? Username { get; }

		internal SglAnalyticsConfiguratorAuthenticatedFactoryArguments(string appName, string appApiToken, HttpClient httpClient, string neutralDataDirectory, ILoggerFactory loggerFactory, RandomGenerator random,
			ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsConfiguratorFactoryArguments, SglAnalyticsConfiguratorAuthenticatedFactoryArguments> customArgumentFactories, AuthorizationData? authorization,
			Guid? userId, string? username) : base(appName, appApiToken, httpClient, neutralDataDirectory, loggerFactory, random, customArgumentFactories) {
			Authorization = authorization;
			UserId = userId;
			Username = username;
		}
	}

	/// <summary>
	/// The interface used for configuring the cryptography parameters for a <see cref="SglAnalytics"/> client using the fluent API pattern.
	/// </summary>
	public interface ICryptoConfigurator {
		/// <summary>
		/// If multiple recipients use the same elliptic curve parameters, allow the client to use a shared key-pair per-log for them for ECDH.
		/// </summary>
		/// <returns>A reference to this <see cref="ICryptoConfigurator"/> object for chaining.</returns>
		ICryptoConfigurator AllowSharedMessageKeyPair();
		/// <summary>
		/// If multiple recipients use the same elliptic curve parameters, forbid the client from using a shared key-pair per-log for ECDH and force it to use a seperate key-pair for each recipient.
		/// </summary>
		/// <returns>A reference to this <see cref="ICryptoConfigurator"/> object for chaining.</returns>
		ICryptoConfigurator DisallowSharedMessageKeyPair();
		/// <summary>
		/// Sets the data encryption mode to use for the data logs.
		/// </summary>
		/// <param name="mode">The mode to use.</param>
		/// <returns>A reference to this <see cref="ICryptoConfigurator"/> object for chaining.</returns>
		ICryptoConfigurator UseDataEncryptionMode(DataEncryptionMode mode = DataEncryptionMode.AES_256_CCM);
	}

	/// <summary>
	/// The interface used for configuring the <see cref="SglAnalytics"/> client using the fluent API pattern.
	/// </summary>
	public interface ISglAnalyticsConfigurator {
		/// <summary>
		/// Sets the function used to obtain the data directory for SGL Analytics from <see cref="SglAnalyticsConfiguratorDataDirectorySourceArguments"/> (containing the app name).
		/// </summary>
		/// <param name="dataDirectorySource">The function to use.</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseDataDirectory(Func<SglAnalyticsConfiguratorDataDirectorySourceArguments, string> dataDirectorySource);
		/// <summary>
		/// Sets the function used to obtain the synchronization context for the main context when constructing the <see cref="SglAnalytics"/> object.
		/// </summary>
		/// <param name="synchronizationContextGetter">The function to use.</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseSynchronizationContext(Func<SynchronizationContext> synchronizationContextGetter);
		/// <summary>
		/// Sets the factory for the <see cref="ILoggerFactory"/> object to use for diagnostics logging.
		/// </summary>
		/// <param name="loggerFactoryFactory">The factory function to use.</param>
		/// <param name="dispose">Whether the object shall be disposed when the <see cref="SglAnalytics"/> object is disposed. (Only applies if the object returned from the factory implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.)</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseLoggerFactory(Func<SglAnalyticsConfiguratorFactoryArguments, ILoggerFactory> loggerFactoryFactory, bool dispose = true);
		/// <summary>
		/// Sets the factory used to obtain the <see cref="IUserRegistrationClient"/> implementation.
		/// </summary>
		/// <param name="userRegistrationClientFactory">The factory function to use.</param>
		/// <param name="dispose">Whether the object shall be disposed when the <see cref="SglAnalytics"/> object is disposed. (Only applies if the object returned from the factory implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.)</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseUserRegistrationClient(Func<SglAnalyticsConfiguratorFactoryArguments, IUserRegistrationClient> userRegistrationClientFactory, bool dispose = true);
		/// <summary>
		/// Sets the factory used to obtain the <see cref="ILogCollectorClient"/> implementation.
		/// </summary>
		/// <param name="logCollectorClientFactory">The factory function to use.</param>
		/// <param name="dispose">Whether the object shall be disposed when the <see cref="SglAnalytics"/> object is disposed. (Only applies if the object returned from the factory implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.)</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseLogCollectorClient(Func<SglAnalyticsConfiguratorFactoryArguments, ILogCollectorClient> logCollectorClientFactory, bool dispose = true);
		/// <summary>
		/// Sets the factory used to obtain the <see cref="IRootDataStore"/> implementation.
		/// </summary>
		/// <param name="rootDataStoreFactory">The factory function to use.</param>
		/// <param name="dispose">Whether the object shall be disposed when the <see cref="SglAnalytics"/> object is disposed. (Only applies if the object returned from the factory implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.)</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseRootDataStore(Func<SglAnalyticsConfiguratorFactoryArguments, IRootDataStore> rootDataStoreFactory, bool dispose = true);
		/// <summary>
		/// Sets the factory used to obtain the <see cref="ILogStorage"/> implementation to use when no user is authenticated,
		/// i.e. after calling <see cref="SglAnalytics.UseOfflineModeAsync"/> without stored credentials.
		/// </summary>
		/// <param name="logStorageFactory">The factory function to use.</param>
		/// <param name="dispose">Whether the object shall be disposed when the <see cref="SglAnalytics"/> object is disposed. (Only applies if the object returned from the factory implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.)</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseAnonymousLogStorage(Func<SglAnalyticsConfiguratorFactoryArguments, ILogStorage> logStorageFactory, bool dispose = true);
		/// <summary>
		/// Sets the factory used to obtain the <see cref="ILogStorage"/> implementation when a user is authenticated on the client.
		/// </summary>
		/// <param name="logStorageFactory">The factory function to use.</param>
		/// <param name="dispose">Whether the object shall be disposed when the <see cref="SglAnalytics"/> object is disposed. (Only applies if the object returned from the factory implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.)</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseUserLogStorage(Func<SglAnalyticsConfiguratorAuthenticatedFactoryArguments, ILogStorage> logStorageFactory, bool dispose = true);
		/// <summary>
		/// Sets the factory for the validator object that checks the certificates of data recipients to determine the authorized recipients for end-to-end encrypted data.
		/// </summary>
		/// <param name="recipientCertificateValidatorFactory">The factory function to use.</param>
		/// <param name="dispose">Whether the object shall be disposed when the <see cref="SglAnalytics"/> object is disposed. (Only applies if the object returned from the factory implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.)</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseRecipientCertificateValidator(Func<SglAnalyticsConfiguratorFactoryArguments, ICertificateValidator> recipientCertificateValidatorFactory, bool dispose = true);

		/// <summary>
		/// Configures cryptography parameters using the given configurator action.
		/// </summary>
		/// <param name="configure">An action object to that will be invoked to configure the cryptography parameters.</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator ConfigureCryptography(Action<ICryptoConfigurator> configure);
		/// <summary>
		/// Installs a factory for custom arguments of type <typeparamref name="T"/> that will be made available to other factories through <see cref="SglAnalyticsConfiguratorFactoryArguments.GetCustomArgument{T}"/>.
		/// </summary>
		/// <typeparam name="T">The argument type to be created.</typeparam>
		/// <param name="factory">The factory object used to create the argument object.</param>
		/// <param name="cacheResult">Indicates whether the created objects shall be cached for reuse.</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseCustomArgumentFactory<T>(Func<SglAnalyticsConfiguratorFactoryArguments, T> factory, bool cacheResult = false) where T : class;
		/// <summary>
		/// Installs a factory for custom arguments of type <typeparamref name="T"/> that will be made available to other factories through <see cref="SglAnalyticsConfiguratorFactoryArguments.GetCustomArgument{T}"/> 
		/// on <see cref="SglAnalyticsConfiguratorAuthenticatedFactoryArguments"/> objects.
		/// </summary>
		/// <typeparam name="T">The argument type to be created.</typeparam>
		/// <param name="factory">The factory object used to create the argument object.</param>
		/// <param name="cacheResult">Indicates whether the created objects shall be cached for reuse.</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseAuthenticatedCustomArgumentFactory<T>(Func<SglAnalyticsConfiguratorAuthenticatedFactoryArguments, T> factory, bool cacheResult = false) where T : class;

		/// <summary>
		/// Specifies the strength of the secret that is generated upon user registration.
		/// The secret is created by generating the given number of bytes and then base64-encoding them.
		/// Therefore the actual secret string will be longer due to encoding overhead.
		/// </summary>
		/// <param name="length">The number of bytes to use.</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		ISglAnalyticsConfigurator UseLegthOfGeneratedUserSecrets(int length);
	}

	/// <summary>
	/// Provides short-hand methods for <see cref="ISglAnalyticsConfigurator.UseRecipientCertificateValidator(Func{SglAnalyticsConfiguratorFactoryArguments, ICertificateValidator}, bool)"/>.
	/// </summary>
	public static class SglAnalyticsConfiguratorSimpleRecipientCertificateValidatorExtensions {
		/// <summary>
		/// Sets the client up to use a string value (usually from a constant) containing CA / signer certificates (in PEM format) to validate recipient certificates.
		/// </summary>
		/// <param name="configurator">The configurator object on which to setup the validator.</param>
		/// <param name="caCertificatePem">The PEM string to load.</param>
		/// <param name="ignoreCAValidityPeriod">
		/// Indicates whether the validity period of the certificates is ignored. This can be used to avoid expiration of the certifiactes when they are baked into shipped
		/// software, that may not be updated in time to replace expired CA certificates.
		/// </param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		public static ISglAnalyticsConfigurator UseEmbeddedRecipientCertificateAuthority(this ISglAnalyticsConfigurator configurator, string caCertificatePem, bool ignoreCAValidityPeriod = true) {
			configurator.UseRecipientCertificateValidator(args => new CACertTrustValidator(caCertificatePem, ignoreCAValidityPeriod, args.LoggerFactory.CreateLogger<CACertTrustValidator>(), args.LoggerFactory.CreateLogger<CertificateStore>()));
			return configurator;
		}
		/// <summary>
		/// Sets the client up to validate recipient certificates using CA / signer certificates loaded from the given reader in PEM format.
		/// </summary>
		/// <param name="configurator">The configurator object on which to setup the validator.</param>
		/// <param name="getCaCertificateReader">
		/// A function to open the reader to load the PEM data from.
		/// Is invoked when the <see cref="SglAnalytics"/> object is configured.
		/// The returned reader is disposed automatically.
		/// </param>
		/// <param name="sourceName">The name for the source to use for log messages, usually the file name or similar.</param>
		/// <param name="ignoreCAValidityPeriod">
		/// Indicates whether the validity period of the certificates is ignored. This can be used to avoid expiration of the certifiactes when they are baked into shipped
		/// software, that may not be updated in time to replace expired CA certificates.
		/// </param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		public static ISglAnalyticsConfigurator UseRecipientCertificateAuthorityFromReader(this ISglAnalyticsConfigurator configurator, Func<TextReader> getCaCertificateReader, string sourceName, bool ignoreCAValidityPeriod = true) {
			configurator.UseRecipientCertificateValidator(args => {
				using var reader = getCaCertificateReader();
				return new CACertTrustValidator(reader, sourceName, ignoreCAValidityPeriod, args.LoggerFactory.CreateLogger<CACertTrustValidator>(), args.LoggerFactory.CreateLogger<CertificateStore>());
			});
			return configurator;
		}
		/// <summary>
		/// Sets the client up to validate recipient certificates using an accept-list of signer public keys, i.e. only certificates signed using one of the listed keys are accepted.
		/// The public keys are loaded from a PEM string, usually an embedded string constant.
		/// </summary>
		/// <param name="configurator">The configurator object on which to setup the validator.</param>
		/// <param name="signerPublicKeyPem">The PEM string to load.</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		public static ISglAnalyticsConfigurator UseEmbeddedRecipientCertificateSignerKey(this ISglAnalyticsConfigurator configurator, string signerPublicKeyPem) {
			configurator.UseRecipientCertificateValidator(args => new KeyOnlyTrustValidator(signerPublicKeyPem, args.LoggerFactory.CreateLogger<KeyOnlyTrustValidator>()));
			return configurator;
		}
		/// <summary>
		/// Sets the client up to validate recipient certificates using an accept-list of signer public keys, i.e. only certificates signed using one of the listed keys are accepted.
		/// The public keys are loaded in PEM format from the given reader.
		/// </summary>
		/// <param name="configurator">The configurator object on which to setup the validator.</param>
		/// <param name="sourceName">The name for the source to use for log messages, usually the file name or similar.</param>
		/// <param name="getSignerPublicKeyReader">
		/// A function to open the reader to load the PEM data from.
		/// Is invoked when the <see cref="SglAnalytics"/> object is configured.
		/// The returned reader is disposed automatically.
		/// </param>
		/// <returns>A reference to this <see cref="ISglAnalyticsConfigurator"/> object for chaining.</returns>
		public static ISglAnalyticsConfigurator UseRecipientCertificateSignerKeyFromReader(this ISglAnalyticsConfigurator configurator, string sourceName, Func<TextReader> getSignerPublicKeyReader) {
			configurator.UseRecipientCertificateValidator(args => {
				using var reader = getSignerPublicKeyReader();
				return new KeyOnlyTrustValidator(reader, sourceName, args.LoggerFactory.CreateLogger<KeyOnlyTrustValidator>());
			});
			return configurator;
		}
	}
}
