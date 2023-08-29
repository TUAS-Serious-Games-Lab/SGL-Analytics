using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application {
	/// <summary>
	/// Provides the <see cref="UseUsersBackendAppplicationLayer(IServiceCollection, IConfiguration)"/> extension method.
	/// </summary>
	public static class ServiceCollectionExtensions {
		/// <summary>
		/// Registers the application layer services for the SGL Analytics users backend service to <paramref name="services"/>.
		/// </summary>
		/// <param name="services">The service collection to which to add the services.</param>
		/// <param name="config">The configuration root from which to obtain relevant configuration for the services.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseUsersBackendAppplicationLayer(this IServiceCollection services, IConfiguration config) {
			services.AddSingleton<IKeyAuthChallengeStateHolder, KeyAuthChallengeStateHolder>();
			services.AddHostedService<KeyAuthChallengeStateCleanupService>();
			services.AddScoped<IUserManager, UserManager>();
			services.Configure<KeyAuthOptions>(config.GetSection(KeyAuthOptions.ExporterKeyAuth));
			services.AddScoped<IKeyAuthManager, KeyAuthManager>();
			return services;
		}
	}
}
