using Microsoft.Extensions.Logging;
using SGL.Analytics.Client;
using SGL.Analytics.ExporterClient;
using SGL.Utilities;
using SGL.Utilities.TestUtilities.XUnit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.EndToEndTest {
	public partial class SglAnalyticsEndToEndTest {
		[ConditionallyTestedFact(typeof(SglAnalyticsEndToEndTest), nameof(ShouldRun), "No test backend available.")]
		public async Task UsersWithDelegatedAuthCanRegisterAuthenticateAndCorrectlyUploadLogs() {
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
			var ct = cts.Token;
			using var syncContext = new SingleThreadedSynchronizationContext(logUncaughtException);
			await syncContext;
			Guid userId;
			Guid log1Id;
			Guid snapShot1Id = Guid.NewGuid();
			Guid log2Id;
			Guid log3Id;
			Guid snapShot2Id = Guid.NewGuid();
			Guid log4Id;
			UserData userData = new UserData { Foo = 4242, Bar = "This is a Test!!", Obj = new Dictionary<string, string> { ["A"] = "Y", ["B"] = "X" } };
			var beginTime = DateTime.Now;
			var testUpstreamClient = new TestUpstreamClient(httpClient);
			await using (var analytics = new SglAnalytics(appName, appApiToken, httpClient, config => {
				if (recipientCaCertPemFile != null) {
					config.UseRecipientCertificateAuthorityFromReader(() => File.OpenText(recipientCaCertPemFile), recipientCaCertPemFile, ignoreCAValidityPeriod: true);
				}
				else {
					config.UseEmbeddedRecipientCertificateAuthority(recipientCaCertPemText!, ignoreCAValidityPeriod: true);
				}
				config.UseDataDirectory(args => Path.Combine(Directory.GetCurrentDirectory(), "AnalyticsData_Delegated"));
				config.UseLoggerFactory(_ => LoggerFactory, false);
				config.ConfigureCryptography(config => config.AllowSharedMessageKeyPair());
			})) {
				await testUpstreamClient.StartSession(testUpstreamSecret, ct);
				var loginResult = await analytics.TryLoginWithUpstreamDelegationAsync(ct => Task.FromResult(testUpstreamClient.Authorization!.Value), ct);
				Assert.Equal(LoginAttemptResult.CredentialsNotAvailable, loginResult);
				await analytics.RegisterWithUpstreamDelegationAsync(userData, ct => Task.FromResult(testUpstreamClient.Authorization!.Value), ct);
				Assert.Equal(testUpstreamClient.AuthorizedUserId, analytics.LoggedInUserId);
				Assert.True(analytics.LoggedInUserId.HasValue);
				userId = analytics.LoggedInUserId.Value;
				logger.LogInformation("Registered user {userId}.", userId);
				(log1Id, log2Id) = await RecordTestData(analytics, snapShot1Id, ct);
			}
			var midTime = DateTime.Now;
			await using (var analytics = new SglAnalytics(appName, appApiToken, httpClient, config => {
				if (recipientCaCertPemFile != null) {
					config.UseRecipientCertificateAuthorityFromReader(() => File.OpenText(recipientCaCertPemFile), recipientCaCertPemFile, ignoreCAValidityPeriod: true);
				}
				else {
					config.UseEmbeddedRecipientCertificateAuthority(recipientCaCertPemText!, ignoreCAValidityPeriod: true);
				}
				config.UseDataDirectory(args => Path.Combine(Directory.GetCurrentDirectory(), "AnalyticsData_Delegated"));
				config.UseLoggerFactory(_ => LoggerFactory, false);
				config.ConfigureCryptography(config => config.AllowSharedMessageKeyPair());
			})) {
				await testUpstreamClient.StartSession(testUpstreamSecret, ct);
				var loginResult = await analytics.TryLoginWithUpstreamDelegationAsync(ct => Task.FromResult(testUpstreamClient.Authorization!.Value), ct);
				Assert.Equal(LoginAttemptResult.Completed, loginResult);
				Assert.Equal(testUpstreamClient.AuthorizedUserId, analytics.LoggedInUserId);
				Assert.Equal(userId, analytics.LoggedInUserId);
				Assert.True(analytics.LoggedInUserId.HasValue);
				userId = analytics.LoggedInUserId.Value;
				(log3Id, log4Id) = await RecordTestData(analytics, snapShot2Id, ct);
			}
			var endTime = DateTime.Now;

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
				await ValidateTestData(exporter, userId, log1Id, log2Id, snapShot1Id, beginTime, midTime, userData, ct);
				await ValidateTestData(exporter, userId, log3Id, log4Id, snapShot2Id, midTime, endTime, userData, ct);
			}
		}
	}
}
