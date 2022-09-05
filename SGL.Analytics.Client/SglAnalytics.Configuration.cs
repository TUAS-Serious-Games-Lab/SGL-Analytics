using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.EndToEnd;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace SGL.Analytics.Client {
	public partial class SglAnalytics {
		internal class SglAnalyticsConfigurator : ISglAnalyticsConfigurator {
			internal SglAnalyticsConfigurator() {
				DataDirectorySource = args => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), args.AppName);
				SynchronizationContextGetter = () => SynchronizationContext.Current ?? throw new InvalidOperationException("No SynchronizationContext set. " +
					"SGL Analytics requires a synchronized SynchronizationContext that can be used to dispatch event handler invocations to the main thread.");
				LoggerFactory = (args => NullLoggerFactory.Instance, true);
				RootDataStoreFactory = (args => new FileRootDataStore(args.AppName), true);
				LogStorageFactory = (args => new DirectoryLogStorage(Path.Combine(args.DataDirectory, "DataLogs")), true);
				LogCollectorClientFactory = (args => new LogCollectorRestClient(args.HttpClient.BaseAddress), true);
				UserRegistrationClientFactory = (args => new UserRegistrationRestClient(args.HttpClient.BaseAddress), true);
				RecipientCertificateValidatorFactory = (args => throw new MissingSglAnalyticsConfigurationException(nameof(ISglAnalyticsConfigurator.UseRecipientCertificateValidator)), true);
			}

			internal Func<SglAnalyticsConfiguratorDataDirectorySourceArguments, string> DataDirectorySource { get; private set; }
			internal Func<SynchronizationContext> SynchronizationContextGetter { get; private set; }
			internal (Func<SglAnalyticsConfiguratorFactoryArguments, ILoggerFactory> Factory, bool Dispose) LoggerFactory { get; private set; }
			internal (Func<SglAnalyticsConfiguratorFactoryArguments, IRootDataStore> Factory, bool Dispose) RootDataStoreFactory { get; private set; }
			internal (Func<SglAnalyticsConfiguratorFactoryArguments, ILogStorage> Factory, bool Dispose) LogStorageFactory { get; private set; }
			internal (Func<SglAnalyticsConfiguratorFactoryArguments, ILogCollectorClient> Factory, bool Dispose) LogCollectorClientFactory { get; private set; }
			internal (Func<SglAnalyticsConfiguratorFactoryArguments, IUserRegistrationClient> Factory, bool Dispose) UserRegistrationClientFactory { get; private set; }
			internal (Func<SglAnalyticsConfiguratorFactoryArguments, ICertificateValidator> Factory, bool Dispose) RecipientCertificateValidatorFactory { get; private set; }
			internal ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsConfiguratorFactoryArguments, SglAnalyticsConfiguratorAuthenticatedFactoryArguments> CustomArgumentFactories { get; private set; } =
				new ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsConfiguratorFactoryArguments, SglAnalyticsConfiguratorAuthenticatedFactoryArguments>();
			internal CryptoConfig CryptoConfig() {
				var config = new CryptoConfig { };
				CryptoConfigurator?.Invoke(new CryptoConfigurator(config));
				return config;
			}
			internal Action<ICryptoConfigurator>? CryptoConfigurator { get; private set; }

			public ISglAnalyticsConfigurator UseDataDirectory(Func<SglAnalyticsConfiguratorDataDirectorySourceArguments, string> dataDirectorySource) {
				DataDirectorySource = dataDirectorySource;
				return this;
			}
			public ISglAnalyticsConfigurator UseSynchronizationContext(Func<SynchronizationContext> synchronizationContextGetter) {
				SynchronizationContextGetter = synchronizationContextGetter;
				return this;
			}
			public ISglAnalyticsConfigurator UseLoggerFactory(Func<SglAnalyticsConfiguratorFactoryArguments, ILoggerFactory> loggerFactoryFactory, bool dispose = true) {
				LoggerFactory = (loggerFactoryFactory, dispose);
				return this;
			}
			public ISglAnalyticsConfigurator UseRootDataStore(Func<SglAnalyticsConfiguratorFactoryArguments, IRootDataStore> rootDataStoreFactory, bool dispose = true) {
				RootDataStoreFactory = (rootDataStoreFactory, dispose);
				return this;
			}
			public ISglAnalyticsConfigurator UseLogStorage(Func<SglAnalyticsConfiguratorFactoryArguments, ILogStorage> logStorageFactory, bool dispose = true) {
				LogStorageFactory = (logStorageFactory, dispose);
				return this;
			}
			public ISglAnalyticsConfigurator UseLogCollectorClient(Func<SglAnalyticsConfiguratorFactoryArguments, ILogCollectorClient> logCollectorClientFactory, bool dispose = true) {
				LogCollectorClientFactory = (logCollectorClientFactory, dispose);
				return this;
			}
			public ISglAnalyticsConfigurator UseUserRegistrationClient(Func<SglAnalyticsConfiguratorFactoryArguments, IUserRegistrationClient> userRegistrationClientFactory, bool dispose = true) {
				UserRegistrationClientFactory = (userRegistrationClientFactory, dispose);
				return this;
			}
			public ISglAnalyticsConfigurator UseRecipientCertificateValidator(Func<SglAnalyticsConfiguratorFactoryArguments, ICertificateValidator> recipientCertificateValidatorFactory, bool dispose = true) {
				RecipientCertificateValidatorFactory = (recipientCertificateValidatorFactory, dispose);
				return this;
			}

			public ISglAnalyticsConfigurator UseAuthenticatedCustomArgumentFactory<T>(Func<SglAnalyticsConfiguratorAuthenticatedFactoryArguments, T> factory, bool cacheResult = false) where T : class {
				CustomArgumentFactories.SetCustomArgumentFactory(factory, cacheResult);
				return this;
			}
			public ISglAnalyticsConfigurator UseCustomArgumentFactory<T>(Func<SglAnalyticsConfiguratorFactoryArguments, T> factory, bool cacheResult = false) where T : class {
				CustomArgumentFactories.SetCustomArgumentFactory(factory, cacheResult);
				return this;
			}

			public ISglAnalyticsConfigurator ConfigureCryptography(Action<ICryptoConfigurator> configure) {
				CryptoConfigurator += configure;
				return this;
			}
		}
		internal class CryptoConfigurator : ICryptoConfigurator {
			CryptoConfig config;

			public CryptoConfigurator(CryptoConfig config) {
				this.config = config;
			}
			public ICryptoConfigurator AllowSharedMessageKeyPair() {
				config.AllowSharedMessageKeyPair = true;
				return this;
			}
			public ICryptoConfigurator DisallowSharedMessageKeyPair() {
				config.AllowSharedMessageKeyPair = false;
				return this;
			}
			public ICryptoConfigurator UseDataEncryptionMode(DataEncryptionMode mode = DataEncryptionMode.AES_256_CCM) {
				config.DataEncryptionMode = mode;
				return this;
			}
		}
	}
}
