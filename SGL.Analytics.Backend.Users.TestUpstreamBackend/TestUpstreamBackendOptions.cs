namespace SGL.Analytics.Backend.Users.TestUpstreamBackend {
	public class TestUpstreamBackendOptions {
		public const string ConfigSectionName = "Sgla:TestUpstream";
		public string AppName { get; set; } = "Testing";
		public string Secret { get; set; } = null!;
	}
}
