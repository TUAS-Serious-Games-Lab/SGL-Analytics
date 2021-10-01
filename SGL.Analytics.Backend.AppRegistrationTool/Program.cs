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
using SGL.Analytics.Backend.WebUtilities;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
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
						services.AddScoped<AppRegistrationManager>();
					});

		async static Task<int> PushMain(PushOptions opts) {
			using var host = CreateHostBuilder(opts).Build();

			var contextTypes = new[] { typeof(LogsContext), typeof(UsersContext) };
			await host.WaitForDbsReadyAsync<LogsContext, UsersContext>(pollingInterval: TimeSpan.FromMilliseconds(100));
			using (var scope = host.Services.CreateScope()) {
				var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
				try {
					using var transactions = (await Task.WhenAll(
						contextTypes.Select(ctxT => scope.ServiceProvider.GetRequiredService(ctxT))
							.OfType<DbContext>()
							.Select(ctx => ctx.Database.BeginTransactionAsync())
					)).ToDisposableEnumerable<IDbContextTransaction>();

					var appRegMgr = scope.ServiceProvider.GetRequiredService<AppRegistrationManager>();

					var definitions = new List<ApplicationWithUserProperties>();
					var results = Enumerable.Empty<AppRegistrationManager.PushResult>();
					foreach (var appDef in definitions) {
						// Can't use Select(), because definitions need to be processed in sequence, Select() would map to task and run all
						// push operations concurrently, which is not supported by DbContext. Therefore, we need to await each push operation separately.
						results.Concat(await appRegMgr.PushApplicationAsync(appDef));
					}

					if (results.Any(res => res == AppRegistrationManager.PushResult.Error)) {
						logger.LogInformation("Due to the above errors, the transactions for the updates are being rolled back now...");
						foreach (var t in transactions) {
							await t.RollbackAsync();
						}
						return 2;
					}
					else {
						foreach (var t in transactions) {
							await t.CommitAsync();
						}
						logger.LogInformation("The transactions were successfully committed.");
						return 0;
					}
				}
				catch (Exception ex) {
					logger.LogError(ex, "Encountered and unexpected error.");
					return 3;
				}
			}
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
