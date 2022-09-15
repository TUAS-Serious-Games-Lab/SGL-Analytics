using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Entity {
	/// <summary>
	/// Represents an authorized key pair for authenticating a an exporter user, i.e. a user that can download data instead of just uploading data.
	/// The key pair is owner by the respective user and is represented by a certificate over the public key.
	/// The certificate is signed by a trusted signer to proof that it is valid.
	/// The contained key is then used to validate a challenge response where the client signed a challenge byte string using the user's private key.
	/// </summary>
	public class ExporterKeyAuthCertificate : ApplicationCertificateBase {
		/// <summary>
		/// The App to which this entry belongs.
		/// </summary>
		public ApplicationWithUserProperties App { get; set; } = null!;

		/// <summary>
		/// Creates a new <see cref="ExporterKeyAuthCertificate"/> object using the given data.
		/// </summary>
		public ExporterKeyAuthCertificate(Guid appId, KeyId publicKeyId, string label, string certificatePem) : base(appId, publicKeyId, label, certificatePem) { }

		/// <summary>
		/// Creates a new <see cref="ExporterKeyAuthCertificate"/> object using the given data.
		/// </summary>
		/// <param name="app">The application to which the key-authentication certificate belongs.</param>
		/// <param name="publicKeyId">The public key id of the key-authentication certificate.</param>
		/// <param name="label">A label identifying the key-authentication certificate (or the person using it) in humand-readable form.</param>
		/// <param name="certificatePem">The certificate authorizing the key-authentication certificate's public key, in PEM-encoded form.</param>
		/// <returns>The created object.</returns>
		public static ExporterKeyAuthCertificate Create(ApplicationWithUserProperties app, KeyId publicKeyId, string label, string certificatePem) {
			var ekac = new ExporterKeyAuthCertificate(app.Id, publicKeyId, label, certificatePem);
			ekac.App = app;
			return ekac;
		}

		/// <summary>
		/// Creates a new <see cref="ExporterKeyAuthCertificate"/> object using the given data.
		/// The <see cref="KeyId"/> is derived from the public key in <paramref name="certificatePem"/>.
		/// </summary>
		/// <param name="app">The application to which the key-authentication certificate belongs.</param>
		/// <param name="label">A label identifying the key-authentication certificate (or the person using it) in humand-readable form.</param>
		/// <param name="certificatePem">The certificate authorizing the key-authentication certificate's public key, in PEM-encoded form.</param>
		/// <returns>The created object.</returns>
		public static ExporterKeyAuthCertificate Create(ApplicationWithUserProperties app, string label, string certificatePem) {
			return Create(app, GetKeyIdFromPem(certificatePem), label, certificatePem);
		}
	}
}
