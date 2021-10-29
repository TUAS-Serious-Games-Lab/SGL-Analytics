using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Utilities {

	/// <summary>
	/// The interface required for background service classes that are to be used with <see cref="ScopingBackgroundService{TService}"/> and <see cref="ScopedBackgroundServiceExtensions.AddScopedBackgroundService{TService}(IServiceCollection)"/>.
	/// </summary>
	public interface IScopedBackgroundService {
		/// <summary>
		/// Invokes the asynchronous execution of the service's operation.
		/// </summary>
		/// <param name="stoppingToken">A cancellation token that is triggered when the application host is performing a graceful shutdown.</param>
		/// <returns>A task that completes when the operation is completed and is cancelled if the operation is cancelled.</returns>
		Task ExecuteAsync(CancellationToken stoppingToken);
	}

	/// <summary>
	/// Allows hosting a background service, implemented by the given class, that utilizes scoped services from the dependency injection container.
	/// This class simply creates a single scope, gets the given service type from it and then invokes <see cref="IScopedBackgroundService.ExecuteAsync(CancellationToken)"/> on it.
	/// </summary>
	/// <typeparam name="TService">The type of the service object to retrieve.</typeparam>
	/// <remarks>
	/// This helper class is needed as <see cref="BackgroundService"/> and <see cref="IHostedService"/> don't support this use case directly
	/// because they are added as singletons and lifetime rules of the DI container forbid singleton services from accessing scoped services.
	/// </remarks>
	public class ScopingBackgroundService<TService> : BackgroundService where TService : IScopedBackgroundService {
		IServiceProvider services;

		/// <summary>
		/// Instantieates the <see cref="ScopingBackgroundService{TService}"/> using the given dependency injection service provider.
		/// This is usually also invoked by the DI container.
		/// </summary>
		/// <param name="services">The service provider to use for creating the actual service.</param>
		public ScopingBackgroundService(IServiceProvider services) {
			this.services = services;
		}

		/// <summary>
		/// Creates a scope for the service to instantiate it, obtains it from the DI container and asynchronously invokes <see cref="IScopedBackgroundService.ExecuteAsync(CancellationToken)"/> on it.
		/// </summary>
		/// <param name="stoppingToken">A cancellation token that is triggered when the application host is performing a graceful shutdown.</param>
		/// <returns>A task that completes when the operation is completed and is cancelled if the operation is cancelled.</returns>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
			using var scope = services.CreateScope();
			var service = scope.ServiceProvider.GetRequiredService<TService>();
			await service.ExecuteAsync(stoppingToken);
		}
	}

	/// <summary>
	/// Provides the <see cref="AddScopedBackgroundService{TService}(IServiceCollection)"/> extension method.
	/// </summary>
	public static class ScopedBackgroundServiceExtensions {
		/// <summary>
		/// Adds a scoped background service of the given type to the service collection.
		/// The advantage of this over <see cref="ServiceCollectionHostedServiceExtensions.AddHostedService{THostedService}(IServiceCollection)"/> is that the scoped background services
		/// can access other scoped services through dependency injection because they are also hosted in a DI scope.
		/// </summary>
		/// <typeparam name="TService">The class implementing the service.</typeparam>
		/// <param name="services">The service collection to add the service to.</param>
		/// <returns>A reference to <c>services</c> for chaining.</returns>
		public static IServiceCollection AddScopedBackgroundService<TService>(this IServiceCollection services) where TService : class, IScopedBackgroundService {
			services.AddScoped<TService>();
			services.AddHostedService<ScopingBackgroundService<TService>>();
			return services;
		}
	}
}
