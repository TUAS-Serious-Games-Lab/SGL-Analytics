using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.AppRegistrationTool {
	public class AppRegistrationManager {
		private Logs.Application.Interfaces.IApplicationRepository logsAppRepo;
		private Users.Application.Interfaces.IApplicationRepository usersAppRepo;
		private ILogger<AppRegistrationManager> logger;

		public AppRegistrationManager(
			Logs.Application.Interfaces.IApplicationRepository logsAppRepo,
			Users.Application.Interfaces.IApplicationRepository usersAppRepo,
			ILogger<AppRegistrationManager> logger) {
			this.logsAppRepo = logsAppRepo;
			this.usersAppRepo = usersAppRepo;
			this.logger = logger;
		}

		public enum PushResult {
			Unchanged, Added, Updated, Error
		}

		public async Task<IEnumerable<PushResult>> PushApplicationAsync(ApplicationWithUserProperties application, CancellationToken ct = default) {
			return await Task.WhenAll(PushLogsApplication(application, ct), PushUsersApplication(application, ct));
		}

		private async Task<PushResult> PushLogsApplication(Application application, CancellationToken ct = default) {
			try {
				var existingApp = await logsAppRepo.GetApplicationByNameAsync(application.Name, ct);
				if (existingApp != null) {
					if (existingApp.ApiToken != application.ApiToken) {
						logger.LogInformation("Application {appName} is already registered in LogsAPI, but with a different API token. Updating the token ...", application.Name);
						existingApp.ApiToken = application.ApiToken;
						await logsAppRepo.UpdateApplicationAsync(existingApp, ct);
						return PushResult.Updated;
					}
					else {
						logger.LogInformation("Application {appName} is already registered in LogsAPI. Skipping its registration ...", application.Name);
						return PushResult.Unchanged;
					}
				}
				else {
					logger.LogInformation("Registering application {appName} in LogsAPI ...", application.Name);
					await logsAppRepo.AddApplicationAsync(application, ct);
					return PushResult.Added;
				}
			}
			catch (Exception ex) {
				logger.LogError(ex, "Error while pushing registration for application {appName} into LogsAPI.", application.Name);
				return PushResult.Error;
			}
		}
		private async Task<PushResult> PushUsersApplication(ApplicationWithUserProperties application, CancellationToken ct = default) {
			try {
				var existingApp = await usersAppRepo.GetApplicationByNameAsync(application.Name, ct);
				if (existingApp != null) {
					bool changed = false;
					if (existingApp.ApiToken != application.ApiToken) {
						changed = true;
						logger.LogInformation("Application {appName} is already registered in UsersAPI, but with a different API token. Updating the token ...", application.Name);
						existingApp.ApiToken = application.ApiToken;
					}
					var newProperties = application.UserProperties.Where(prop => existingApp.UserProperties.Count(exProp => exProp.Name == prop.Name) == 0);
					if (newProperties.Any()) {
						changed = true;
						logger.LogInformation("Application {appName} is already registered in UsersAPI, but has gained the user properties [{newProps}]. Adding these properties ...",
							application.Name, string.Join(", ", newProperties.Select(p => p.Name)));
						foreach (var prop in newProperties) {
							existingApp.AddProperty(prop.Name, prop.Type, prop.Required);
						}
					}
					var missingProperties = existingApp.UserProperties.Where(exProp => application.UserProperties.Count(prop => exProp.Name == prop.Name) == 0);
					if (missingProperties.Any()) {
						logger.LogWarning("Application {appName} is already registered in UsersAPI, but the current definition is missing the user properties [{missingProps}] " +
							"that are present in the existing registration. The push operation however only adds properties for safety reasons. Therefore these properties will remain present.",
							application.Name, string.Join(", ", missingProperties.Select(p => p.Name)));
					}
					var correspondingProperties = application.UserProperties.Select(prop => {
						var exProp = existingApp.UserProperties.FirstOrDefault(exProp => exProp.Name == prop.Name);
						return (prop, exProp);
					}).Where(propPair => propPair.exProp != null);
					var propsWithTypeChanges = correspondingProperties.Where(propPair => propPair.prop.Type != propPair.exProp!.Type);
					if (propsWithTypeChanges.Any()) {
						logger.LogWarning("Application {appName} is already registered in UsersAPI, but the user properties [{propsWithTypeChanges}] have a different type. " +
							"Changing the type is not supported, because this would break existing instances. Therefore these properties will keep their current types.",
							application.Name, string.Join(", ", propsWithTypeChanges.Select(p => p.prop.Name)));
					}
					var newlyRequiredProps = correspondingProperties.Where(propPair => propPair.prop.Required && !(propPair.exProp!.Required));
					if (newlyRequiredProps.Any()) {
						logger.LogWarning("Application {appName} is already registered in UsersAPI, but the user properties [{newlyRequiredProps}] were previously optional but are now required. " +
							"Making option properties required is not supported, because this would break existing instances. Therefore these properties will keep their optional status.",
							application.Name, string.Join(", ", newlyRequiredProps.Select(p => p.prop.Name)));
					}
					var newlyOptionalProps = correspondingProperties.Where(propPair => !(propPair.prop.Required) && propPair.exProp!.Required);
					if (newlyOptionalProps.Any()) {
						changed = true;
						logger.LogInformation("Application {appName} is already registered in UsersAPI, but the user properties [{newlyOptionalProps}] were previously optional but are now required. " +
							"Changing them to optional ...", application.Name, string.Join(", ", newlyOptionalProps.Select(p => p.prop.Name)));
						foreach (var (prop, exProp) in newlyOptionalProps) {
							exProp.Required = false;
						}
					}
					if (changed) {
						await usersAppRepo.UpdateApplicationAsync(existingApp, ct);
						return PushResult.Updated;
					}
					else {
						logger.LogInformation("Application {appName} is already registered in UsersAPI. Skipping its registration ...", application.Name);
						return PushResult.Unchanged;
					}
				}
				else {
					logger.LogInformation("Registering application {appName} in UsersAPI ...", application.Name);
					await usersAppRepo.AddApplicationAsync(application, ct);
					return PushResult.Added;
				}
			}
			catch (Exception ex) {
				logger.LogError(ex, "Error while pushing registration for application {appName} into UsersAPI.", application.Name);
				return PushResult.Error;
			}
		}
	}
}
