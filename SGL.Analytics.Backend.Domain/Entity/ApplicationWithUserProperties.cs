using SGL.Analytics.Backend.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SGL.Analytics.Backend.Domain.Entity {
	/// <summary>
	/// Models a registered application that uses SGL Analytics, extending <see cref="Application"/> with the ability to store application-specific per-user registration properties
	/// and certificates for authorized data exporters, as needed by the users backend.
	/// </summary>
	public class ApplicationWithUserProperties : Application {
		/// <summary>
		/// If the application uses delegated authentication using authorization tokens from an upstream system,
		/// stores the URL, pointing to the upstream backend, which is called with the upstream token for validation.
		/// </summary>
		public Uri? BasicFederationUpstreamAuthUrl { get; set; } = null;
		/// <summary>
		/// A collection containing the per-user property definitions for this application.
		/// It indicates which properties are supported or required.
		/// </summary>
		public ICollection<ApplicationUserPropertyDefinition> UserProperties { get; set; } = null!;
		/// <summary>
		/// A collection containing the user registration for this application.
		/// </summary>
		public IReadOnlyCollection<UserRegistration> UserRegistrations { get; set; } = null!;
		/// <summary>
		/// A collection containing the key certificates for authorized data exporters.
		/// </summary>
		public ICollection<ExporterKeyAuthCertificate> AuthorizedExporters { get; set; } = null!;
		/// <summary>
		/// A collection containing the key certificates for signers for the application.
		/// </summary>
		public ICollection<SignerCertificate> SignerCertificates { get; set; } = null!;

		/// <summary>
		/// Creates an <see cref="ApplicationWithUserProperties"/> with the given data values, leaving <see cref="UserProperties"/> and <see cref="UserRegistrations"/> empty.
		/// This constructor is intended to be used by the OR mapper. To create a new application, see <see cref="Create(string, string, Uri?)"/>.
		/// </summary>
		public ApplicationWithUserProperties(Guid id, string name, string apiToken, Uri? basicFederationUpstreamAuthUrl = null) :
			base(id, name, apiToken) {
			BasicFederationUpstreamAuthUrl = basicFederationUpstreamAuthUrl;
		}

		/// <summary>
		/// Creates an application object with the given id and data values.
		/// The <see cref="UserProperties"/> are initialized with an empty collection object, that can be filled with <see cref="AddProperty(string, UserPropertyType, bool)"/>.
		/// </summary>
		/// <returns>The created object.</returns>
		public static ApplicationWithUserProperties Create(Guid id, string name, string apiToken, Uri? basicFederationUpstreamAuthUrl = null) {
			var app = new ApplicationWithUserProperties(id, name, apiToken, basicFederationUpstreamAuthUrl);
			app.UserProperties = new List<ApplicationUserPropertyDefinition>();
			app.DataRecipients = new List<Recipient>();
			app.AuthorizedExporters = new List<ExporterKeyAuthCertificate>();
			app.SignerCertificates = new List<SignerCertificate>();
			return app;
		}

		/// <summary>
		/// Creates an application object with the given data values and a generated id.
		/// The <see cref="UserProperties"/> are initialized with an empty collection object, that can be filled with <see cref="AddProperty(string, UserPropertyType, bool)"/>.
		/// </summary>
		/// <returns>The created object.</returns>
		public static ApplicationWithUserProperties Create(string name, string apiToken, Uri? basicFederationUpstreamAuthUrl = null) {
			return Create(Guid.NewGuid(), name, apiToken, basicFederationUpstreamAuthUrl);
		}

		/// <summary>
		/// Adds a new property definition to the application object.
		/// Definitions with <paramref name="required"/> set to <see langword="true"/> should preferably only be added to newly created applications,
		/// as adding one after the fact doesn't automatically add instances of the property to existing user registrations.
		/// </summary>
		/// <param name="name">The name of the property. Must be unique within the application.</param>
		/// <param name="type">The data type of the property.</param>
		/// <param name="required">Whether the property is required, otherwise it is optional.</param>
		/// <returns>The created <see cref="ApplicationUserPropertyDefinition"/>.</returns>
		public ApplicationUserPropertyDefinition AddProperty(string name, UserPropertyType type, bool required = false) {
			if (UserProperties.Count(p => p.Name == name) > 0) {
				throw new ConflictingPropertyNameException(name);
			}
			var prop = ApplicationUserPropertyDefinition.Create(this, name, type, required);
			UserProperties.Add(prop);
			return prop;
		}
		/// <summary>
		/// Adds an authorized exporter entry that grants an exporter authentication key pair access for exporting data.
		/// </summary>
		/// <param name="label">A descriptive label as a human-readable identifier for the key.</param>
		/// <param name="certificatePem">The certificate for the key pair, encoded in PEM format.</param>
		/// <returns>The created certificate entry object.</returns>
		public ExporterKeyAuthCertificate AddAuthorizedExporter(string label, string certificatePem) {
			var exporter = ExporterKeyAuthCertificate.Create(this, label, certificatePem);
			AuthorizedExporters.Add(exporter);
			return exporter;
		}

		public SignerCertificate AddSignerCertificate(string label, string certificatePem) {
			var signer = SignerCertificate.Create(this, label, certificatePem);
			SignerCertificates.Add(signer);
			return signer;
		}
	}
}
