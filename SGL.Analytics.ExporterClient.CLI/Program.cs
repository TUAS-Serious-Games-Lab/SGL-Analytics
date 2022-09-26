using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using SGL.Analytics.ExporterClient;
using SGL.Utilities;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SGL.Analytics.ExporterClient.CLI;

class Program {
	class Options {
		[Option('b', "backend", HelpText = "(Default: https://localhost) Specify the base URL of the API backend.")]
		public Uri Backend { get; set; } = new Uri("https://localhost");
		[Option('a', "appname", HelpText = "Specifiy the app on which to operate.")]
		public string AppName { get; set; } = "SGL.Analytics.Client.Example";
		[Option('l', "log-level", HelpText = "Diagnostics logging level for SGL Analytics.")]
		public LogLevel LoggingLevel { get; set; } = LogLevel.None;
		[Option('r', "raw-output", HelpText = "The directory to which the raw game log files shall be written.")]
		public string? LogsOutputDir { get; set; } = null;
		[Option('u', "user-output", HelpText = "The directory to which the user registration files shall be written.")]
		public string? UsersOutputDir { get; set; } = null;
	}
	async static Task Main(string[] args) => await ((Func<ParserResult<Options>, Task>)(res => res.MapResult(RealMain, async errs => await DisplayHelp(res, errs))))(new Parser(c => c.HelpWriter = null).ParseArguments<Options>(args));

	async static Task DisplayHelp(ParserResult<Options> result, IEnumerable<Error> errs) {
		await Console.Out.WriteLineAsync(HelpText.AutoBuild(result, h => {
			h.AdditionalNewLineAfterOption = false;
			h.Heading = $"SGL Analytics Exporter CLI {Assembly.GetExecutingAssembly().GetName().Version}";
			h.MaximumDisplayWidth = 180;
			return h;
		}));
	}

	async static Task RealMain(Options opts) {
		using var cts = new CancellationTokenSource();
		var ct = cts.Token;
		Console.CancelKeyPress += (_, _) => cts.Cancel();
		using var loggerFactory = LoggerFactory.Create(config => config.ClearProviders().AddConsole().SetMinimumLevel(opts.LoggingLevel));
		using var syncContext = new SingleThreadedSynchronizationContext(ex => {
			loggerFactory.CreateLogger<SingleThreadedSynchronizationContext>().LogError(ex, "Exception escapted from async callback.");
		});
		await syncContext; // Switch to 'main' thread context.
		var logger = loggerFactory.CreateLogger<Program>();
		using var httpClient = new HttpClient();
		httpClient.BaseAddress = opts.Backend;
		await using SglAnalyticsExporter exporter = new SglAnalyticsExporter(httpClient, config => {
			config.UseLoggerFactory(_ => loggerFactory, false);
		});
	}
}
