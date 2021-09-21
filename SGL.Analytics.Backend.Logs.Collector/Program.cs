using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector {
	public class Program {
		public static async Task Main(string[] args) {
			IHost host = CreateHostBuilder(args).Build();
			using (var serviceScope = host.Services.CreateScope()) {
				using (var context = serviceScope.ServiceProvider.GetRequiredService<LogsContext>()) {
					if (!await context.Database.CanConnectAsync()) {
						Console.WriteLine("The database to be available for connection.");
						while (!await context.Database.CanConnectAsync()) {
							await Task.Delay(500);
						}
					}
					if ((await context.Database.GetPendingMigrationsAsync()).Any()) {
						Console.WriteLine("The database schema is not up-to-date. Please apply database migrations before starting the service.");
						Console.Write("Waiting for migrations to be applied.");
						while ((await context.Database.GetPendingMigrationsAsync()).Any()) {
							await Task.Delay(500);
						}
					}
				}
			}
			await host.RunAsync();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder => {
					webBuilder.UseStartup<Startup>();
				});
	}
}
