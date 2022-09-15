using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.IO;

namespace SGL.Analytics.Backend.Domain.Entity {
	/// <summary>
	/// Represents an authorized recipient for the collected data under end-to-end encryption.
	/// The client uses these entries to determine the public keys for which the data keys for the files shall be encrypted.
	/// </summary>
	public class Recipient : ApplicationCertificateBase {
		/// <summary>
		/// The App to which this recipient belongs.
		/// </summary>
		public Application App { get; set; } = null!;

		/// <summary>
		/// The certificate for the recipient's public key, authorizing them as a valid recipient, encoded in the PEM format.
		/// The authorization of the recipient is achieved by it being present in the recipient list and the certificate being singed by a signer key pair trusted by the client.
		/// </summary>
		public new string CertificatePem {
			get => base.CertificatePem;
			set => base.CertificatePem = value;
		}

		/// <summary>
		/// The certificate for the recipient's public key, authorizing them as a valid recipient.
		/// The authorization of the recipient is achieved by it being present in the recipient list and the certificate being singed by a signer key pair trusted by the client.
		/// </summary>
		public new Certificate Certificate => base.Certificate;

		/// <summary>
		/// Instantiates a <see cref="Recipient"/> object with the given data.
		/// </summary>
		public Recipient(Guid appId, KeyId publicKeyId, string label, string certificatePem) : base(appId, publicKeyId, label, certificatePem) { }

		/// <summary>
		/// Creates a new <see cref="Recipient"/> object using the given data.
		/// </summary>
		/// <param name="app">The application to which the recipient belongs.</param>
		/// <param name="publicKeyId">The public key id of the recipient.</param>
		/// <param name="label">A label identifying the recipient in humand-readable form.</param>
		/// <param name="certificatePem">The certificate authorizing the recipient's public key, in PEM-encoded form.</param>
		/// <returns>The created object.</returns>
		public static Recipient Create(Application app, KeyId publicKeyId, string label, string certificatePem) {
			var r = new Recipient(app.Id, publicKeyId, label, certificatePem);
			r.App = app;
			return r;
		}

		/// <summary>
		/// Creates a new <see cref="Recipient"/> object using the given data.
		/// The <see cref="KeyId"/> is derived from the public key in <paramref name="certificatePem"/>.
		/// </summary>
		/// <param name="app">The application to which the recipient belongs.</param>
		/// <param name="label">A label identifying the recipient in humand-readable form.</param>
		/// <param name="certificatePem">The certificate authorizing the recipient's public key, in PEM-encoded form.</param>
		/// <returns>The created object.</returns>
		public static Recipient Create(Application app, string label, string certificatePem) {
			return Create(app, GetKeyIdFromPem(certificatePem), label, certificatePem);
		}

	}
}
