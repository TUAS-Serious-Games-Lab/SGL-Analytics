using SGL.Analytics.Backend.Domain.Entity;
using SGL.Utilities.Backend.Applications;
using SGL.Utilities.Crypto.Keys;

namespace SGL.Analytics.Backend.Users.Application.Interfaces {
	/// <summary>
	/// Encapsulates options for queries on <see cref="IApplicationRepository{ApplicationWithUserProperties, ApplicationQueryOptions}"/>.
	/// </summary>
	public class ApplicationQueryOptions {
		/// <summary>
		/// If true, indicates to fetch the defined user properties for each fetched application.
		/// </summary>
		public bool FetchUserProperties { get; set; } = true;
		/// <summary>
		/// If true, indicates to fetch associated recipient key entries for each fetched application.
		/// </summary>
		public bool FetchRecipients { get; set; } = false;
		/// <summary>
		/// If true, indicates to fetch associated exporter certificate entries for each fetched application.
		/// </summary>
		public bool FetchExporterCertificates { get; set; } = false;
		/// <summary>
		/// If true, indicates to fetch the exporter certificate with the given key id for each fetched application.
		/// </summary>
		public KeyId? FetchExporterCertificate { get; set; } = null;
	}
}
