using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Utilities.Backend.Applications;

namespace SGL.Analytics.Backend.Logs.Infrastructure {
	/// <summary>
	/// Provides the <see cref="UseLogsBackendInfrastructure(IServiceCollection, IConfiguration)"/> extension method.
	/// </summary>
	public static class ServiceCollectionExtensions {
		/// <summary>
		/// Adds the infrastructure services classes for the SGL Analytics logs collector backend service.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The root config object to obtain configuration entries from.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseLogsBackendInfrastructure(this IServiceCollection services, IConfiguration config) {
			services.AddDbContext<LogsContext>(options => options.UseNpgsql(config.GetConnectionString("LogsContext"),
				o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)));

			services.AddScoped<IApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions>, DbApplicationRepository>();
			services.AddScoped<ILogMetadataRepository, DbLogMetadataRepository>();
			services.UseFileSystemCollectorLogStorage(config);
			services.AddSingleton<IMetricsManager, MetricsManager>();

			return services;
		}
	}
}
