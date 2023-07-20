using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Client;
using SGL.Analytics.DTO;
using SGL.Analytics.ExporterClient;
using SGL.Analytics.ExporterClient.Values;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.TestUtilities.XUnit;
using System.Net.Sockets;
using System.Text.Json;
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
		private string? recipientKeyFile = null;
		private string? recipientKeyText = null;
		private string? recipientKeyPassphrase = null;
		private string? rekeyRecipientCaCertPemFile = null;
		private string? rekeyRecipientCaCertPemText = null;
		private string? rekeyRecipientKeyFile = null;
		private string? rekeyRecipientKeyText = null;
		private string? rekeyRecipientKeyPassphrase = null;

		public SglAnalyticsEndToEndTest(ITestOutputHelper output) {
			LoggerFactory = CreateLoggerFactory(output);
			logger = LoggerFactory.CreateLogger<SglAnalyticsEndToEndTest>();
			appName = Environment.GetEnvironmentVariable("TEST_APPNAME") ?? "TestApp1";
			appApiToken = Environment.GetEnvironmentVariable("TEST_APP_API_TOKEN") ?? "JdXRSl5QWnb9JVbLGE+zLKcpDUx7qJPMtGEu59e5oeM=";
			recipientCaCertPemFile = Environment.GetEnvironmentVariable("TEST_RECIPIENT_CA_FILE");
			if (recipientCaCertPemFile == null) {
				recipientCaCertPemText = Environment.GetEnvironmentVariable("TEST_RECIPIENT_CA_PEM");
				if (recipientCaCertPemText == null) {
					recipientCaCertPemText = localDevDemoSignerCertificatePem;
				}
			}
			recipientKeyFile = Environment.GetEnvironmentVariable("TEST_RECIPIENT_KEY_FILE");
			if (recipientKeyFile == null) {
				recipientKeyText = Environment.GetEnvironmentVariable("TEST_RECIPIENT_KEY_PEM");
			}
			recipientKeyPassphrase = Environment.GetEnvironmentVariable("TEST_RECIPIENT_KEY_PASSPHRASE");

			rekeyRecipientCaCertPemFile = Environment.GetEnvironmentVariable("TEST_REKEY_RECIPIENT_CA_FILE");
			if (rekeyRecipientCaCertPemFile == null) {
				rekeyRecipientCaCertPemText = Environment.GetEnvironmentVariable("TEST_REKEY_RECIPIENT_CA_PEM");
				if (rekeyRecipientCaCertPemText == null) {
					rekeyRecipientCaCertPemText = localDevDemoRekeySignerCertificatePem;
				}
			}
			rekeyRecipientKeyFile = Environment.GetEnvironmentVariable("TEST_REKEY_RECIPIENT_KEY_FILE");
			if (rekeyRecipientKeyFile == null) {
				rekeyRecipientKeyText = Environment.GetEnvironmentVariable("TEST_REKEY_RECIPIENT_KEY_PEM");
			}
			rekeyRecipientKeyPassphrase = Environment.GetEnvironmentVariable("TEST_REKEY_RECIPIENT_KEY_PASSPHRASE");

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

		private bool FindFirstExistingFile(out string path, params string[] paths) {
			foreach (var p in paths) {
				if (File.Exists(p)) {
					path = p;
					return true;
				}
			}
			path = null!;
			return false;
		}

		public class UserData : BaseUserData {
			public int Foo { get; set; }
			public string Bar { get; set; }
			public object Obj { get; set; }
		}

		[ConditionallyTestedFact(typeof(SglAnalyticsEndToEndTest), nameof(ShouldRun), "No test backend available.")]
		public async Task UsersCanUploadLogsWhichCanBeExportedAndDecryptedByRecipients() {
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
			var ct = cts.Token;
			using var syncContext = new SingleThreadedSynchronizationContext(logUncaughtException);
			await syncContext;
			Guid userId;
			Guid log1Id;
			Guid snapShotId = Guid.NewGuid();
			Guid log2Id;
			var beginTime = DateTime.Now;
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
				if (!analytics.IsRegistered()) {
					await analytics.RegisterAsync(new UserData { Foo = 42, Bar = "This is a Test", Obj = new Dictionary<string, string> { ["A"] = "X", ["B"] = "Y" } });
				}
				userId = analytics.UserID ?? Guid.Empty;
				log1Id = analytics.StartNewLog();
				analytics.RecordEventUnshared("Test1", new { X = 12345, Y = 9876, Msg = "Hello World!" }, "TestEvent");
				analytics.RecordEventUnshared("Test1", new { X = 123.45, Y = 98.76, Msg = "Test!Test!Test!" }, "OtherTestEvent");
				analytics.RecordSnapshotUnshared("Test2", snapShotId, new {
					Position = new { X = 123, Y = 345 },
					Energy = 1000,
					Name = "JohnDoe",
					Inventory = new[] { "Apple", "Orange", "WaterBottle" }
				});
				log2Id = analytics.StartNewLog();
				await analytics.FinishAsync();
			}
			var endTime = DateTime.Now;

			KeyFile rekeyTargetKeyFile;
			if (rekeyRecipientKeyFile != null) {
				if (File.Exists(rekeyRecipientKeyFile)) {
					using var keyFile = File.OpenText(rekeyRecipientKeyFile);
					rekeyTargetKeyFile = await KeyFile.LoadAsync(keyFile, rekeyRecipientKeyFile, () => rekeyRecipientKeyPassphrase?.ToCharArray() ?? new char[0], logger, ct);
				}
				else {
					throw new FileNotFoundException("Couldn't find key file.");
				}
			}
			else if (!string.IsNullOrEmpty(rekeyRecipientKeyText)) {
				using var keyFile = new StringReader(rekeyRecipientKeyText);
				rekeyTargetKeyFile = await KeyFile.LoadAsync(keyFile, "[key file]", () => rekeyRecipientKeyPassphrase?.ToCharArray() ?? new char[0], logger, ct);
			}
			else if (FindFirstExistingFile(out var devKeyFile, "../../../RekeyingDevKeyFile.pem", "RekeyingDevKeyFile.pem", "/RekeyingDevKeyFile.pem")) {
				using var keyFile = File.OpenText(devKeyFile);
				rekeyTargetKeyFile = await KeyFile.LoadAsync(keyFile, devKeyFile, () => "ThisIsATest".ToCharArray() ?? new char[0], logger, ct);
			}
			else {
				throw new FileNotFoundException("Couldn't find key file.");
			}

			await using (var exporter = new SglAnalyticsExporter(httpClient, config => {
				config.UseLoggerFactory(_ => LoggerFactory, false);
			})) {
				if (recipientKeyFile != null) {
					if (File.Exists(recipientKeyFile)) {
						await exporter.UseKeyFileAsync(recipientKeyFile, () => recipientKeyPassphrase?.ToCharArray() ?? new char[0], ct);
					}
					else {
						throw new FileNotFoundException("Couldn't find key file.");
					}
				}
				else if (!string.IsNullOrEmpty(recipientKeyText)) {
					using var keyFile = new StringReader(recipientKeyText);
					await exporter.UseKeyFileAsync(keyFile, "[key file]", () => recipientKeyPassphrase?.ToCharArray() ?? new char[0], ct);
				}
				else if (FindFirstExistingFile(out var devKeyFile, "../../../DevKeyFile.pem", "DevKeyFile.pem", "/DevKeyFile.pem")) {
					await exporter.UseKeyFileAsync(devKeyFile, () => "ThisIsATest".ToCharArray(), ct);
				}
				else {
					throw new FileNotFoundException("Couldn't find key file.");
				}
				await ValidateTestData(exporter, userId, log1Id, log2Id, snapShotId, beginTime, endTime, ct);
				{
					// Grant access to recipient that was ignored during recording because the in-game client doesn't have their certificate:
					CACertTrustValidator keyCertValidator;
					if (rekeyRecipientCaCertPemFile != null) {
						var caCert1Content = await File.ReadAllTextAsync(rekeyRecipientCaCertPemFile);
						var caCert2Content = recipientCaCertPemFile != null ? await File.ReadAllTextAsync(recipientCaCertPemFile) : recipientCaCertPemText ?? "";
						var caCertsContent = caCert1Content + "\n\n" + caCert2Content;
						keyCertValidator = new CACertTrustValidator(caCertsContent, ignoreValidityPeriod: true, LoggerFactory.CreateLogger<CACertTrustValidator>(), LoggerFactory.CreateLogger<CertificateStore>());
					}
					else {
						var caCert1Content = rekeyRecipientCaCertPemText!;
						var caCert2Content = recipientCaCertPemFile != null ? await File.ReadAllTextAsync(recipientCaCertPemFile) : recipientCaCertPemText ?? "";
						var caCertsContent = caCert1Content + "\n\n" + caCert2Content;
						keyCertValidator = new CACertTrustValidator(caCertsContent, ignoreValidityPeriod: true, LoggerFactory.CreateLogger<CACertTrustValidator>(), LoggerFactory.CreateLogger<CertificateStore>());
					}
					logger.LogDebug("Trusting the following singer certificates:\n {certs}", string.Join("\n", keyCertValidator.TrustedCACertificates.Select(c => $"\t{c}")));
					var logRekeyResult = await exporter.RekeyLogFilesForRecipientKey(rekeyTargetKeyFile.RecipientKeyId, keyCertValidator, ct);
					logger.LogInformation("Rekeyed log files: total to rekey = {total}, successful = {successful}, unencrypted = {unencrypted}, with errors = {withErrors}",
						logRekeyResult.TotalToRekey, logRekeyResult.Successful, logRekeyResult.Unencrypted, logRekeyResult.SkippedDueToError);
					var userRekeyResult = await exporter.RekeyUserRegistrationsForRecipientKey(rekeyTargetKeyFile.RecipientKeyId, keyCertValidator, ct);
					logger.LogInformation("Rekeyed user registrations: total to rekey = {total}, successful = {successful}, unencrypted = {unencrypted}, with errors = {withErrors}",
						userRekeyResult.TotalToRekey, userRekeyResult.Successful, userRekeyResult.Unencrypted, userRekeyResult.SkippedDueToError);
				}
			}
			// Now use other recipient credentials and verify that they now have access:
			await using (var exporter = new SglAnalyticsExporter(httpClient, config => {
				config.UseLoggerFactory(_ => LoggerFactory, false);
			})) {
				await exporter.UseKeyFileAsync(rekeyTargetKeyFile, ct);
				await ValidateTestData(exporter, userId, log1Id, log2Id, snapShotId, beginTime, endTime, ct);
			}
		}

		private async Task ValidateTestData(SglAnalyticsExporter exporter, Guid userId, Guid log1Id, Guid log2Id, Guid snapShotId, DateTime beginTime, DateTime endTime, CancellationToken ct) {
			await exporter.SwitchToApplicationAsync(appName);
			{
				var log1 = await exporter.GetDecryptedLogFileByIdAsync(log1Id);

				Assert.Equal(log1Id, log1.Metadata.LogFileId);
				Assert.Equal(userId, log1.Metadata.UserId);
				Assert.InRange(log1.Metadata.UploadTime, beginTime.ToUniversalTime(), endTime.ToUniversalTime());

				Assert.NotNull(log1.Content);
				using var log1Content = log1.Content;
				var log1Entries = await exporter.ParseLogEntriesAsync(log1Content, ct).ToListAsync(ct);
				Assert.Equal("Test1", log1Entries[0].Channel);
				Assert.Equal("TestEvent", Assert.IsAssignableFrom<EventLogFileEntry>(log1Entries[0]).EventType);
				Assert.Equal(12345, Assert.Contains("X", log1Entries[0].Payload));
				Assert.Equal(9876, Assert.Contains("Y", log1Entries[0].Payload));
				Assert.Equal("Hello World!", Assert.Contains("Msg", log1Entries[0].Payload));
				Assert.InRange(log1Entries[0].TimeStamp, beginTime, endTime);

				Assert.Equal("Test1", log1Entries[1].Channel);
				Assert.Equal("OtherTestEvent", Assert.IsAssignableFrom<EventLogFileEntry>(log1Entries[1]).EventType);
				Assert.Equal(123.45, Assert.Contains("X", log1Entries[1].Payload));
				Assert.Equal(98.76, Assert.Contains("Y", log1Entries[1].Payload));
				Assert.Equal("Test!Test!Test!", Assert.Contains("Msg", log1Entries[1].Payload));
				Assert.InRange(log1Entries[1].TimeStamp, beginTime, endTime);

				Assert.Equal("Test2", log1Entries[2].Channel);
				Assert.Equal(snapShotId, Assert.IsAssignableFrom<SnapshotLogFileEntry>(log1Entries[2]).ObjectId);
				Assert.Equal(new Dictionary<string, object?> { ["X"] = 123, ["Y"] = 345 }, Assert.Contains("Position", log1Entries[2].Payload));
				Assert.Equal(1000, Assert.Contains("Energy", log1Entries[2].Payload));
				Assert.Equal("JohnDoe", Assert.Contains("Name", log1Entries[2].Payload));
				Assert.Equal(new[] { "Apple", "Orange", "WaterBottle" }.AsEnumerable(), Assert.IsAssignableFrom<IEnumerable<object?>>(Assert.Contains("Inventory", log1Entries[2].Payload)));
				Assert.InRange(log1Entries[2].TimeStamp, beginTime, endTime);
			}
			{
				var log2 = await exporter.GetDecryptedLogFileByIdAsync(log2Id);

				Assert.Equal(log2Id, log2.Metadata.LogFileId);
				Assert.Equal(userId, log2.Metadata.UserId);
				Assert.InRange(log2.Metadata.UploadTime, beginTime.ToUniversalTime(), endTime.ToUniversalTime());

				Assert.NotNull(log2.Content);
				using var log1Content = log2.Content;
				var log2Entries = await exporter.ParseLogEntriesAsync(log1Content, ct).ToListAsync(ct);
				Assert.Empty(log2Entries);
			}
			{
				var userReg = await exporter.GetDecryptedUserRegistrationByIdAsync(userId, ct);
				Assert.NotNull(userReg);
				Assert.NotNull(userReg.DecryptedStudySpecificProperties);
				Assert.Equal(42, Assert.Contains("Foo", userReg.DecryptedStudySpecificProperties));
				Assert.Equal("This is a Test", Assert.Contains("Bar", userReg.DecryptedStudySpecificProperties));
				Assert.Equal(new Dictionary<string, string> { ["A"] = "X", ["B"] = "Y" }, Assert.Contains("Obj", userReg.DecryptedStudySpecificProperties));
			}
		}

		private const string localDevDemoSignerCertificatePem = @"
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

		private const string localDevDemoRekeySignerCertificatePem = @"
