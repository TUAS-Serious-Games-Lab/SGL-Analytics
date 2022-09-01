using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.AppRegistrationTool {
	public partial class Program {
		async static Task<int> ApplyMigrationsMain(ApplyMigrationsOptions opts) {
			using var host = CreateHostBuilder(opts, services => { }).Build();
			using var scope = host.Services.CreateScope();
			var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
			try {
				logger.LogInformation("Applying migrations for {ctx}...", nameof(UsersContext));
				await scope.ServiceProvider.GetRequiredService<UsersContext>().Database.MigrateAsync();
				logger.LogInformation("Applying migrations for {ctx}...", nameof(LogsContext));
				await scope.ServiceProvider.GetRequiredService<LogsContext>().Database.MigrateAsync();
				logger.LogInformation("... done!");
				return 0;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Applying migrations failed.");
				return 2;
			}
		}
	}
}
