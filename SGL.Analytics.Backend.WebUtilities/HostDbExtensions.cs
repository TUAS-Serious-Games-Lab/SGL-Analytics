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

namespace SGL.Analytics.Backend.WebUtilities {
	public static class HostDbExtensions {
		public static async Task WaitForDbsReadyAsync(this IHost host, TimeSpan pollingInterval, CancellationToken ct, params Type[] contextTypes) {
			using (var serviceScope = host.Services.CreateScope()) {
				using (var contexts = contextTypes.Select(type => serviceScope.ServiceProvider.GetRequiredService(type)).OfType<DbContext>().ToDisposableEnumerable()) {
					if (!(await Task.WhenAll(contexts.Select(context => context.Database.CanConnectAsync(ct)))).All(b => b)) {
						await Console.Out.WriteLineAsync("the database server is not yet available.");
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

		public static async Task WaitForDbReadyAsync<TContext>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default) where TContext : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext));
		}
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2));
		}
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3));
		}
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3, TContext4>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext where TContext4 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3), typeof(TContext4));
		}
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3, TContext4, TContext5>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext where TContext4 : DbContext where TContext5 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3), typeof(TContext4), typeof(TContext5));
		}
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3, TContext4, TContext5, TContext6>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext where TContext4 : DbContext where TContext5 : DbContext where TContext6 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3), typeof(TContext4), typeof(TContext5), typeof(TContext6));
		}
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3, TContext4, TContext5, TContext6, TContext7>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext where TContext4 : DbContext where TContext5 : DbContext
			where TContext6 : DbContext where TContext7 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3), typeof(TContext4), typeof(TContext5),
				typeof(TContext6), typeof(TContext7));
		}
		public static async Task WaitForDbsReadyAsync<TContext1, TContext2, TContext3, TContext4, TContext5, TContext6, TContext7, TContext8>(this IHost host, TimeSpan pollingInterval, CancellationToken ct = default)
			where TContext1 : DbContext where TContext2 : DbContext where TContext3 : DbContext where TContext4 : DbContext where TContext5 : DbContext
			where TContext6 : DbContext where TContext7 : DbContext where TContext8 : DbContext {
			await host.WaitForDbsReadyAsync(pollingInterval, ct, typeof(TContext1), typeof(TContext2), typeof(TContext3), typeof(TContext4), typeof(TContext5),
				typeof(TContext6), typeof(TContext7), typeof(TContext8));
		}
	}
}
