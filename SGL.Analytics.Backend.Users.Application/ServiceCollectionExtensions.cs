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
	public static class ServiceCollectionExtensions {
		public static IServiceCollection UseUsersBackendAppplicationLayer(this IServiceCollection services, IConfiguration config) {
			services.AddSingleton<IKeyAuthChallengeStateHolder, KeyAuthChallengeStateHolder>();
			services.AddScoped<IUserManager, UserManager>();
			return services;
		}
	}
}
