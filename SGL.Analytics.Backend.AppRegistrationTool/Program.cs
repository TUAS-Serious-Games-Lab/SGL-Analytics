using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SGL.Analytics.Backend.Logs.Infrastructure;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Users.Infrastructure;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.Backend.WebUtilities;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.AppRegistrationTool {
	class Program {
		class BaseOptions {
			[Option('c', "config", HelpText = "Specifies an additional config file to use.")]
			public IEnumerable<string> ConfigFiles { get; set; } = new List<string>();
		}
		[Verb("generate-api-token", HelpText = "Generates an API token for an application registration.")]
		class GenerateApiTokenOptions : BaseOptions { }
		[Verb("push", HelpText = "Push application registration data into backend. Registers new application, adds new user registration properties.")]
		class PushOptions : BaseOptions {
			[Value(0, MetaName = "APP_DEFINITIONS_DIRECTORY", HelpText = "(Default: Current Directory) The directory in which there is one json file for each application to push.")]
			public string AppDefinitionsDirectory { get; set; } = Environment.CurrentDirectory;
		}
		[Verb("remove-property", HelpText = "(Not yet implemented) Remove a user registration property from an application registration and all its instances.")]
		class RemovePropertyOptions : BaseOptions {
			// Not implemented
		}
		[Verb("remove-application", HelpText = "(Not yet implemented) Removes an application AND ALL ASSOCIATED DATABASE ENTRIES (user registrations, property definitions, app log metadata).")]
		class RemoveApplicationOptions : BaseOptions {
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

		static IHostBuilder CreateHostBuilder(PushOptions opts) =>
			 Host.CreateDefaultBuilder()
					.UseConsoleLifetime()
					.ConfigureAppConfiguration(config => {
						foreach (var configFile in opts.ConfigFiles) {
							config.AddJsonFile(configFile);
						}
					})
					.ConfigureServices((context, services) => {
						services.UseUsersBackendInfrastructure(context.Configuration);
						services.UseLogsBackendInfrastructure(context.Configuration);
					});

		async static Task<int> PushMain(PushOptions opts) {
			using var host = CreateHostBuilder(opts).Build();

			await host.WaitForDbsReadyAsync<LogsContext, UsersContext>(pollingInterval: TimeSpan.FromMilliseconds(100));
			using (var scope = host.Services.CreateScope()) {

			}
			return 0;
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
