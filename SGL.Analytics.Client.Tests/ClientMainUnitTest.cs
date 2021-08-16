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

	}
}
