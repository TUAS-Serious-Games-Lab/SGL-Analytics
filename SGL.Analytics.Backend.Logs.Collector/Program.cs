using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Utilities.Backend;
using SGL.Utilities.Backend.AspNetCore;
using SGL.Utilities.Logging.FileLogging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector {
	/// <summary>
	/// The main class for the logs collector service.
	/// </summary>
	public class Program {

		/// <summary>
		/// The entry point to the service executable.
		/// </summary>
		public static async Task Main(string[] args) {
			if (Environment.GetEnvironmentVariable("SGLA_MIGRATION_ONLY") != null) {
				await Console.Error.WriteLineAsync("Service started in environment marked as SGLA_MIGRATION_ONLY, shutting down ...");
				return;
			}
			IHost host = CreateHostBuilder(args).Build();
			await host.WaitForDbReadyAsync<LogsContext>(pollingInterval: TimeSpan.FromMilliseconds(500));
			await host.WaitForConfigValueSetAsync("Jwt:SymmetricKey", TimeSpan.FromMilliseconds(500));
			await host.RunAsync();
		}

		/// <summary>
		/// The factory method for the host builder for the logs collector service.
		/// </summary>
		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureAppConfiguration(config => {
					var tmpConf = config.Build();
					var keyDir = tmpConf.GetValue<string>("Jwt:KeyDirectory") ?? "./JWT-Key";
					config.AddKeyPerFile(keyDir, optional: true, reloadOnChange: true);
					var additionalConfFiles = new List<string>();
					tmpConf.GetSection("AdditionalConfigFiles").Bind(additionalConfFiles);
					foreach (var acf in additionalConfFiles) {
						Console.WriteLine($"Including additional config file {acf}");
						config.AddJsonFile(acf, optional: true, reloadOnChange: true);
					}
				})
				.ConfigureWebHostDefaults(webBuilder => {
					webBuilder.UseStartup<Startup>();
				})
				.ConfigureLogging(logging => logging.AddFile(builder => {
					builder.AddRequestScopePlaceholders();
					builder.AddUserIdScopePlaceholder();
					builder.AddAppNameScopePlaceholder();
				}));

	}
}
