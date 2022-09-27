using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using SGL.Analytics.ExporterClient;
using SGL.Analytics.ExporterClient.Implementations;
using SGL.Utilities;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SGL.Analytics.ExporterClient.CLI;

class Program {
	class Options {
		[Option('b', "backend", HelpText = "(Default: https://localhost) Specify the base URL of the API backend.")]
		public Uri Backend { get; set; } = new Uri("https://localhost");
		[Option('a', "appname", HelpText = "Specify the app on which to operate.")]
		public string AppName { get; set; } = "SGL.Analytics.Client.Example";
		[Option('l', "log-level", HelpText = "Diagnostics logging level for SGL Analytics.")]
		public LogLevel LoggingLevel { get; set; } = LogLevel.None;
		[Option('r', "raw-output", HelpText = "The directory to which the raw game log files shall be written.")]
		public string? LogsOutputDir { get; set; } = null;
		[Option('u', "user-output", HelpText = "The directory to which the user registration files shall be written.")]
		public string? UsersOutputDir { get; set; } = null;
		[Option('k', "key-file", Required = true, HelpText = "The file containing the authentication and decryption key pairs (with their associated certificates for identification).\n" +
			"If $SGL_ANALYTICS_EXPORTER_KEY_PASSPHRASE is set, the passphrase for this file will be taken from it, otherwise the passphrase will be prompted.")]
		public string KeyFile { get; set; } = null!;
		[Option('j', "request-concurrency", HelpText = "Specify how many concurrent requests an opertation that needs to perform many requests is allowed to perform.")]
		public int RequestConcurrency { get; set; } = 16;
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
		var logger = loggerFactory.CreateLogger<Program>();
		using var httpClient = new HttpClient();
		httpClient.BaseAddress = opts.Backend;
		await using SglAnalyticsExporter exporter = new SglAnalyticsExporter(httpClient, config => {
			config.UseLoggerFactory(_ => loggerFactory, false);
			config.UseRequestConcurrency(() => opts.RequestConcurrency);
		});
		var keyPassphrase = Environment.GetEnvironmentVariable("SGL_ANALYTICS_EXPORTER_KEY_PASSPHRASE")?.ToCharArray();
		if (keyPassphrase == null) {
			keyPassphrase = PasswordReader.PromptPassword("Please enter key file passphrase");
		}
		await exporter.UseKeyFileAsync(opts.KeyFile, () => keyPassphrase, ct);
		await exporter.SwitchToApplicationAsync(opts.AppName, ct);
		var logMetadata = await exporter.GetLogFileMetadataAsync(q => q, ct);
		foreach (var logMd in logMetadata) {
			Console.Out.WriteLine($"{logMd.LogFileId:D}{logMd.NameSuffix} {logMd.UserId} {logMd.Size} {logMd.EndTime} {logMd.LogContentEncoding}");
		}
		var userRegSink = new SimpleDirectoryUserRegistrationSink();
		if (opts.UsersOutputDir != null) userRegSink.DirectoryPath = opts.UsersOutputDir;
		await exporter.GetDecryptedUserRegistrationsAsync(userRegSink, q => q, ct);
		var logsSink = new SimpleDirectoryLogFileSink();
		if (opts.LogsOutputDir != null) logsSink.DirectoryPath = opts.LogsOutputDir;
		await exporter.GetDecryptedLogFilesAsync(logsSink, q => q, ct);
	}
}
