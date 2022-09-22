using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Utilities;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public partial class SglAnalyticsExporter {
		private class SglAnalyticsExporterConfigurator : ISglAnalyticsExporterConfigurator {
			internal SglAnalyticsExporterConfigurator() {
				SynchronizationContextGetter = () => SynchronizationContext.Current ?? throw new InvalidOperationException("No SynchronizationContext set. " +
					"SGL Analytics requires a synchronized SynchronizationContext that can be used to dispatch event handler invocations to the main thread.");
				LoggerFactory = (args => NullLoggerFactory.Instance, true);
				Authenticator = ((args, keyPair) => new ExporterKeyPairAuthenticator(args.HttpClient, keyPair, args.LoggerFactory.CreateLogger<ExporterKeyPairAuthenticator>(), args.Random), true);
				LogApiClient = (args => new LogExporterApiClient(args.HttpClient, args.Authorization), true);
				UserApiClient = (args => new UserExporterApiClient(args.HttpClient, args.Authorization), true);
			}

			internal Func<SynchronizationContext> SynchronizationContextGetter { get; private set; }

			internal (Func<SglAnalyticsExporterConfiguratorFactoryArguments, ILoggerFactory> Factory, bool Dispose) LoggerFactory;

			internal (Func<SglAnalyticsExporterConfiguratorFactoryArguments, KeyPair, IExporterAuthenticator> Factory, bool Dispose) Authenticator;

			internal (Func<SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments, ILogExporterApiClient> Factory, bool Dispose) LogApiClient { get; private set; }
			internal (Func<SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments, IUserExporterApiClient> Factory, bool Dispose) UserApiClient { get; private set; }

			internal ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsExporterConfiguratorFactoryArguments, SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments> CustomArgumentFactories { get; private set; } =
				new ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsExporterConfiguratorFactoryArguments, SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments>();

			public ISglAnalyticsExporterConfigurator UseSynchronizationContext(Func<SynchronizationContext> synchronizationContextGetter) {
				SynchronizationContextGetter = synchronizationContextGetter;
				return this;
			}
			public ISglAnalyticsExporterConfigurator UseLoggerFactory(Func<SglAnalyticsExporterConfiguratorFactoryArguments, ILoggerFactory> loggerFactoryFactory, bool dispose = true) {
				LoggerFactory = (loggerFactoryFactory, dispose);
				return this;
			}

			public ISglAnalyticsExporterConfigurator UseAuthenticator(Func<SglAnalyticsExporterConfiguratorFactoryArguments, KeyPair, IExporterAuthenticator> authenticatorFactory, bool dispose = true) {
				Authenticator = (authenticatorFactory, dispose);
				return this;
			}

			public ISglAnalyticsExporterConfigurator UseLogApiClient(Func<SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments, ILogExporterApiClient> logExporterFactory, bool dispose = true) {
				LogApiClient = (logExporterFactory, dispose);
				return this;
			}
			public ISglAnalyticsExporterConfigurator UseUserApiClient(Func<SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments, IUserExporterApiClient> userExporterFactory, bool dispose = true) {
				UserApiClient = (userExporterFactory, dispose);
				return this;
			}

			public ISglAnalyticsExporterConfigurator UseCustomArgumentFactory<T>(Func<SglAnalyticsExporterConfiguratorFactoryArguments, T> factory, bool cacheResult = false) where T : class {
				CustomArgumentFactories.SetCustomArgumentFactory(factory, cacheResult);
				return this;
			}
			public ISglAnalyticsExporterConfigurator UseAuthenticatedCustomArgumentFactory<T>(Func<SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments, T> factory, bool cacheResult = false) where T : class {
				CustomArgumentFactories.SetCustomArgumentFactory(factory, cacheResult);
				return this;
			}
		}
	}
}
