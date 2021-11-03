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
using SGL.Analytics.Backend.Utilities;
using SGL.Analytics.Utilities;
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
		async static Task<int> Main(string[] args) => await ((Func<ParserResult<object>, Task<int>>)(res => res.MapResult(
			async (PushOptions opts) => await PushMain(opts),
			async (GenerateApiTokenOptions opts) => await GenerateApiTokenMain(opts),
			async (RemovePropertyOptions opts) => await RemovePropertyMain(opts),
			async (RemoveApplicationOptions opts) => await RemoveApplicationMain(opts),
			async errs => await DisplayHelp(res, errs)
			)))(new Parser(c => c.HelpWriter = null).
			ParseArguments<PushOptions, GenerateApiTokenOptions, RemovePropertyOptions, RemoveApplicationOptions>(args));

		async static Task<int> DisplayHelp(ParserResult<object> result, IEnumerable<Error> errs) {
			await Console.Out.WriteLineAsync(HelpText.AutoBuild(result, h => {
				h.AdditionalNewLineAfterOption = false;
				h.Heading = $"SGL Analytics Application Registration Tool {Assembly.GetExecutingAssembly().GetName().Version}";
				h.MaximumDisplayWidth = 170;
				return h;
			}));
			return 1;
		}

		static IHostBuilder CreateHostBuilder(PushOptions opts, ServiceResultWrapper<PushJob, int> exitCodeWrapper) =>
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
						services.AddSingleton(opts);
						services.AddSingleton(exitCodeWrapper);
						services.AddScopedBackgroundService<PushJob>();
					});

		async static Task<int> PushMain(PushOptions opts) {
			ServiceResultWrapper<PushJob, int> exitCodeWrapper = new(0);
			using var host = CreateHostBuilder(opts, exitCodeWrapper).Build();
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
