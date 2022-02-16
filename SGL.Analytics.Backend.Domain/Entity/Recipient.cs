using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;
using SGL.Utilities.Crypto;
using System;
using System.IO;

namespace SGL.Analytics.Backend.Domain.Entity {
	public class Recipient {
		public Guid AppId { get; set; }
		public Application App { get; set; } = null!;
		public KeyId PublicKeyId { get; set; }
		public string Label { get; set; }
		public string CertificatePem { get; set; }

		public Recipient(Guid appId, KeyId publicKeyId, string label, string certificatePem) {
			AppId = appId;
			PublicKeyId = publicKeyId;
			Label = label;
			CertificatePem = certificatePem;
		}

		public static Recipient Create(Application app, KeyId publicKeyId, string label, string certificatePem) {
			var r = new Recipient(app.Id, publicKeyId, label, certificatePem);
			r.App = app;
			return r;
		}

		public static Recipient Create(Application app, string label, string certificatePem) {
			using var strReader = new StringReader(certificatePem);
			var pemReader = new PemReader(strReader);
			var cert = (X509Certificate)pemReader.ReadObject();
			var keyId = KeyId.CalculateId(cert.GetPublicKey());
			return Create(app, keyId, label, certificatePem);
		}
	}
}
