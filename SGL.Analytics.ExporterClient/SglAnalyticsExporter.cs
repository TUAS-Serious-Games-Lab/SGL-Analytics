namespace SGL.Analytics.ExporterClient {
	public partial class SglAnalyticsExporter {
		public SglAnalyticsExporter(HttpClient httpClient, Action<ISglAnalyticsExporterConfigurator> configuration) {
			this.httpClient = httpClient;
			configuration(configurator);
		}
	}
}
