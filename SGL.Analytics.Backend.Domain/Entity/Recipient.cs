using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.IO;

namespace SGL.Analytics.Backend.Domain.Entity {
	public class Recipient {
		public Guid AppId { get; set; }
		public Application App { get; set; } = null!;
		public KeyId PublicKeyId { get; init; }
		public string Label { get; set; }
		public string CertificatePem { get; set; }

		public Recipient(Guid appId, string label, string certificatePem) {
			AppId = appId;
			Label = label;
			CertificatePem = certificatePem;
		}

		public static Recipient Create(Application app, KeyId publicKeyId, string label, string certificatePem) {
			var r = new Recipient(app.Id, label, certificatePem) { PublicKeyId = publicKeyId };
			r.App = app;
			return r;
		}

		public static Recipient Create(Application app, string label, string certificatePem) {
			using var strReader = new StringReader(certificatePem);
			var cert = Certificate.LoadOneFromPem(strReader);
			var keyId = KeyId.CalculateId(cert.PublicKey);
			return Create(app, keyId, label, certificatePem);
		}
	}
}
