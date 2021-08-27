using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.LogCollector {
	public class Program {
		public static void Main(string[] args) {
			IHost host = CreateHostBuilder(args).Build();
			// TODO: Change this to use DB migrations when first real version of database schema is defined.
			using (var serviceScope = host.Services.CreateScope()) {
				var context = serviceScope.ServiceProvider.GetRequiredService<LogsContext>();
				context.Database.EnsureCreated();
			}
			host.Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder => {
					webBuilder.UseStartup<Startup>();
				});
	}
}
