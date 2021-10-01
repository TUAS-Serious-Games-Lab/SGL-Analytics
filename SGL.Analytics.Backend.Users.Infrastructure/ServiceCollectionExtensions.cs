using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.Backend.Users.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Infrastructure {
	public static class ServiceCollectionExtensions {
		public static IServiceCollection UseUsersBackendInfrastructure(this IServiceCollection services, IConfiguration config) {
			services.AddDbContext<UsersContext>(options => options.UseNpgsql(config.GetConnectionString("UsersContext")));

			services.AddScoped<IApplicationRepository, DbApplicationRepository>();
			services.AddScoped<IUserRepository, DbUserRepository>();

			return services;
		}
	}
}
