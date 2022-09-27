using Microsoft.Extensions.Logging;
using SGL.Utilities;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public class SglAnalyticsExporterConfiguratorFactoryArguments {
		/// <summary>
		/// The <see cref="HttpClient"/> object for the client that component implementations should use for making requests to the backend.
		/// The <see cref="HttpClient.BaseAddress"/> is expected to be set to the base adress of the backend server, e.g. <c>https://sgl-analytics.example.com/</c>.
		/// Component implementations need to add their relative API paths and parameters under this.
		/// </summary>
		public HttpClient HttpClient { get; }
		/// <summary>
		/// The <see cref="ILoggerFactory"/> that shall be used for diagnostics logging of components.
		/// </summary>
		public ILoggerFactory LoggerFactory { get; }
		/// <summary>
		/// A cryptographic random generator that components can use.
		/// </summary>
		public RandomGenerator Random { get; }

		private ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsExporterConfiguratorFactoryArguments, SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments> customArgumentFactories;
		/// <summary>
		/// Attempts to obtain a custom argument object of type <typeparamref name="T"/> from the registered custom argument factories.
		/// </summary>
		/// <typeparam name="T">The custom argument type to obtain.</typeparam>
		/// <returns>The created (or cached) object of type <typeparamref name="T"/>, or null if no factory for the type was found.</returns>
		public T? GetCustomArgument<T>() where T : class {
			return customArgumentFactories.GetCustomArgument<T>(this);
		}

		internal SglAnalyticsExporterConfiguratorFactoryArguments(HttpClient httpClient, ILoggerFactory loggerFactory, RandomGenerator random,
				ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsExporterConfiguratorFactoryArguments, SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments> customArgumentFactories) {
			HttpClient = httpClient;
			LoggerFactory = loggerFactory;
			Random = random;
			this.customArgumentFactories = customArgumentFactories;
		}
	}

	public class SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments : SglAnalyticsExporterConfiguratorFactoryArguments {
		/// <summary>
		/// The technical name of the application for which analytics logs are exported. This is used for identifying the application in the backend.
		/// </summary>
		public string AppName { get; }
		/// <summary>
		/// The authorization token data for the user session.
		/// </summary>
		public AuthorizationData Authorization { get; }

		public KeyId AuthenticationKeyId { get; }
		public Certificate AuthenticationCertificate { get; }
		public KeyId DecryptionKeyId { get; }
		public Certificate DecryptionCertificate { get; }

		internal SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments(HttpClient httpClient, ILoggerFactory loggerFactory, RandomGenerator random, ConfiguratorCustomArgumentFactoryContainer<SglAnalyticsExporterConfiguratorFactoryArguments, SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments> customArgumentFactories, string appName, AuthorizationData authorization, KeyId authenticationKeyId, Certificate authenticationCertificate, KeyId decryptionKeyId, Certificate decryptionCertificate) : base(httpClient, loggerFactory, random, customArgumentFactories) {
			AppName = appName;
			Authorization = authorization;
			AuthenticationKeyId = authenticationKeyId;
			AuthenticationCertificate = authenticationCertificate;
			DecryptionKeyId = decryptionKeyId;
			DecryptionCertificate = decryptionCertificate;
		}

	}

	public interface ISglAnalyticsExporterConfigurator {
		/// <summary>
		/// Sets the function used to obtain the maximum number of concurrent in-flight requests for an operation.
		/// </summary>
		/// <param name="requestConcurrencyGetter">The function to use.</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsExporterConfigurator"/> object for chaining.</returns>
		ISglAnalyticsExporterConfigurator UseRequestConcurrency(Func<int> requestConcurrencyGetter);
		/// <summary>
		/// Sets the factory for the <see cref="ILoggerFactory"/> object to use for diagnostics logging.
		/// </summary>
		/// <param name="loggerFactoryFactory">The factory function to use.</param>
		/// <param name="dispose">Whether the object shall be disposed when the <see cref="SglAnalyticsExporter"/> object is disposed. (Only applies if the object returned from the factory implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.)</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsExporterConfigurator"/> object for chaining.</returns>
		ISglAnalyticsExporterConfigurator UseLoggerFactory(Func<SglAnalyticsExporterConfiguratorFactoryArguments, ILoggerFactory> loggerFactoryFactory, bool dispose = true);

		ISglAnalyticsExporterConfigurator UseAuthenticator(Func<SglAnalyticsExporterConfiguratorFactoryArguments, KeyPair, IExporterAuthenticator> authenticatorFactory, bool dispose = true);
		ISglAnalyticsExporterConfigurator UseUserApiClient(Func<SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments, IUserExporterApiClient> userExporterFactory, bool dispose = true);
		ISglAnalyticsExporterConfigurator UseLogApiClient(Func<SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments, ILogExporterApiClient> logExporterFactory, bool dispose = true);

		/// <summary>
		/// Installs a factory for custom arguments of type <typeparamref name="T"/> that will be made available to other factories through <see cref="SglAnalyticsExporterConfiguratorFactoryArguments.GetCustomArgument{T}"/>.
		/// </summary>
		/// <typeparam name="T">The argument type to be created.</typeparam>
		/// <param name="factory">The factory object used to create the argument object.</param>
		/// <param name="cacheResult">Indicates whether the created objects shall be cached for reuse.</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsExporterConfigurator"/> object for chaining.</returns>
		ISglAnalyticsExporterConfigurator UseCustomArgumentFactory<T>(Func<SglAnalyticsExporterConfiguratorFactoryArguments, T> factory, bool cacheResult = false) where T : class;
		/// <summary>
		/// Installs a factory for custom arguments of type <typeparamref name="T"/> that will be made available to other factories through <see cref="SglAnalyticsExporterConfiguratorFactoryArguments.GetCustomArgument{T}"/> 
		/// on <see cref="SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments"/> objects.
		/// </summary>
		/// <typeparam name="T">The argument type to be created.</typeparam>
		/// <param name="factory">The factory object used to create the argument object.</param>
		/// <param name="cacheResult">Indicates whether the created objects shall be cached for reuse.</param>
		/// <returns>A reference to this <see cref="ISglAnalyticsExporterConfigurator"/> object for chaining.</returns>
		ISglAnalyticsExporterConfigurator UseAuthenticatedCustomArgumentFactory<T>(Func<SglAnalyticsExporterConfiguratorAuthenticatedFactoryArguments, T> factory, bool cacheResult = false) where T : class;
	}
}
