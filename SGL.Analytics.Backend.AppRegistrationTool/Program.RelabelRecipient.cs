using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.AppRegistrationTool {
	public partial class Program {
		async static Task<int> RelabelRecipientMain(RelabelRecipientOptions opts) {
			using var host = CreateHostBuilder(opts, services => { }).Build();
			using var scope = host.Services.CreateScope();
			var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
			try {
				var keyId = KeyId.Parse(opts.KeyId);
				var usersApps = scope.ServiceProvider.GetRequiredService<IApplicationRepository<ApplicationWithUserProperties, Users.Application.Interfaces.ApplicationQueryOptions>>();
				var usersApp = await usersApps.GetApplicationByNameAsync(opts.AppName, new Users.Application.Interfaces.ApplicationQueryOptions { FetchRecipients = true });
				if (RelabelRecipient("UsersAPI", opts.AppName, logger, keyId, usersApp, opts.Label)) {
					await usersApps.UpdateApplicationAsync(usersApp!);
				}
				var logsApps = scope.ServiceProvider.GetRequiredService<IApplicationRepository<Domain.Entity.Application, Logs.Application.Interfaces.ApplicationQueryOptions>>();
				var logsApp = await logsApps.GetApplicationByNameAsync(opts.AppName, new Logs.Application.Interfaces.ApplicationQueryOptions { FetchRecipients = true });
				if (RelabelRecipient("LogsAPI", opts.AppName, logger, keyId, logsApp, opts.Label)) {
					await logsApps.UpdateApplicationAsync(logsApp!);
				}
				return 0;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Failed to relabel recipient.");
				return 2;
			}
		}

		private static bool RelabelRecipient(string apiName, string appName, ILogger<Program> logger, KeyId keyId, Application? app, string label) {
			if (app != null) {
				var recipient = app.DataRecipients.SingleOrDefault(r => r.PublicKeyId == keyId);
				if (recipient != null) {
					logger.LogInformation("Changing label of {keyId} from application {appName} in {apiName} to \"{label}\"...", recipient.PublicKeyId, appName, apiName, label);
					recipient.Label = label;
					return true;
				}
				else {
					logger.LogWarning("Recipient is not present in application {appName} in {apiName}.", appName, apiName);
				}
			}
			else {
				logger.LogWarning("Application {appName} is not present in {apiName}.", appName, apiName);
			}
			return false;
		}
	}
}
