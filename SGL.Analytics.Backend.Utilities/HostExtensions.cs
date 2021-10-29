using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SGL.Analytics.Utilities;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace SGL.Analytics.Backend.Utilities {
	/// <summary>
	/// Provides extension methods for <see cref="IHost"/> to help with application startup.
	/// </summary>
	public static class HostExtensions {

		/// <summary>
		/// Asynchronously waits for a config value with a given key to become available by polling.
		/// This method is intended for cases where a value, e.g. a generated secret, is expected to be provided externally at startup by some other process that must complete before the application can be started.
		/// The value can e.g. be brought into the config system using a file with reloading active that is updated by the other process.
		/// </summary>
		/// <param name="host">The host of which the configuration is polled.</param>
		/// <param name="key">The configuration key to poll.</param>
		/// <param name="pollingInterval">The time between polls.</param>
		/// <param name="ct">A cancellation token to allow the wating and polling operation to be cancelled.</param>
		/// <returns></returns>
		public static async Task WaitForConfigValueSetAsync(this IHost host, string key, TimeSpan pollingInterval, CancellationToken ct = default) {
			var conf = host.Services.GetRequiredService<IConfiguration>();
			if (string.IsNullOrWhiteSpace(conf.GetValue<string>(key))) {
				await Console.Out.WriteLineAsync($"The required config value '{key}' is not yet set.");
				await Console.Out.WriteAsync("Waiting for it to be set through config reloading.");
				while (string.IsNullOrWhiteSpace(conf.GetValue<string>(key))) {
					await Task.Delay(pollingInterval, ct);
					await Console.Out.WriteAsync(".");
				}
				await Console.Out.WriteLineAsync();
			}
		}

		/// <summary>
		/// Asynchronously waits for database contexts of all given types to have their database available and on the expected migration version by polling
		/// <see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CanConnectAsync(CancellationToken)"/> and
		/// <see cref="RelationalDatabaseFacadeExtensions.GetPendingMigrationsAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade, CancellationToken)"/>.
		/// This is intended to wait for the database server / container to be up and ready and for a migrations script to run on deployment.
		/// </summary>
		/// <param name="host">The host in which the database contexts are to be hosted.</param>
		/// <param name="pollingInterval">The time between polls.</param>
		/// <param name="ct">A cancellation token to allow the wating and polling operation to be cancelled.</param>
		/// <param name="contextTypes">The types of the database context classes.</param>
		/// <returns>A task that becomes ready when all databases are available.</returns>
		public static async Task WaitForDbsReadyAsync(this IHost host, TimeSpan pollingInterval, CancellationToken ct, params Type[] contextTypes) {
			using (var serviceScope = host.Services.CreateScope()) {
				using (var contexts = contextTypes.Select(type => serviceScope.ServiceProvider.GetRequiredService(type)).OfType<DbContext>().ToDisposableEnumerable()) {
					if (!(await Task.WhenAll(contexts.Select(context => context.Database.CanConnectAsync(ct)))).All(b => b)) {
						await Console.Out.WriteLineAsync("The database server is not yet available.");
						await Console.Out.WriteAsync("Waiting for the database to be available for connection.");
						while (!(await Task.WhenAll(contexts.Select(context => context.Database.CanConnectAsync(ct)))).All(b => b)) {
							await Task.Delay(pollingInterval, ct);
							await Console.Out.WriteAsync(".");
						}
						await Console.Out.WriteLineAsync();
					}
					var pendingMigrations = await GetPendingMigrations(contexts, ct);
					if (pendingMigrations.Any()) {
						Console.WriteLine("The database schema is not up-to-date. Please apply database migrations before starting the program.");
						await Console.Out.WriteLineAsync("The following migrations are pending:");
						foreach (var (context, migrations) in pendingMigrations) {
							await Console.Out.WriteLineAsync($"  For context {context}:");
							foreach (var migration in migrations) {
								await Console.Out.WriteLineAsync($"    {migration}");
							}
						}
						Console.Write("Waiting for migrations to be applied.");
						while ((await GetPendingMigrations(contexts, ct)).Any()) {
							await Task.Delay(pollingInterval, ct);
							await Console.Out.WriteAsync(".");
						}
						await Console.Out.WriteLineAsync();
					}
				}
			}
		}

		private static async Task<IEnumerable<(string Name, IEnumerable<string>)>> GetPendingMigrations(DisposableEnumerable<DbContext> contexts, CancellationToken ct = default) {
			return (await Task.WhenAll(contexts.Select(async context => (context.GetType().Name, await context.Database.GetPendingMigrationsAsync(ct))))).Where(cm => cm.Item2.Any());
		}

		/// <summary>
		/// Asynchronously waits for a database context of the given type to have its database available and on the expected migration version.
		/// Acts as a convenience wrapper around <see cref="WaitForDbsReadyAsync(IHost, TimeSpan, CancellationToken, Type[])"/>.
		/// </summary>
		/// <typeparam name="TContext">The context class to wait for.</typeparam>
		public static async Task WaitForDbReadyAsync<TContext>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default) where TContext : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext));
		}
		/// <summary>
		/// Asynchronously waits for database contexts of the given types to have their database available and on the expected migration version.
		/// Acts as a convenience wrapper around <see cref="WaitForDbsReadyAsync(IHost, TimeSpan, CancellationToken, Type[])"/>.
		/// </summary>
		/// <typeparam name="TContext1">The first context class to wait for.</typeparam>
		/// <typeparam name="TContext2">The second context class to wait for.</typeparam>
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2));
		}
		/// <summary>
		/// Asynchronously waits for database contexts of the given types to have their database available and on the expected migration version.
		/// Acts as a convenience wrapper around <see cref="WaitForDbsReadyAsync(IHost, TimeSpan, CancellationToken, Type[])"/>.
		/// </summary>
		/// <typeparam name="TContext1">The first context class to wait for.</typeparam>
		/// <typeparam name="TContext2">The second context class to wait for.</typeparam>
		/// <typeparam name="TContext3">The third context class to wait for.</typeparam>
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3));
		}
		/// <summary>
		/// Asynchronously waits for database contexts of the given types to have their database available and on the expected migration version.
		/// Acts as a convenience wrapper around <see cref="WaitForDbsReadyAsync(IHost, TimeSpan, CancellationToken, Type[])"/>.
		/// </summary>
		/// <typeparam name="TContext1">The first context class to wait for.</typeparam>
		/// <typeparam name="TContext2">The second context class to wait for.</typeparam>
		/// <typeparam name="TContext3">The third context class to wait for.</typeparam>
		/// <typeparam name="TContext4">The fourth context class to wait for.</typeparam>
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3, TContext4>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext where TContext4 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3), typeof(TContext4));
		}
		/// <summary>
		/// Asynchronously waits for database contexts of the given types to have their database available and on the expected migration version.
		/// Acts as a convenience wrapper around <see cref="WaitForDbsReadyAsync(IHost, TimeSpan, CancellationToken, Type[])"/>.
		/// </summary>
		/// <typeparam name="TContext1">The first context class to wait for.</typeparam>
		/// <typeparam name="TContext2">The second context class to wait for.</typeparam>
		/// <typeparam name="TContext3">The third context class to wait for.</typeparam>
		/// <typeparam name="TContext4">The fourth context class to wait for.</typeparam>
		/// <typeparam name="TContext5">The fifth context class to wait for.</typeparam>
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3, TContext4, TContext5>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext where TContext4 : DbContext where TContext5 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3), typeof(TContext4), typeof(TContext5));
		}
		/// <summary>
		/// Asynchronously waits for database contexts of the given types to have their database available and on the expected migration version.
		/// Acts as a convenience wrapper around <see cref="WaitForDbsReadyAsync(IHost, TimeSpan, CancellationToken, Type[])"/>.
		/// </summary>
		/// <typeparam name="TContext1">The first context class to wait for.</typeparam>
		/// <typeparam name="TContext2">The second context class to wait for.</typeparam>
		/// <typeparam name="TContext3">The third context class to wait for.</typeparam>
		/// <typeparam name="TContext4">The fourth context class to wait for.</typeparam>
		/// <typeparam name="TContext5">The fifth context class to wait for.</typeparam>
		/// <typeparam name="TContext6">The sixth context class to wait for.</typeparam>
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3, TContext4, TContext5, TContext6>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext where TContext4 : DbContext where TContext5 : DbContext where TContext6 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3), typeof(TContext4), typeof(TContext5), typeof(TContext6));
		}
		/// <summary>
		/// Asynchronously waits for database contexts of the given types to have their database available and on the expected migration version.
		/// Acts as a convenience wrapper around <see cref="WaitForDbsReadyAsync(IHost, TimeSpan, CancellationToken, Type[])"/>.
		/// </summary>
		/// <typeparam name="TContext1">The first context class to wait for.</typeparam>
		/// <typeparam name="TContext2">The second context class to wait for.</typeparam>
		/// <typeparam name="TContext3">The third context class to wait for.</typeparam>
		/// <typeparam name="TContext4">The fourth context class to wait for.</typeparam>
		/// <typeparam name="TContext5">The fifth context class to wait for.</typeparam>
		/// <typeparam name="TContext6">The sixth context class to wait for.</typeparam>
		/// <typeparam name="TContext7">The seventh context class to wait for.</typeparam>
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3, TContext4, TContext5, TContext6, TContext7>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext where TContext4 : DbContext where TContext5 : DbContext
			where TContext6 : DbContext where TContext7 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3), typeof(TContext4), typeof(TContext5),
				typeof(TContext6), typeof(TContext7));
		}
		/// <summary>
		/// Asynchronously waits for database contexts of the given types to have their database available and on the expected migration version.
		/// Acts as a convenience wrapper around <see cref="WaitForDbsReadyAsync(IHost, TimeSpan, CancellationToken, Type[])"/>.
		/// </summary>
		/// <typeparam name="TContext1">The first context class to wait for.</typeparam>
		/// <typeparam name="TContext2">The second context class to wait for.</typeparam>
		/// <typeparam name="TContext3">The third context class to wait for.</typeparam>
		/// <typeparam name="TContext4">The fourth context class to wait for.</typeparam>
		/// <typeparam name="TContext5">The fifth context class to wait for.</typeparam>
		/// <typeparam name="TContext6">The sixth context class to wait for.</typeparam>
		/// <typeparam name="TContext7">The seventh context class to wait for.</typeparam>
		/// <typeparam name="TContext8">The eigth context class to wait for.</typeparam>
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3, TContext4, TContext5, TContext6, TContext7, TContext8>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext where TContext4 : DbContext where TContext5 : DbContext
			where TContext6 : DbContext where TContext7 : DbContext where TContext8 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3), typeof(TContext4), typeof(TContext5),
				typeof(TContext6), typeof(TContext7), typeof(TContext8));
		}
	}
}
