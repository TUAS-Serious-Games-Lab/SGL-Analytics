using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SGL.Analytics.Utilities.Logging.FileLogging;
using SGL.Analytics.Backend.WebUtilities;
using SGL.Analytics.Backend.Utilities;

namespace SGL.Analytics.Backend.Users.Registration {
	public class Program {
		public static async Task Main(string[] args) {
			IHost host = CreateHostBuilder(args).Build();
			await host.WaitForDbReadyAsync<UsersContext>(pollingInterval: TimeSpan.FromMilliseconds(500));
			await host.RunAsync();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder => {
					webBuilder.UseStartup<Startup>();
				})
				.ConfigureLogging(logging => logging.AddFile(builder => {
					builder.AddRequestScopePlaceholders();
					builder.AddUserIdScopePlaceholder();
				}));
	}
}
