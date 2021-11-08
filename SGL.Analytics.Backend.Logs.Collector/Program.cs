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
using SGL.Utilities.Logging.FileLogging;
using SGL.Utilities.Backend.AspNetCore;
using SGL.Utilities.Backend;

namespace SGL.Analytics.Backend.Logs.Collector {
	/// <summary>
	/// The main class for the logs collector service.
	/// </summary>
	public class Program {

		/// <summary>
		/// The entry point to the service executable.
		/// </summary>
		public static async Task Main(string[] args) {
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
