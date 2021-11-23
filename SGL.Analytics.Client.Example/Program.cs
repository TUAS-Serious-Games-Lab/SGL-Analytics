using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Example {
	class Program {
		class Options {
			[Option('b', "backend", HelpText = "(Default: https://localhost) Specify the base URL of the API backend.")]
			public Uri Backend { get; set; } = new Uri("https://localhost");
			[Option('a', "appname", HelpText = "Specify the app name to use under which the demo app is registered in the backend.", Default = "SGL.Analytics.Client.Example")]
			public string AppName { get; set; } = "SGL.Analytics.Client.Example";
			[Option('t', "token", HelpText = "Specify the API token value to use that is registered in the backend.")]
			public string AppApiToken { get; set; } = "FUfq7iwB43fCkIXLlRSiSy2CKrm6FWmAt/L3kzAqELU=";
			[Option('u', "username", HelpText = "Username to use for the registration if not already registered. If not specified, an alphanumeric random string is used.")]
			public string Username { get; set; } = StringGenerator.GenerateRandomWord(8);
			[Option('v', "verbose", HelpText = "Produce extra output. (Draws the board after each move.)")]
			public bool Verbose { get; set; } = false;
			[Option('l', "log-level", HelpText = "Diagnostics logging level for SGL Analytics.")]
			public LogLevel LoggingLevel { get; set; } = LogLevel.None;

			[Option('o',"logs-list", HelpText = "Write the list of IDs of the recorded game logs.")]
			public string? LogsListFile { get; set; } = null;
			[Option('k',"keep", HelpText = "Keep the recorded files in the archive/ subdirectory under the log storage directory after upload.")]
			public bool KeepFiles { get; set; } = false;
			[Option('d', "logs-directory", HelpText = "Override the storage directory to use for log storage.")]
			public string? LogsDirectory { get; set; } = null;

			[Value(0, MetaName = "MOVES_FILES", HelpText = "The name(s) / path(s) of one or more files containing the moves to make in the simulated TicTacToe game in order. " +
				"Each line in these files should consist of two numbers in range 1-3, separated by a comma, that indicate the column and row position to mark. " +
				"The moves alternate between players. If no file is given, standard input is used.")]
			public IEnumerable<string> MovesFiles { get; set; } = new string[0];

			[Usage]
			public static IEnumerable<CommandLine.Text.Example> Examples => new List<CommandLine.Text.Example> {
				new CommandLine.Text.Example("Run a game with moves from moves.txt and submit analytics with registered user. " +
					"If not registered, register as demouser. Uses the specified connection details for the backend.",
					new UnParserSettings(){ PreferShortName = true},
					new Options() {
						AppApiToken = StringGenerator.GenerateRandomWord(32),
						Username = "demouser",
						MovesFiles = new []{ "moves.txt" }
					}),
			};
		}
		async static Task Main(string[] args) => await ((Func<ParserResult<Options>, Task>)(res => res.MapResult(RealMain, async errs => await DisplayHelp(res, errs))))(new Parser(c => c.HelpWriter = null).ParseArguments<Options>(args));

		async static Task DisplayHelp(ParserResult<Options> result, IEnumerable<Error> errs) {
			await Console.Out.WriteLineAsync(HelpText.AutoBuild(result, h => {
				h.AdditionalNewLineAfterOption = false;
				h.Heading = $"SGL Analytics Client Demo {Assembly.GetExecutingAssembly().GetName().Version}";
				h.MaximumDisplayWidth = 170;
				return h;
			}));
		}

		async static Task RealMain(Options opts) {
			ILogger<SGLAnalytics> logger = NullLogger<SGLAnalytics>.Instance;
			if (opts.LoggingLevel < LogLevel.None) {
				logger = LoggerFactory.Create(config => config.ClearProviders().AddConsole().SetMinimumLevel(opts.LoggingLevel)).CreateLogger<SGLAnalytics>();
			}
			var rootDS = new FileRootDataStore(opts.AppName);
			var logStorage = new DirectoryLogStorage(opts.LogsDirectory ?? Path.Combine(rootDS.DataDirectory, "DataLogs"));
			logStorage.Archiving = opts.KeepFiles;
			SGLAnalytics analytics = new SGLAnalytics(opts.AppName, opts.AppApiToken,
				rootDataStore: rootDS,
				logStorage: logStorage,
				logCollectorClient: new LogCollectorRestClient(opts.Backend),
				userRegistrationClient: new UserRegistrationRestClient(opts.Backend),
				diagnosticsLogger: logger);
			if (!analytics.IsRegistered()) {
				try {
					await analytics.RegisterAsync(new BaseUserData(opts.Username));
					Environment.ExitCode = 2;
				}
				catch (Exception ex) {
					await Console.Error.WriteLineAsync($"Registration Error: {ex.Message}");
				}
			}
			TicTacToeController gameController = new TicTacToeController(analytics, opts.Verbose, Console.Out);
			if (opts.MovesFiles.Any()) {
				foreach (var movesFile in opts.MovesFiles) {
					using (var fileReader = new StreamReader(new FileStream(movesFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))) {
						await gameController.ReadAndProcessMoves(fileReader);
					}
				}
			}
			else {
				await gameController.ReadAndProcessMoves(Console.In);
			}
			try {
				await analytics.FinishAsync();
			}
			catch (Exception ex) {
				logger.LogError(ex, "An exception was thrown from FinishAsync.");
				Environment.ExitCode = 3;
			}
			var logIds = gameController.LogIds.Select(id => id.ToString());
			await Console.Out.WriteLineAsync($"\n The following logs were recorded: {string.Join(", ", logIds)}");
			if (opts.LogsListFile != null) {
				await File.WriteAllLinesAsync(opts.LogsListFile, logIds);
			}
		}
	}
}
