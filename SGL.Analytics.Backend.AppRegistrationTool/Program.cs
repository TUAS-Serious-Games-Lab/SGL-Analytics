using CommandLine;
using CommandLine.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Infrastructure;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Users.Infrastructure;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Utilities;
using SGL.Utilities.Backend;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.AppRegistrationTool {
	/// <summary>
	/// The main class for the AppRegistrationTool program.
	/// </summary>
	public class Program {
		/// <summary>
		/// Represents the common command-line options for all verbs supported by the tool.
		/// </summary>
		public class BaseOptions {
			/// <summary>
			/// Specifies the additional config files to use.
			/// </summary>
			[Option('c', "config", HelpText = "Specifies an additional config file to use.")]
			public IEnumerable<string> ConfigFiles { get; set; } = new List<string>();
		}
		/// <summary>
		/// Represents the command line options for the <c>generate-api-token</c> verb.
		/// </summary>
		[Verb("generate-api-token", HelpText = "Generates an API token for an application registration.")]
		public class GenerateApiTokenOptions : BaseOptions { }
		/// <summary>
		/// Represents the command line options for the <c>push</c> verb.
		/// </summary>
		[Verb("push", HelpText = "Push application registration data into backend. Registers new application, adds new user registration properties.")]
		public class PushOptions : BaseOptions {
			/// <summary>
			/// Specifies the directory containing the application definition files to push.
			/// </summary>
			[Value(0, MetaName = "APP_DEFINITIONS_DIRECTORY", HelpText = "(Default: Current Directory) The directory in which there is one json file for each application to push. Note: appsettings.*.json and appsettings.json files are ignored.")]
			public string AppDefinitionsDirectory { get; set; } = Environment.CurrentDirectory;
		}
		/// <summary>
		/// Represents the command line options for the currently unimplemented <c>remove-property</c> verb.
		/// </summary>
		[Verb("remove-property", HelpText = "(Not yet implemented) Remove a user registration property from an application registration and all its instances.")]
		public class RemovePropertyOptions : BaseOptions {
			// Not implemented
		}
		/// <summary>
		/// Represents the command line options for the currently unimplemented <c>remove-application</c> verb.
		/// </summary>
		[Verb("remove-application", HelpText = "(Not yet implemented) Removes an application AND ALL ASSOCIATED DATABASE ENTRIES (user registrations, property definitions, app log metadata).")]
		public class RemoveApplicationOptions : BaseOptions {
			// Not implemented
		}

		[Verb("apply-migrations", HelpText = "Applies the database migrations to the target database. This is usually intended for local development use as the production environment has automation for this.", Hidden = true)]
		public class ApplyMigrationsOptions : BaseOptions { }

		[Verb("list-recipients", HelpText = "Lists the currently registered recipients for a registered application.")]
		public class ListRecipientsOptions : BaseOptions {
			[Value(0, MetaName = "APP_NAME", HelpText = "The application of which to list the recipients.", Required = true)]
			public string AppName { get; set; }
		}

		[Verb("remove-recipient", HelpText = "Remove a given data recipient key from a registered application.")]
		public class RemoveRecipientOptions : BaseOptions {
			// Not implemented
		}

		async static Task<int> Main(string[] args) => await ((Func<ParserResult<object>, Task<int>>)(res => res.MapResult(
			async (PushOptions opts) => await PushMain(opts),
			async (GenerateApiTokenOptions opts) => await GenerateApiTokenMain(opts),
			async (RemovePropertyOptions opts) => await RemovePropertyMain(opts),
			async (RemoveApplicationOptions opts) => await RemoveApplicationMain(opts),
			async (ApplyMigrationsOptions opts) => await ApplyMigrationsMain(opts),
			async (RemoveRecipientOptions opts) => await RemoveRecipientMain(opts),
			async (ListRecipientsOptions opts) => await ListRecipientsMain(opts),
			async errs => await DisplayHelp(res, errs)
			)))(new Parser(c => c.HelpWriter = null).
			ParseArguments<PushOptions, GenerateApiTokenOptions, RemovePropertyOptions, RemoveApplicationOptions, ApplyMigrationsOptions, RemoveRecipientOptions, ListRecipientsOptions>(args));

		async static Task<int> DisplayHelp(ParserResult<object> result, IEnumerable<Error> errs) {
			await Console.Out.WriteLineAsync(HelpText.AutoBuild(result, h => {
				h.AdditionalNewLineAfterOption = false;
				h.Heading = $"SGL Analytics Application Registration Tool {Assembly.GetExecutingAssembly().GetName().Version}";
				h.MaximumDisplayWidth = 170;
				return h;
			}));
			return 1;
		}

		static IHostBuilder CreateHostBuilder(BaseOptions opts, Action<IServiceCollection> confServices) =>
			 Host.CreateDefaultBuilder()
					.UseConsoleLifetime(options => options.SuppressStatusMessages = true)
					.ConfigureAppConfiguration(config => {
						foreach (var configFile in opts.ConfigFiles) {
							config.AddJsonFile(configFile);
						}
						var tmpConf = config.Build();
						var additionalConfFiles = new List<string>();
						tmpConf.GetSection("AdditionalConfigFiles").Bind(additionalConfFiles);
						foreach (var acf in additionalConfFiles) {
							Console.WriteLine($"Including additional config file {acf}");
							config.AddJsonFile(acf, optional: true, reloadOnChange: true);
						}
					})
					.ConfigureServices((context, services) => {
						services.UseUsersBackendInfrastructure(context.Configuration);
						services.UseLogsBackendInfrastructure(context.Configuration);
						services.AddScoped<AppRegistrationManager>();
						confServices(services);
					});

		async static Task<int> PushMain(PushOptions opts) {
			ServiceResultWrapper<PushJob, int> exitCodeWrapper = new(0);
			using var host = CreateHostBuilder(opts, services => {
				services.AddSingleton(opts);
				services.AddSingleton(exitCodeWrapper);
				services.AddScopedBackgroundService<PushJob>();
			}).Build();
			await host.RunAsync();
			return exitCodeWrapper.Result;
		}

		async static Task<int> GenerateApiTokenMain(GenerateApiTokenOptions opts) {
			var token = SecretGenerator.Instance.GenerateSecret(32);
			await Console.Out.WriteLineAsync(token);
			return 0;
		}
		async static Task<int> RemovePropertyMain(RemovePropertyOptions opts) {
			await Console.Out.WriteLineAsync("This verb is not yet implemented.");
			return 1;
		}
		async static Task<int> RemoveApplicationMain(RemoveApplicationOptions opts) {
			await Console.Out.WriteLineAsync("This verb is not yet implemented.");
			return 1;
		}

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

		async static Task<int> RemoveRecipientMain(RemoveRecipientOptions opts) {
			await Console.Out.WriteLineAsync("This verb is not yet implemented.");
			return 1;
		}
	}
}
