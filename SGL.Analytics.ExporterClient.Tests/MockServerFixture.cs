using System;
using WireMock.Server;
using Xunit;

namespace SGL.Analytics.ExporterClient.Tests {
	public class MockServerFixture : IDisposable {
		private WireMockServer server;

		public WireMockServer Server => server;
		public MockServerFixture() {
			server = WireMockServer.Start();
		}
		public void Reset() {
			server.Reset();
		}
		public void Dispose() {
			server.Stop();
			server.Dispose();
		}
	}

	[CollectionDefinition("Mock Web Server")]
	public class MockServerCollection : ICollectionFixture<MockServerFixture> { }


}
