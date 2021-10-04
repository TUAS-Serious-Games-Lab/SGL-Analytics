using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure {
	public static class ServiceCollectionExtensions {
		public static IServiceCollection UseLogsBackendInfrastructure(this IServiceCollection services, IConfiguration config) {
			services.AddDbContext<LogsContext>(options => options.UseNpgsql(config.GetConnectionString("LogsContext")));

			services.AddScoped<IApplicationRepository, DbApplicationRepository>();
			services.AddScoped<ILogMetadataRepository, DbLogMetadataRepository>();
			services.UseFileSystemCollectorLogStorage(config);

			return services;
		}
	}
}
