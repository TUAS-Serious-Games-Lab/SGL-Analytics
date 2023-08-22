using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.IO;

namespace SGL.Analytics.Backend.Domain.Entity {

	/// <summary>
	/// Acts as a base class for certificates stored in association with an application.
	/// </summary>
	public abstract class ApplicationCertificateBase {
		private string certificatePem = "";
		private Certificate? certificate = null;
		/// <summary>
		/// The id of the App to which this entry belongs.
		/// </summary>
		public Guid AppId { get; set; }
		/// <summary>
		/// The Id derived from the certificate's public key, used to identify the key pair behind the certificate within the app.
		/// </summary>
		public KeyId PublicKeyId { get; }
		/// <summary>
		/// A human-readable label providing an easy means of identification for the entry.
		/// This can, e.g. be the role or name of the person represented by the certificate.
		/// </summary>
		public string Label { get; set; }
		/// <summary>
		/// The certificate held by this entry, encoded in the PEM format.
		/// </summary>
		public string CertificatePem {
			get => certificatePem;
			set {
				certificatePem = value;
				certificate = null;
			}
		}

		/// <summary>
		/// The certificate for the held by this entry.
		/// </summary>
		public Certificate Certificate {
			get {
				if (certificate == null) {
					using var strReader = new StringReader(CertificatePem);
					certificate = Certificate.LoadOneFromPem(strReader);
				}
				return certificate;
			}
		}

		/// <summary>
		/// Constructs a new certificate entry for an application.
		/// </summary>
		/// <param name="appId">The id of the associated application.</param>
		/// <param name="publicKeyId">The key id of the public key of the certificate held in the entry.</param>
		/// <param name="label">A human-readable label to identify the certificate.</param>
		/// <param name="certificatePem">The actual certificate data in PEM-encoded form.</param>
		public ApplicationCertificateBase(Guid appId, KeyId publicKeyId, string label, string certificatePem) {
			AppId = appId;
			Label = label;
			PublicKeyId = publicKeyId;
			CertificatePem = certificatePem;
		}

		/// <summary>
		///	Calculates the KeyId of the certificate's public key from the PEM-encoded certificate data.
		/// </summary>
		/// <param name="certificatePem">The PEM data containing the certificate.</param>
		/// <returns>The KeyId of the certificate's public key.</returns>
		protected static KeyId GetKeyIdFromPem(string certificatePem) {
			using var strReader = new StringReader(certificatePem);
			var cert = Certificate.LoadOneFromPem(strReader);
			return cert.PublicKey.CalculateId();
		}
	}
}