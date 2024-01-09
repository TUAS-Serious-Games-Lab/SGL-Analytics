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
using SGL.Analytics.Backend.Users.Application.Interfaces;
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
	public partial class Program {
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
			/// <summary>
			/// If set to true, allows deletion of less critical related table entries associated with the app registrations,
			/// such as exporter and signer certificates, i.e. those that are not generally forbidden in push operations due to risk of data loss.
			/// If false, warnings are logged instead of deleting the entry.
			/// </summary>
			[Option('d', "allow-related-entry-delete", Default = false, HelpText = "Allow deletion of less critical related table entries associated with the app registrations," +
				" such as exporter and signer certificates, i.e. those that are not generally forbidden in push operations due to risk of data loss." +
				" If false, warnings are logged instead of deleting the entry.")]
			public bool AllowRelatedEntryDelete { get; set; } = false;
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

		/// <summary>
		/// Represents the command line options for the <c>apply-migrations</c> verb.
		/// </summary>
		[Verb("apply-migrations", HelpText = "Applies the database migrations to the target database. This is usually intended for local development use as the production environment has automation for this.", Hidden = true)]
		public class ApplyMigrationsOptions : BaseOptions { }

		/// <summary>
		/// Represents the command line options for the <c>list-recipients</c> verb.
		/// </summary>
		[Verb("list-recipients", HelpText = "Lists the currently registered recipients for a registered application.")]
		public class ListRecipientsOptions : BaseOptions {
			/// <summary>
			/// The unique name of the application on which to operate.
			/// </summary>
			[Value(0, MetaName = "APP_NAME", HelpText = "The application of which to list the recipients.", Required = true)]
			public string AppName { get; set; } = null!; // Required field, filled by CommandLine.
		}

		/// <summary>
		/// Represents the command line options for the <c>remove-recipient</c> verb.
		/// </summary>
		[Verb("remove-recipient", HelpText = "Remove a given data recipient key from a registered application.")]
		public class RemoveRecipientOptions : BaseOptions {
			/// <summary>
			/// The unique name of the application on which to operate.
			/// </summary>
			[Value(0, MetaName = "APP_NAME", HelpText = "The application from which to remove the recipient from.", Required = true)]
			public string AppName { get; set; } = null!; // Required field, filled by CommandLine.
			/// <summary>
			/// The key id of the recipient key-pair to remove.
			/// </summary>
			[Value(1, MetaName = "KEYID", HelpText = "The keyid of the recipient to remove.", Required = true)]
			public string KeyId { get; set; } = null!; // Required field, filled by CommandLine.
		}

		/// <summary>
		/// Represents the command line options for the <c>relabel-recipient</c> verb.
		/// </summary>
		[Verb("relabel-recipient", HelpText = "Assign a new label to the given data recipient key in a registered application.")]
		public class RelabelRecipientOptions : BaseOptions {
			/// <summary>
			/// The unique name of the application on which to operate.
			/// </summary>
			[Value(0, MetaName = "APP_NAME", HelpText = "The application in which to relabel the recipient.", Required = true)]
			public string AppName { get; set; } = null!; // Required field, filled by CommandLine.
			/// <summary>
			/// The key id of the recipient key-pair of which to change the label.
			/// </summary>
			[Value(1, MetaName = "KEYID", HelpText = "The keyid of the recipient to change the label of.", Required = true)]
			public string KeyId { get; set; } = null!; // Required field, filled by CommandLine.
			/// <summary>
			/// The new label to assign.
			/// </summary>
			[Value(2, MetaName = "NEW_LABEL", HelpText = "The new label text.", Required = true)]
			public string Label { get; set; } = null!; // Required field, filled by CommandLine.
		}
		/// <summary>
		/// Represents the command line options for the <c>list-exporters</c> verb.
		/// </summary>
		[Verb("list-exporters", HelpText = "Lists the currently registered exporters for a registered application.")]
		public class ListExportersOptions : BaseOptions {
			/// <summary>
			/// The unique name of the application on which to operate.
			/// </summary>
			[Value(0, MetaName = "APP_NAME", HelpText = "The application of which to list the exporters.", Required = true)]
			public string AppName { get; set; } = null!; // Required field, filled by CommandLine.
		}

		/// <summary>
		/// Represents the command line options for the <c>relabel-exporters</c> verb.
		/// </summary>
		[Verb("relabel-exporter", HelpText = "Assign a new label to the given data exporter key in a registered application.")]
		public class RelabelExporterOptions : BaseOptions {
			/// <summary>
			/// The unique name of the application on which to operate.
			/// </summary>
			[Value(0, MetaName = "APP_NAME", HelpText = "The application in which to relabel the exporter.", Required = true)]
			public string AppName { get; set; } = null!; // Required field, filled by CommandLine.
			/// <summary>
			/// The key id of the exporter key-pair of which to change the label.
			/// </summary>
			[Value(1, MetaName = "KEYID", HelpText = "The keyid of the exporter to change the label of.", Required = true)]
			public string KeyId { get; set; } = null!; // Required field, filled by CommandLine.
			/// <summary>
			/// The new label to assign.
			/// </summary>
			[Value(2, MetaName = "NEW_LABEL", HelpText = "The new label text.", Required = true)]
			public string Label { get; set; } = null!; // Required field, filled by CommandLine.
		}

		async static Task<int> Main(string[] args) => await ((Func<ParserResult<object>, Task<int>>)(res => res.MapResult(
			async (PushOptions opts) => await PushMain(opts),
			async (GenerateApiTokenOptions opts) => await GenerateApiTokenMain(opts),
			async (RemovePropertyOptions opts) => await RemovePropertyMain(opts),
			async (RemoveApplicationOptions opts) => await RemoveApplicationMain(opts),
			async (ApplyMigrationsOptions opts) => await ApplyMigrationsMain(opts),
			async (RemoveRecipientOptions opts) => await RemoveRecipientMain(opts),
			async (ListRecipientsOptions opts) => await ListRecipientsMain(opts),
			async (RelabelRecipientOptions opts) => await RelabelRecipientMain(opts),
			async (ListExportersOptions opts) => await ListExportersMain(opts),
			async (RelabelExporterOptions opts) => await RelabelExporterMain(opts),
			async errs => await DisplayHelp(res, errs)
			)))(new Parser(c => c.HelpWriter = null).
			ParseArguments<PushOptions, GenerateApiTokenOptions, RemovePropertyOptions, RemoveApplicationOptions, ApplyMigrationsOptions,
				RemoveRecipientOptions, ListRecipientsOptions, RelabelRecipientOptions>(args));

		static Task<int> RelabelExporterMain(RelabelExporterOptions opts) {
			throw new NotImplementedException();
		}

		static Task<int> ListExportersMain(ListExportersOptions opts) {
			throw new NotImplementedException();
		}

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
	}
}
