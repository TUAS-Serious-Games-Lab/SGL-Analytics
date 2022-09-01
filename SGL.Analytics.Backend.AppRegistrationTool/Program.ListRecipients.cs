using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Backend.Applications;
using System;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.AppRegistrationTool {
	public partial class Program {
		async static Task<int> ListRecipientsMain(ListRecipientsOptions opts) {
			using var host = CreateHostBuilder(opts, services => { }).Build();
			using var scope = host.Services.CreateScope();
			var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
			try {
				var usersApps = scope.ServiceProvider.GetRequiredService<IApplicationRepository<ApplicationWithUserProperties, Users.Application.Interfaces.ApplicationQueryOptions>>();
				var usersApp = await usersApps.GetApplicationByNameAsync(opts.AppName, new Users.Application.Interfaces.ApplicationQueryOptions { FetchRecipients = true });
				if (usersApp != null) {
					await Console.Out.WriteLineAsync("Recipients in UsersAPI:");
					await PrintRecipients(usersApp);
				}
				else {
					await Console.Out.WriteLineAsync("Not present in UsersAPI.");
				}
				await Console.Out.WriteLineAsync();
				var logsApps = scope.ServiceProvider.GetRequiredService<IApplicationRepository<Domain.Entity.Application, Logs.Application.Interfaces.ApplicationQueryOptions>>();
				var logsApp = await logsApps.GetApplicationByNameAsync(opts.AppName, new Logs.Application.Interfaces.ApplicationQueryOptions { FetchRecipients = true });
				if (logsApp != null) {
					await Console.Out.WriteLineAsync("Recipients in LogsAPI:");
					await PrintRecipients(logsApp);
				}
				else {
					await Console.Out.WriteLineAsync("Not present in LogsAPI.");
				}
				return 0;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Failed to list recipients.");
				return 2;
			}
		}

		private static async Task PrintRecipients(Application app) {
			await Console.Out.WriteLineAsync("\tPublic Key Id\t| Label\t| Subject\t| Issuer\t| Not Valid Before\t| Not Valid After\t| Serial Number");
			foreach (var r in app.DataRecipients) {
				try {
					var cert = r.Certificate;
					await Console.Out.WriteLineAsync($"\t{r.PublicKeyId}\t{r.Label}\t{cert.SubjectDN}\t{cert.IssuerDN}\t{cert.NotBefore}\t{cert.NotAfter}\t{Convert.ToHexString(cert.SerialNumber)}");
				}
				catch {
					await Console.Out.WriteLineAsync($"\t{r.PublicKeyId}\t{r.Label}\t[couldn't load certificate]");
				}
			}
		}

	}
}
