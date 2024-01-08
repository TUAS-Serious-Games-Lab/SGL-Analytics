using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Entity {
	/// <summary>
	/// Represents signer for an application that signs certificates for public keys such as exporter authentication keys..
	/// </summary>
	public class SignerCertificate : ApplicationCertificateBase {
		/// <summary>
		/// The App to which this signer certificate belongs.
		/// </summary>
		public Application App { get; set; } = null!;

		/// <summary>
		/// The certificate for the signer's public key, authorizing them as a valid signer, encoded in the PEM format.
		/// </summary>
		public new string CertificatePem {
			get => base.CertificatePem;
			set => base.CertificatePem = value;
		}

		/// <summary>
		/// The certificate for the signer's public key, authorizing them as a valid signer.
		/// </summary>
		public new Certificate Certificate => base.Certificate;

		/// <summary>
		/// Instantiates a <see cref="SignerCertificate"/> object with the given data.
		/// </summary>
		public SignerCertificate(Guid appId, KeyId publicKeyId, string label, string certificatePem) : base(appId, publicKeyId, label, certificatePem) { }

		/// <summary>
		/// Creates a new <see cref="SignerCertificate"/> object using the given data.
		/// </summary>
		/// <param name="app">The application to which the signer belongs.</param>
		/// <param name="publicKeyId">The public key id of the signer.</param>
		/// <param name="label">A label identifying the signer in humand-readable form.</param>
		/// <param name="certificatePem">The certificate authorizing the signer's public key, in PEM-encoded form.</param>
		/// <returns>The created object.</returns>
		public static SignerCertificate Create(Application app, KeyId publicKeyId, string label, string certificatePem) {
			var r = new SignerCertificate(app.Id, publicKeyId, label, certificatePem);
			r.App = app;
			return r;
		}

		/// <summary>
		/// Creates a new <see cref="SignerCertificate"/> object using the given data.
		/// The <see cref="KeyId"/> is derived from the public key in <paramref name="certificatePem"/>.
		/// </summary>
		/// <param name="app">The application to which the signer belongs.</param>
		/// <param name="label">A label identifying the signer in humand-readable form.</param>
		/// <param name="certificatePem">The certificate for the signer's public key, in PEM-encoded form.</param>
		/// <returns>The created object.</returns>
		public static SignerCertificate Create(Application app, string label, string certificatePem) {
			return Create(app, GetKeyIdFromPem(certificatePem), label, certificatePem);
		}

	}
}
