using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Backend.Applications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.AppRegistrationTool {

	/// <summary>
	/// Provides functionality to manage application registrations in the databases of both, the logs collector service database and the user registration service database.
	/// </summary>
	public class AppRegistrationManager {
		private IApplicationRepository<Domain.Entity.Application, Logs.Application.Interfaces.ApplicationQueryOptions> logsAppRepo;
		private IApplicationRepository<ApplicationWithUserProperties, Users.Application.Interfaces.ApplicationQueryOptions> usersAppRepo;
		private ILogger<AppRegistrationManager> logger;

		/// <summary>
		/// Constructs a application registration manager operating on the two given application repository, one for each service's database.
		/// </summary>
		/// <param name="logsAppRepo">The application repository object for the logs collector service.</param>
		/// <param name="usersAppRepo">The application repository object for the user registration service.</param>
		/// <param name="logger">A logger to log diagnostig messages to.</param>
		public AppRegistrationManager(
			IApplicationRepository<Domain.Entity.Application, Logs.Application.Interfaces.ApplicationQueryOptions> logsAppRepo,
			IApplicationRepository<ApplicationWithUserProperties, Users.Application.Interfaces.ApplicationQueryOptions> usersAppRepo,
			ILogger<AppRegistrationManager> logger) {
			this.logsAppRepo = logsAppRepo;
			this.usersAppRepo = usersAppRepo;
			this.logger = logger;
		}

		/// <summary>
		/// Represents the result of an operation of <see cref="PushApplicationAsync(ApplicationWithUserProperties, CancellationToken)"/> for each of the databases.
		/// </summary>
		public enum PushResult {
			/// <summary>
			/// The application was already present and either already has the expected state and thus was not changed, or was not changed because the change was prevented as a safety measure.
			/// </summary>
			Unchanged,
			/// <summary>
			/// The application was not yet present and was added to the database.
			/// </summary>
			Added,
			/// <summary>
			/// The application was already present but had a different state, thus the state in the database was updated.
			/// </summary>
			Updated,
			/// <summary>
			/// An error prevented pushing the application definition.
			/// </summary>
			Error
		}

		/// <summary>
		/// Asynchronously pushes the given application definition into the service databases.
		/// </summary>
		/// <param name="application">The application definition to push.</param>
		/// <param name="ct">A cancellation token allowing the cancellation of the operation.</param>
		/// <returns>A task object representing the operation, providing an enumerable with one <see cref="PushResult"/> for each target database.</returns>
		public async Task<IEnumerable<PushResult>> PushApplicationAsync(ApplicationWithUserProperties application, CancellationToken ct = default) {
			return await Task.WhenAll(PushLogsApplication(application, ct), PushUsersApplication(application, ct));
		}

		/// <summary>
		/// Update recipient list in <paramref name="oldApplication"/> to fit that from <paramref name="newApplication"/>.
		/// </summary>
		/// <returns>True if anything was changed, false otherwise.</returns>
		public bool PushRecipients(string apiName, Domain.Entity.Application newApplication, Domain.Entity.Application oldApplication) {
			var appName = oldApplication.Name;
			var missingRecipients = oldApplication.DataRecipients.Where(r1 => !newApplication.DataRecipients.Any(r2 => r1.PublicKeyId == r2.PublicKeyId)).ToList();
			var addedRecipients = newApplication.DataRecipients.Where(r1 => !oldApplication.DataRecipients.Any(r2 => r1.PublicKeyId == r2.PublicKeyId)).ToList();
			var commonRecipients = oldApplication.DataRecipients.Join(newApplication.DataRecipients, r => r.PublicKeyId, r => r.PublicKeyId, (or, nr) => (Old: or, New: nr));
			var changedRecipients = commonRecipients.Where(pair => pair.Old.Label != pair.New.Label || pair.Old.CertificatePem != pair.New.CertificatePem).ToList();
			bool changed = false;
			if (missingRecipients.Any()) {
				foreach (var mr in missingRecipients) {
					logger.LogWarning("Application {appName} is already registered in {apiName} with a data recipient list containing the key {keyid} (labeled as \"{label}\"), " +
						"which is no longer present in the current application definition. The key will however not be automatically removed to prevent data loss." +
						"To remove a no longer needed key, use the remove-recipient command verb of the app registration tool.",
						appName, apiName, mr.PublicKeyId, mr.Label);
				}
			}
			if (addedRecipients.Any()) {
				changed = true;
				foreach (var ar in addedRecipients) {
					logger.LogInformation("Adding new data recipient {keyId} (with label \"{label}\") to application {appName} in {apiName}.", ar.PublicKeyId, ar.Label, appName, apiName);
					oldApplication.AddRecipient(ar.Label, ar.CertificatePem);
				}
			}
			if (changedRecipients.Any()) {
				changed = true;
				foreach (var pair in changedRecipients) {
					if (pair.Old.Label != pair.New.Label) {
						logger.LogInformation("Changing label for recipient {keyid} in application {appName} in {apiName} from \"{old}\" to \"{new}\".",
							pair.Old.PublicKeyId, appName, apiName, pair.Old.Label, pair.New.Label);
						pair.Old.Label = pair.New.Label;
					}
					if (pair.Old.CertificatePem != pair.New.CertificatePem) {
						logger.LogInformation("Updating certificate PEM for recipient {keyid} in application {appName} in {apiName}.", pair.Old.PublicKeyId, appName, apiName);
						pair.Old.CertificatePem = pair.New.CertificatePem;
					}
				}
			}
			return changed;
		}

		/// <summary>
		/// Update exporter certificate list in <paramref name="oldApplication"/> to fit that from <paramref name="newApplication"/>.
		/// </summary>
		/// <returns>True if anything was changed, false otherwise.</returns>
		public bool PushExporterCerts(string apiName, ApplicationWithUserProperties newApplication, ApplicationWithUserProperties oldApplication) {
			var appName = oldApplication.Name;
			var missingExporters = oldApplication.AuthorizedExporters.Where(r1 => !newApplication.AuthorizedExporters.Any(r2 => r1.PublicKeyId == r2.PublicKeyId)).ToList();
			var addedExporters = newApplication.AuthorizedExporters.Where(r1 => !oldApplication.AuthorizedExporters.Any(r2 => r1.PublicKeyId == r2.PublicKeyId)).ToList();
			var commonExporters = oldApplication.AuthorizedExporters.Join(newApplication.AuthorizedExporters, r => r.PublicKeyId, r => r.PublicKeyId, (or, nr) => (Old: or, New: nr));
			var changedExporters = commonExporters.Where(pair => pair.Old.Label != pair.New.Label || pair.Old.CertificatePem != pair.New.CertificatePem).ToList();
			bool changed = false;
			if (missingExporters.Any()) {
				foreach (var mexp in missingExporters) {
					logger.LogInformation("Removing data exporter {keyId} (with label \"{label}\") from application {appName} in {apiName}.", mexp.PublicKeyId, mexp.Label, appName, apiName);
					oldApplication.AuthorizedExporters.Remove(mexp);
				}
			}
			if (addedExporters.Any()) {
				changed = true;
				foreach (var ae in addedExporters) {
					logger.LogInformation("Adding new data exporter {keyId} (with label \"{label}\") to application {appName} in {apiName}.", ae.PublicKeyId, ae.Label, appName, apiName);
					oldApplication.AddAuthorizedExporter(ae.Label, ae.CertificatePem);
				}
			}
			if (changedExporters.Any()) {
				changed = true;
				foreach (var pair in changedExporters) {
					if (pair.Old.Label != pair.New.Label) {
						logger.LogInformation("Changing label for exporter {keyid} in application {appName} in {apiName} from \"{old}\" to \"{new}\".",
							pair.Old.PublicKeyId, appName, apiName, pair.Old.Label, pair.New.Label);
						pair.Old.Label = pair.New.Label;
					}
					if (pair.Old.CertificatePem != pair.New.CertificatePem) {
						logger.LogInformation("Updating certificate PEM for exporter {keyid} in application {appName} in {apiName}.", pair.Old.PublicKeyId, appName, apiName);
						pair.Old.CertificatePem = pair.New.CertificatePem;
					}
				}
			}
			return changed;
		}

		private async Task<PushResult> PushLogsApplication(Domain.Entity.Application application, CancellationToken ct = default) {
			var queryOpts = new Logs.Application.Interfaces.ApplicationQueryOptions { FetchRecipients = true };
			try {
				var existingApp = await logsAppRepo.GetApplicationByNameAsync(application.Name, queryOpts, ct: ct);
				if (existingApp != null) {
					bool changed = false;
					if (existingApp.ApiToken != application.ApiToken) {
						logger.LogInformation("Application {appName} is already registered in LogsAPI, but with a different API token. Updating the token ...", application.Name);
						existingApp.ApiToken = application.ApiToken;
						changed = true;
					}
					if (PushRecipients("LogsAPI", application, existingApp)) {
						changed = true;
					}
					if (changed) {
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
				var queryOpts = new Users.Application.Interfaces.ApplicationQueryOptions { FetchRecipients = true, FetchUserProperties = true, FetchExporterCertificates = true };
				var existingApp = await usersAppRepo.GetApplicationByNameAsync(application.Name, queryOpts, ct: ct);
				if (existingApp != null) {
					bool changed = false;
					if (existingApp.ApiToken != application.ApiToken) {
						changed = true;
						logger.LogInformation("Application {appName} is already registered in UsersAPI, but with a different API token. Updating the token ...", application.Name);
						existingApp.ApiToken = application.ApiToken;
					}
					if (existingApp.BasicFederationUpstreamAuthUrl != application.BasicFederationUpstreamAuthUrl) {
						changed = true;
						logger.LogInformation("Application {appName} is already registered in UsersAPI, but with a different basic-federation upstream URL. Updating the URL from {old} to {new} ...", application.Name, existingApp.BasicFederationUpstreamAuthUrl, application.BasicFederationUpstreamAuthUrl);
						existingApp.BasicFederationUpstreamAuthUrl = application.BasicFederationUpstreamAuthUrl;
					}
					if (PushRecipients("UsersAPI", application, existingApp)) {
						changed = true;
					}
					if (PushExporterCerts("UsersAPI", application, existingApp)) {
						changed = true;
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
							exProp!.Required = false;
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
