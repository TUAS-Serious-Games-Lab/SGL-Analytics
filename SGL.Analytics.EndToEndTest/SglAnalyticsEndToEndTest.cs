using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Client;
using SGL.Analytics.ExporterClient;
using SGL.Utilities;
using SGL.Utilities.TestUtilities.XUnit;
using System.Net.Sockets;
using Xunit.Abstractions;

namespace SGL.Analytics.EndToEndTest {
	public class SglAnalyticsEndToEndTest {
		public static bool ShouldRun() {
			var backendUrl = Environment.GetEnvironmentVariable("TEST_BACKEND");
			if (backendUrl != null) {
				return true;
			}
			using (TcpClient tcpClient = new TcpClient()) {
				try {
					tcpClient.Connect("localhost", 443);
					return true;
				}
				catch (Exception) {
					return false;
				}
			}
		}

		private string appName;
		private string appApiToken;
		private HttpClient httpClient;
		public ILoggerFactory LoggerFactory { get; }
		private ILogger<SglAnalyticsEndToEndTest> logger;
		private readonly ITestOutputHelper output;

		private string? recipientCaCertPemText = null;
		private string? recipientCaCertPemFile = null;

		public SglAnalyticsEndToEndTest(ITestOutputHelper output) {
			LoggerFactory = CreateLoggerFactory(output);
			logger = LoggerFactory.CreateLogger<SglAnalyticsEndToEndTest>();
			appName = Environment.GetEnvironmentVariable("TEST_APPNAME") ?? "TestApp1";
			appApiToken = Environment.GetEnvironmentVariable("TEST_APP_API_TOKEN") ?? "JdXRSl5QWnb9JVbLGE+zLKcpDUx7qJPMtGEu59e5oeM=";
			recipientCaCertPemFile = Environment.GetEnvironmentVariable("TEST_RECIPIENT_CA_FILE");
			if (recipientCaCertPemFile == null) {
				recipientCaCertPemText = Environment.GetEnvironmentVariable("TEST_RECIPIENT_CA_PEM");
				if (recipientCaCertPemText == null) {
					recipientCaCertPemText = localDevDemoSignerCertificatesPem;
				}
			}
			httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri(Environment.GetEnvironmentVariable("TEST_BACKEND") ?? "https://localhost/");
			this.output = output;
		}

		private ILoggerFactory CreateLoggerFactory(ITestOutputHelper output, string? tag = null) {
			return Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder
				.AddDebug()
				.AddXUnit(() => output, config => {
					if (tag != null) {
						config.AddTag(tag);
					}
				})
				.SetMinimumLevel(LogLevel.Trace)
				.AddConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> {
					["LogLevel:SGL"] = "Trace",
				}).Build()));
		}
		private void logUncaughtException(Exception ex) {
			LoggerFactory.CreateLogger<SingleThreadedSynchronizationContext>().LogError(ex, "Exception escapted from async callback.");
		}

		[Fact]
		public async Task UsersCanUploadLogsWhichCanBeExportedAndDecryptedByRecipients() {
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
			var ct = cts.Token;
			using var syncContext = new SingleThreadedSynchronizationContext(logUncaughtException);
			await syncContext;
			await using (var analytics = new SglAnalytics(appName, appApiToken, httpClient, config => {
				if (recipientCaCertPemFile != null) {
					config.UseRecipientCertificateAuthorityFromReader(() => File.OpenText(recipientCaCertPemFile), recipientCaCertPemFile, ignoreCAValidityPeriod: true);
				}
				else {
					config.UseEmbeddedRecipientCertificateAuthority(recipientCaCertPemText!, ignoreCAValidityPeriod: true);
				}
				config.UseDataDirectory(args => Path.Combine(Directory.GetCurrentDirectory(), "AnalyticsData"));
				config.UseLoggerFactory(_ => LoggerFactory, false);
				config.ConfigureCryptography(config => config.AllowSharedMessageKeyPair());
			})) {
				// TODO: Register and generate logs
			}

			await using (var exporter = new SglAnalyticsExporter(httpClient, config => {
				config.UseLoggerFactory(_ => LoggerFactory, false);
			})) {
				// TODO: Download, decrypt and check user registration and logs
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