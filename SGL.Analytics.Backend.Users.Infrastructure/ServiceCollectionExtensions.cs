using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.Backend.Users.Infrastructure.Services;

namespace SGL.Analytics.Backend.Users.Infrastructure {
	/// <summary>
	/// Provides the <see cref="UseUsersBackendInfrastructure(IServiceCollection, IConfiguration)"/> extension method.
	/// </summary>
	public static class ServiceCollectionExtensions {
		/// <summary>
		/// Adds the infrastructure services classes for the SGL Analytics user registration backend service.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The root config object to obtain configuration entries from.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseUsersBackendInfrastructure(this IServiceCollection services, IConfiguration config) {
			services.AddDbContext<UsersContext>(options => options.UseNpgsql(config.GetConnectionString("UsersContext")));

			services.AddScoped<IApplicationRepository, DbApplicationRepository>();
			services.AddScoped<IUserRepository, DbUserRepository>();
			services.AddSingleton<IMetricsManager, MetricsManager>();

			return services;
		}
	}
}
