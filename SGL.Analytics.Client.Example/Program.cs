using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
			[Option('u', "username", HelpText = "Username to use for the registration if not already registered.\nIf not specified, an alphanumeric random string is used.\nUse --no-username to register with empty username.")]
			public string Username { get; set; } = StringGenerator.GenerateRandomWord(8);
			[Option("no-username", HelpText = "Register without username field.")]
			public bool NoUsername { get; set; } = false;
			[Option('v', "verbose", HelpText = "Produce extra output. (Draws the board after each move.)")]
			public bool Verbose { get; set; } = false;
			[Option('l', "log-level", HelpText = "Diagnostics logging level for SGL Analytics.")]
			public LogLevel LoggingLevel { get; set; } = LogLevel.None;
			[Option('i', "user-id-file", HelpText = "Write registered user id to the given file.")]
			public string? UserIdFile { get; set; } = null;
			[Option('o', "logs-list", HelpText = "Write the list of IDs of the recorded game logs.")]
			public string? LogsListFile { get; set; } = null;
			[Option('k', "keep", HelpText = "Keep the recorded files in the archive/ subdirectory under the log storage directory after upload.")]
			public bool KeepFiles { get; set; } = false;
			[Option('d', "logs-directory", HelpText = "Override the storage directory to use for log storage.")]
			public string? LogsDirectory { get; set; } = null;
			[Option('s', "recipient-signer-pem", HelpText = "The PEM file containing the trusted signer certificates for authorized recipients. Must be specified, unless the local dev demo certificate shall be used.")]
			public string? RecipientSignerPemFile { get; set; } = null;

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
						MovesFiles = new []{ "moves.txt" },
						RecipientSignerPemFile = "Signers.pem"
					}),
			};
		}
		async static Task Main(string[] args) => await ((Func<ParserResult<Options>, Task>)(res => res.MapResult(RealMain, async errs => await DisplayHelp(res, errs))))(new Parser(c => c.HelpWriter = null).ParseArguments<Options>(args));

		async static Task DisplayHelp(ParserResult<Options> result, IEnumerable<Error> errs) {
			await Console.Out.WriteLineAsync(HelpText.AutoBuild(result, h => {
				h.AdditionalNewLineAfterOption = false;
				h.Heading = $"SGL Analytics Client Demo {Assembly.GetExecutingAssembly().GetName().Version}";
				h.MaximumDisplayWidth = 180;
				return h;
			}));
		}

		async static Task RealMain(Options opts) {
			using var loggerFactory = LoggerFactory.Create(config => config.ClearProviders().AddConsole().SetMinimumLevel(opts.LoggingLevel));
			using var syncContext = new SingleThreadedSynchronizationContext(ex => {
				loggerFactory.CreateLogger<SingleThreadedSynchronizationContext>().LogError(ex, "Exception escapted from async callback.");
			});
			await syncContext; // Switch to 'main' thread context.
			var logger = loggerFactory.CreateLogger<Program>();
			using var httpClient = new HttpClient();
			httpClient.BaseAddress = opts.Backend;
			await using SglAnalytics analytics = new SglAnalytics(opts.AppName, opts.AppApiToken, httpClient, config => {
				if (opts.LogsDirectory != null) {
					config.UseAnonymousLogStorage(args => {
						var logStorage = new DirectoryLogStorage(opts.LogsDirectory ?? Path.Combine(args.DataDirectory, "DataLogs", "Anonymous"));
						logStorage.Archiving = opts.KeepFiles;
						return logStorage;
					});
					config.UseUserLogStorage(args => {
						var logStorage = new DirectoryLogStorage(opts.LogsDirectory ?? Path.Combine(args.DataDirectory, "DataLogs", args.Username ?? args.UserId?.ToString() ?? "Anonymous"));
						logStorage.Archiving = opts.KeepFiles;
						return logStorage;
					});
				}
				config.UseLoggerFactory(_ => loggerFactory, dispose: false);
				if (opts.RecipientSignerPemFile == null) {
					config.UseEmbeddedRecipientCertificateAuthority(localDevDemoSignerCertificatesPem, ignoreCAValidityPeriod: true);
				}
				else {
					config.UseRecipientCertificateValidator(args => {
						using var reader = File.OpenText(opts.RecipientSignerPemFile);
						return new CACertTrustValidator(reader, opts.RecipientSignerPemFile, ignoreValidityPeriod: true,
							args.LoggerFactory.CreateLogger<CACertTrustValidator>(), args.LoggerFactory.CreateLogger<CertificateStore>());
					});
				}
			});
			if (!analytics.IsRegistered()) {
				try {
					if (opts.NoUsername) {
						await analytics.RegisterUserWithDeviceSecretAsync(new BaseUserData(null));
					}
					else {
						string password = SecretGenerator.Instance.GenerateSecret(10);
						await Console.Out.WriteLineAsync($"Generated password: {password}");
						await analytics.RegisterUserWithPasswordAsync(new BaseUserData(opts.Username), password);
					}
				}
				catch (Exception ex) {
					await Console.Error.WriteLineAsync($"Registration Error: {ex.Message}");
					Environment.ExitCode = 2;
				}
			}
			if (opts.UserIdFile != null) {
				await File.WriteAllLinesAsync(opts.UserIdFile, Enumerable.Repeat(analytics.LoggedInUserId.ToString() ?? "<null>", 1));
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
		private const string localDevDemoSignerCertificatesPem = @"
-----BEGIN CERTIFICATE-----
MIIFuTCCA6GgAwIBAgIUKpQ24sBFO9bqQjDP+7s+wGCbcngwDQYJKoZIhvcNAQEL
BQAwZDELMAkGA1UEBhMCREUxGTAXBgNVBAoMEEhvY2hzY2h1bGUgVHJpZXIxJDAi
BgNVBAsMG1NlbmlvciBIZWFsdGggR2FtZXMgUHJvamVjdDEUMBIGA1UEAwwLVGVz
dCBTaWduZXIwHhcNMjIwOTE0MDkwNjM5WhcNMzIwOTEzMDkwNjM5WjBkMQswCQYD
VQQGEwJERTEZMBcGA1UECgwQSG9jaHNjaHVsZSBUcmllcjEkMCIGA1UECwwbU2Vu
aW9yIEhlYWx0aCBHYW1lcyBQcm9qZWN0MRQwEgYDVQQDDAtUZXN0IFNpZ25lcjCC
AiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAJqMM9IVEFEGTI4h6zxIiZBd
11vosyI6juQ+V6j+QlCIJUrh0y1AmePDZKHfymNMd3vj3plUsVquQo3LyEbzfH6R
QyZUZGEiqFfsXhGbtmPYYSx+9uwQKn7xOiYWtdDJx1ZRdX0wYO8T0QxYBX3r4Ead
uymHz4yXmJUShnuSzwWNF8BA1RTmfZD4r9u4u2MqEn6FzLJu9BOCWG8BFBO3cAsM
UfmFAnrTKWm8pTW7xUu6D6aJc7dn3SbpNmqJPNSfWJLGmtWpJThYKH22CAlbrD98
tN1Yr6cibG3P4O8mJLN/Mz3SREDQPjOwNiH91CxZq2GE2rdcMArKaPZ3hecGGagM
eCLjRqcPTlpxT81xBidw4acQQhwhJl9HjVwNrdDWadIkmwUKXWU2qjU4EziZ01O1
7HQz3ApjdvBd+gckhak071ac/pHt9aT6aeN3dk21hqOLMEYtvSsfFf4cnQE5zSxp
Y2Q1MpE3B5GyBwWso0vlLu7KrUNqGwa0puEgv+j0qcckkEJJZuaqkoTEZ+WTC7Bf
UYojxXun2MHecF1hIqw/k7YIcmNXuLYq5OQrpuMOTF+KB2kjeP8mHYbvah19DVJg
r9xKMlj+fSZ5iHrvL9kTfb6f4WLm5kptyW3XH+1/jaJ48M5j1jjiKcXLl+grOsLc
fGucytaPJ4mcBo4p/uwtAgMBAAGjYzBhMB0GA1UdDgQWBBThPtKWdjOLXIQtvX2X
IIk75e95UjAfBgNVHSMEGDAWgBThPtKWdjOLXIQtvX2XIIk75e95UjAPBgNVHRMB
Af8EBTADAQH/MA4GA1UdDwEB/wQEAwIBBjANBgkqhkiG9w0BAQsFAAOCAgEAezlK
aoIOHNd/5sMOa2LYyr+TcramqenQ6/h7/e4+UuPH5ovtC3TzmzB8rhOa2g5rhsqK
N+Yc1iSLJMNd2s1PqQlN6WgTaA6JbUDh3h4r4advVY+n3B8omJjJ2iU/csm9q1a4
U4yz9/r3LXyQy5t1Q7hoUFSahrdMMybOsb20bMpwTMcJB0hrIdl1yB/tXOHS2Ax+
i3UeDMupiW+Bj+frPIlD+zsaLGIfe71U4jcfO+AEhJEqPWl/zJNsok/vRzfS1Rvg
86Xi2o+X0bu/7a2ivrpLJDG8ytBkF4uAKwjK7PnhoDuyWmeP4be5/gmT1+cHA4zH
zgDWl0UJ6JrJcZE6SuIgdF/2tZhiL7y1HnUywqq7uj5h9UT6TyOFEF0BG7Ee6ZOF
/47m2eFP0SMRKSBenYITg9ByOAxuZIsuxqv1UAVj85ptg0WU0pisj+lxup46YOIs
IklyxrNwUSFlBSCkzI8PRuhV8NIycRowiBCrWmqEiOxR4FLx53gyTgTQoCJtrllL
h1n6VBPHGysCKT5/Wefyi3DMimNuhyM9Ci7QP88ann+d5smraMpgy1Z+/jmmPQOa
BqWhTbMxCis0OJw9HUtRsh4ftW2n7h3vd+DoT+Czu40T0qzAO+XnXMj6tg3A+e8S
5Youu5yh3WKRfU5eBEC9fSqzjodd/SZAsOWoSXk=
-----END CERTIFICATE-----
";
	}
}
