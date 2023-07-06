using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.KeyTool {
	public partial class MainForm {
		private static async Task GenerateKeyAndCsr(string intermediateKeyPath, string csrOutputPath, bool isSignerCert, KeyType keyType, string? ellipticCurveName, int selectedRsaKeyStrength, string passphrase, DistinguishedName csrDn) {
			var random = new RandomGenerator();
			KeyPair primaryKeyPair = null!;
			KeyPair authKeyPair = null!;
			switch (keyType) {
				case KeyType.RSA:
					var random1 = random.DeriveGenerator(256);
					var random2 = random.DeriveGenerator(256);
					var subTask1 = Task.Run(() => {
						primaryKeyPair = KeyPair.GenerateRSA(random1, selectedRsaKeyStrength);
					});
					var subTask2 = Task.CompletedTask;
					if (!isSignerCert) {
						subTask2 = Task.Run(() => {
							authKeyPair = KeyPair.GenerateRSA(random2, selectedRsaKeyStrength);
						});
					}
					await subTask1;
					await subTask2;
					break;
				case KeyType.EllipticCurves:
					primaryKeyPair = KeyPair.GenerateEllipticCurves(random, 0, ellipticCurveName);
					if (!isSignerCert) {
						authKeyPair = KeyPair.GenerateEllipticCurves(random, 0, ellipticCurveName);
					}
					break;
				default:
					throw new NotImplementedException();
			}
			using (var keyOutputFile = File.Create(intermediateKeyPath, 4096, FileOptions.Asynchronous)) {
				using var pemBuff = new MemoryStream();
				using (var pemWriter = new StreamWriter(pemBuff, leaveOpen: true)) {
					primaryKeyPair.StoreToPem(pemWriter, PemEncryptionMode.AES_256_CBC, passphrase.ToCharArray(), random);
					if (!isSignerCert) {
						authKeyPair.StoreToPem(pemWriter, PemEncryptionMode.AES_256_CBC, passphrase.ToCharArray(), random);
					}
				}
				pemBuff.Position = 0;
				await pemBuff.CopyToAsync(keyOutputFile);
			}
			var csrs = new List<CertificateSigningRequest>();
			if (isSignerCert) {
				var signerCsr = CertificateSigningRequest.Generate(csrDn, primaryKeyPair, requestSubjectKeyIdentifier: true,
					requestAuthorityKeyIdentifier: true, requestKeyUsages: KeyUsages.KeyCertSign,
					requestCABasicConstraints: (true, 1));
				csrs.Add(signerCsr);
			}
			else {
				var encryptionCsr = CertificateSigningRequest.Generate(csrDn, primaryKeyPair, requestSubjectKeyIdentifier: true,
					requestAuthorityKeyIdentifier: true, requestKeyUsages: KeyUsages.KeyEncipherment,
					requestCABasicConstraints: (false, null));
				csrs.Add(encryptionCsr);
				var authCsr = CertificateSigningRequest.Generate(csrDn, authKeyPair, requestSubjectKeyIdentifier: true,
					requestAuthorityKeyIdentifier: true, requestKeyUsages: KeyUsages.DigitalSignature,
					requestCABasicConstraints: (false, null));
				csrs.Add(authCsr);
			}
			using (var csrOutputFile = File.Create(csrOutputPath, 4096, FileOptions.Asynchronous)) {
				using var pemBuff = new MemoryStream();
				using (var pemWriter = new StreamWriter(pemBuff, leaveOpen: true)) {
					foreach (var csr in csrs) {
						csr.StoreToPem(pemWriter);
					}
				}
				pemBuff.Position = 0;
				await pemBuff.CopyToAsync(csrOutputFile);
			}
		}
		private Task SignCertificates(List<CertificateSigningRequest> csrs, string signerCaCertPath, string signerPrivateKeyPath, char[] signerPassphrase, DateTime validToDate, bool allowSignerCert, string certOutputPath) {
			throw new NotImplementedException();
		}

		private Task BuildKeyFile(string intermediateKeyLoadPath, string certificateInputPath, char[] keyFilePassphrase, string keyFileOutputPath) {
			throw new NotImplementedException();
		}
	}
}