-----BEGIN CERTIFICATE-----
MIICwTCCAiOgAwIBAgIQROmC/Ea5gEkYuy0ZUgl7JjAKBggqhkjOPQQDAjBrMQsw
CQYDVQQGEwJERTEZMBcGA1UECgwQSG9jaHNjaHVsZSBUcmllcjEkMCIGA1UECwwb
U2VuaW9yIEhlYWx0aCBHYW1lcyBQcm9qZWN0MRswGQYDVQQDDBJTZWNvbmQgVGVz
dCBTaWduZXIwHhcNMjMwNzEyMTMwNTIxWhcNMzIwOTEzMTEwMzEzWjBrMQswCQYD
VQQGEwJERTEZMBcGA1UECgwQSG9jaHNjaHVsZSBUcmllcjEkMCIGA1UECwwbU2Vu
aW9yIEhlYWx0aCBHYW1lcyBQcm9qZWN0MRswGQYDVQQDDBJTZWNvbmQgVGVzdCBT
aWduZXIwgZswEAYHKoZIzj0CAQYFK4EEACMDgYYABAFWZV/biGqjTR6ujnB7jWRU
L7fsacbrAHSyQ9bcz32gsaHM/twACgoJnPmW8f6PRn884f99QvIn2DTTaFovVWXV
zQHLDLcQfOEmJaoDnokbk3mP7Z5dSOtGWYPxcZjpw1cebNAvx/0JdCprI5Bxro9y
AAb++/42fSDT2qeQDsx9229rFqNmMGQwHwYDVR0jBBgwFoAUwP8L4Jm9ftaRFnug
rDDpRHP0SacwHQYDVR0OBBYEFMD/C+CZvX7WkRZ7oKww6URz9EmnMA4GA1UdDwEB
/wQEAwICBDASBgNVHRMBAf8ECDAGAQH/AgEBMAoGCCqGSM49BAMCA4GLADCBhwJB
IJrRZQRSDKmdkRvsM22KyvZuMLH9UcPm9MrWumuigK8vHXNWa6TbHvDvQ6l+xv8U
iSFd2cKrpb2Q3OETEaQDrFACQgFUzKFlZc3vW2cJPtLHHVQ2BH2UDOFXAnuu2Nlp
1teJZ+/b/wehQx4Uy23ZWxOJdyywcaeJGltIcDuRmhlEkNVX2Q==
-----END CERTIFICATE-----
";
	}
}