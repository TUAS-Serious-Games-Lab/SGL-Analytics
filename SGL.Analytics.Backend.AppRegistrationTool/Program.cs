using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.AppRegistrationTool {
	class Program {
		[Verb("push", isDefault: true, HelpText = "Push application registration data into backend. Registers new application, adds new user registration properties.")]
		class PushOptions {

		}
		[Verb("remove-property", HelpText = "(Not yet implemented) Remove a user registration property from an application registration and all its instances.")]
		class RemovePropertyOptions {
			// Not implemented
		}
		[Verb("remove-application", HelpText = "(Not yet implemented) Removes an application AND ALL ASSOCIATED DATABASE ENTRIES (user registrations, property definitions, app log metadata).")]
		class RemoveApplicationOptions {
			// Not implemented
		}
		[Verb("generate-api-token", HelpText = "Generates an API token for an application registration.")]
		class GenerateApiTokenOptions {

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
		async static Task<int> PushMain(PushOptions opts) {
			return 0;
		}
		async static Task<int> GenerateApiTokenMain(GenerateApiTokenOptions opts) {
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
