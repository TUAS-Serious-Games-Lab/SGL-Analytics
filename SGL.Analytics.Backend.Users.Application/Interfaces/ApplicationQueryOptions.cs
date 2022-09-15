namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	public class ApplicationQueryOptions {
		public bool FetchUserProperties { get; set; } = true;
		public bool FetchRecipients { get; set; } = false;
		public bool FetchExporterCertificates { get; set; } = false;
	}
}
