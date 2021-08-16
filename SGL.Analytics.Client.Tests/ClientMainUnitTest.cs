using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Analytics.Client.Tests {
	public class ClientMainUnitTest : IDisposable {

		private InMemoryLogStorage storage = new InMemoryLogStorage();
		private FakeRootDataStore ds = new FakeRootDataStore();
		private SGLAnalytics analytics;

		public ClientMainUnitTest() {
			analytics = new SGLAnalytics("SGLAnalyticsUnitTests", "FakeApiKey", ds, storage);
		}

		public void Dispose() {
			storage.Dispose();
		}

		[Fact]
		public void EachStartNewLogCreatesLogFile() {
			Assert.Empty(storage.EnumerateLogs());
			analytics.StartNewLog();
			Assert.Single(storage.EnumerateLogs());
			analytics.StartNewLog();
			Assert.Equal(2, storage.EnumerateLogs().Count());
			analytics.StartNewLog();
			Assert.Equal(3, storage.EnumerateLogs().Count());
			analytics.StartNewLog();
			Assert.Equal(4, storage.EnumerateLogs().Count());
		}

		[Fact]
		public async Task AllLogsAreClosedAfterFinish() {
			analytics.StartNewLog();
			analytics.StartNewLog();
			analytics.StartNewLog();
			analytics.StartNewLog();
			analytics.StartNewLog();
			await analytics.FinishAsync();
			Assert.All(storage.EnumerateLogs().Cast<InMemoryLogStorage.LogFile>(), log => Assert.True(log.WriteClosed));
		}
	}
}
