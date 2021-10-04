using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Utilities {
	public interface IScopedBackgroundService {
		Task ExecuteAsync(CancellationToken stoppingToken);
	}

	public class ScopingBackgroundService<TService> : BackgroundService where TService : IScopedBackgroundService {
		IServiceProvider services;

		public ScopingBackgroundService(IServiceProvider services) {
			this.services = services;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			using var scope = services.CreateScope();
			var service = scope.ServiceProvider.GetRequiredService<TService>();
			await service.ExecuteAsync(stoppingToken);
		}
	}

	public static class ScopedBackgroundServiceExtensions {
		public static IServiceCollection AddScopedBackgroundService<TService>(this IServiceCollection services) where TService : class, IScopedBackgroundService {
			services.AddScoped<TService>();
			services.AddHostedService<ScopingBackgroundService<TService>>();
			return services;
		}
	}
}
