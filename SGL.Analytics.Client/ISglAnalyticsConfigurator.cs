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
	public class SglAnalyticsConfiguratorDataDirectorySourceArguments {
		public string AppName { get; }

		internal SglAnalyticsConfiguratorDataDirectorySourceArguments(string appName) {
			AppName = appName;
		}
	}

	public class SglAnalyticsConfiguratorFactoryArguments {
		public string AppName { get; }
		public string AppApiToken { get; }
		public HttpClient HttpClient { get; }
		public string DataDirectory { get; }
		public ILoggerFactory LoggerFactory { get; }
		public RandomGenerator Random { get; }

		private ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsConfiguratorFactoryArguments, SglAnalyticsConfiguratorAuthenticatedFactoryArguments> customArgumentFactories;
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


	public class SglAnalyticsConfiguratorAuthenticatedFactoryArguments : SglAnalyticsConfiguratorFactoryArguments {
		public AuthorizationData Authorization { get; }

		internal SglAnalyticsConfiguratorAuthenticatedFactoryArguments(string appName, string appApiToken, HttpClient httpClient, string neutralDataDirectory, ILoggerFactory loggerFactory, RandomGenerator random,
			ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsConfiguratorFactoryArguments, SglAnalyticsConfiguratorAuthenticatedFactoryArguments> customArgumentFactories, AuthorizationData authorization) : base(appName, appApiToken, httpClient, neutralDataDirectory, loggerFactory, random, customArgumentFactories) {
			Authorization = authorization;
		}
	}

	public interface ICryptoConfigurator {
		ICryptoConfigurator AllowSharedMessageKeyPair();
		ICryptoConfigurator DisallowSharedMessageKeyPair();
		ICryptoConfigurator UseDataEncryptionMode(DataEncryptionMode mode = DataEncryptionMode.AES_256_CCM);
	}

	public interface ISglAnalyticsConfigurator {
		ISglAnalyticsConfigurator UseDataDirectory(Func<SglAnalyticsConfiguratorDataDirectorySourceArguments, string> dataDirectorySource);
		ISglAnalyticsConfigurator UseSynchronizationContext(Func<SynchronizationContext> synchronizationContextGetter);
		ISglAnalyticsConfigurator UseLoggerFactory(Func<SglAnalyticsConfiguratorFactoryArguments, ILoggerFactory> loggerFactoryFactory, bool dispose = true);
		ISglAnalyticsConfigurator UseUserRegistrationClient(Func<SglAnalyticsConfiguratorFactoryArguments, IUserRegistrationClient> userRegistrationClientFactory, bool dispose = true);
		ISglAnalyticsConfigurator UseLogCollectorClient(Func<SglAnalyticsConfiguratorFactoryArguments, ILogCollectorClient> logCollectorClientFactory, bool dispose = true);
		ISglAnalyticsConfigurator UseRootDataStore(Func<SglAnalyticsConfiguratorFactoryArguments, IRootDataStore> rootDataStoreFactory, bool dispose = true);
		ISglAnalyticsConfigurator UseLogStorage(Func<SglAnalyticsConfiguratorFactoryArguments, ILogStorage> logStorageFactory, bool dispose = true);
		ISglAnalyticsConfigurator UseRecipientCertificateValidator(Func<SglAnalyticsConfiguratorFactoryArguments, ICertificateValidator> recipientCertificateValidatorFactory, bool dispose = true);
		ISglAnalyticsConfigurator ConfigureCryptography(Action<ICryptoConfigurator> configure);
		ISglAnalyticsConfigurator UseCustomArgumentFactory<T>(Func<SglAnalyticsConfiguratorFactoryArguments, T> factory, bool cacheResult = false) where T : class;
		ISglAnalyticsConfigurator UseAuthenticatedCustomArgumentFactory<T>(Func<SglAnalyticsConfiguratorAuthenticatedFactoryArguments, T> factory, bool cacheResult = false) where T : class;
	}

	public static class SglAnalyticsConfiguratorSimpleRecipientCertificateValidatorExtensions {
		public static ISglAnalyticsConfigurator UseEmbeddedRecipientCertificateAuthority(this ISglAnalyticsConfigurator configurator, string caCertificatePem, bool ignoreCAValidityPeriod = true) {
			configurator.UseRecipientCertificateValidator(args => new CACertTrustValidator(caCertificatePem, ignoreCAValidityPeriod, args.LoggerFactory.CreateLogger<CACertTrustValidator>(), args.LoggerFactory.CreateLogger<CertificateStore>()));
			return configurator;
		}
		public static ISglAnalyticsConfigurator UseRecipientCertificateAuthorityFromReader(this ISglAnalyticsConfigurator configurator, TextReader caCertificateReader, string sourceName, bool ignoreCAValidityPeriod = true) {
			configurator.UseRecipientCertificateValidator(args => new CACertTrustValidator(caCertificateReader, sourceName, ignoreCAValidityPeriod, args.LoggerFactory.CreateLogger<CACertTrustValidator>(), args.LoggerFactory.CreateLogger<CertificateStore>()));
			return configurator;
		}
		public static ISglAnalyticsConfigurator UseEmbeddedRecipientCertificateSignerKey(this ISglAnalyticsConfigurator configurator, string signerPublicKeyPem) {
			configurator.UseRecipientCertificateValidator(args => new KeyOnlyTrustValidator(signerPublicKeyPem, args.LoggerFactory.CreateLogger<KeyOnlyTrustValidator>()));
			return configurator;
		}
		public static ISglAnalyticsConfigurator UseRecipientCertificateSignerKeyFromReader(this ISglAnalyticsConfigurator configurator, string sourceName, TextReader signerPublicKeyReader) {
			configurator.UseRecipientCertificateValidator(args => new KeyOnlyTrustValidator(signerPublicKeyReader, sourceName, args.LoggerFactory.CreateLogger<KeyOnlyTrustValidator>()));
			return configurator;
		}
	}
}