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
		private static async Task GenerateKeyAndCsr(string intermediateKeyPath, string csrOutputPath, bool isSignerCert, KeyType keyType, string? ellipticCurveName, int selectedRsaKeyStrength, string passphrase, DistinguishedName csrDn, bool storeUnencrypted) {
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
					primaryKeyPair.StoreToPem(pemWriter, storeUnencrypted ? PemEncryptionMode.UNENCRYPTED : PemEncryptionMode.AES_256_CBC, passphrase.ToCharArray(), random);
					if (!isSignerCert) {
						authKeyPair.StoreToPem(pemWriter, storeUnencrypted ? PemEncryptionMode.UNENCRYPTED : PemEncryptionMode.AES_256_CBC, passphrase.ToCharArray(), random);
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
		private async Task SignCertificates(List<CertificateSigningRequest> csrs, string signerCaCertPath, string signerPrivateKeyPath,
				char[] signerPassphrase, DateTime validToDate, bool allowSignerCert, bool selfSign, string certOutputPath) {
			Certificate signerCert = null!;
			if (!selfSign) {
				using (var caCertFile = File.OpenRead(signerCaCertPath)) {
					using var pemBuffer = new MemoryStream();
					await caCertFile.CopyToAsync(pemBuffer);
					pemBuffer.Position = 0;
					using var pemBufferReader = new StreamReader(pemBuffer, leaveOpen: true);
					var loadedCerts = Certificate.LoadAllFromPem(pemBufferReader).ToList();
					signerCert = loadedCerts.FirstOrDefault(cert =>
							cert.CABasicConstraints.HasValue && cert.CABasicConstraints.Value.IsCA &&
							cert.AllowedKeyUsages.HasValue && cert.AllowedKeyUsages.Value.HasFlag(KeyUsages.KeyCertSign)) ??
							throw new Exception($"No CA certificate found in file {Path.GetFileName(signerCaCertPath)}.");
				}
			}
			var signerKeyIdFromCert = !selfSign ? signerCert.PublicKey.CalculateId() : default;
			KeyPair signerKeyPair;
			using (var signerPrivateKeyFile = File.OpenRead(signerPrivateKeyPath)) {
				using var pemBuffer = new MemoryStream();
				await signerPrivateKeyFile.CopyToAsync(pemBuffer);
				pemBuffer.Position = 0;
				using var pemBufferReader = new StreamReader(pemBuffer, leaveOpen: true);
				var pemReader = new PemObjectReader(pemBufferReader, () => signerPassphrase);
				var pemObjects = pemReader.ReadAllObjects().ToList();
				var keyPairs = pemObjects.OfType<KeyPair>().Concat(pemObjects.OfType<PrivateKey>().Select(pk => pk.DeriveKeyPair()));
				signerKeyPair = keyPairs.FirstOrDefault(kp => selfSign || kp.Public.CalculateId() == signerKeyIdFromCert) ??
					throw new Exception($"No matching key for loaded CA certificate was found in {Path.GetFileName(signerPrivateKeyPath)}");
			}
			CsrSigningPolicy policy = new CsrSigningPolicy();
			policy.UseFixedValidityPeriod(DateTime.UtcNow, validToDate);
			policy.ForceKeyIdentifiers();
			if (allowSignerCert) {
				policy.AllowExtensionRequestsForCA();
			}
			List<Certificate> certs;
			if (selfSign) {
				var validityPeriod = policy.GetValidityPeriod();
				certs = csrs.Select(csr => Certificate.Generate(csr.SubjectDN, signerKeyPair.Private, csr.SubjectDN, signerKeyPair.Public,
						validityPeriod.From, validityPeriod.To, policy.GetSerialNumber(), policy.GetSignatureDigest(),
						policy.ShouldGenerateAuthorityKeyIdentifier(csr.RequestedAuthorityKeyIdentifier) ?
							new KeyIdentifier(signerKeyPair.Public) : null,
						policy.ShouldGenerateSubjectKeyIdentifier(csr.RequestedSubjectKeyIdentifier),
						policy.AcceptedKeyUsages(csr.RequestedKeyUsages) ?? KeyUsages.NoneDefined,
						policy.AcceptedCAConstraints(csr.RequestedCABasicConstraints))).ToList();
			}
			else {
				certs = csrs.Select(csr => csr.GenerateCertificate(signerCert, signerKeyPair, policy)).ToList();
			}
			using var outputPemBuffer = new MemoryStream();
			using (var pemBufferWriter = new StreamWriter(outputPemBuffer, leaveOpen: true)) {
				foreach (var cert in certs) {
					cert.StoreToPem(pemBufferWriter);
				}
			}
			outputPemBuffer.Position = 0;
			using (var certOutputFile = File.Create(certOutputPath)) {
				await outputPemBuffer.CopyToAsync(certOutputFile);
			}
		}

		private async Task BuildKeyFile(string intermediateKeyLoadPath, string certificateInputPath, char[] keyFilePassphrase, string keyFileOutputPath) {
			List<Certificate> certs;
			using (var certFile = File.OpenRead(certificateInputPath)) {
				using var pemBuffer = new MemoryStream();
				await certFile.CopyToAsync(pemBuffer);
				pemBuffer.Position = 0;
				using var pemBufferReader = new StreamReader(pemBuffer, leaveOpen: true);
				certs = Certificate.LoadAllFromPem(pemBufferReader).ToList();
			}
			List<KeyPair> keyPairs;
			using (var intermediateKeyFile = File.OpenRead(intermediateKeyLoadPath)) {
				using var pemBuffer = new MemoryStream();
				await intermediateKeyFile.CopyToAsync(pemBuffer);
				pemBuffer.Position = 0;
				using var pemBufferReader = new StreamReader(pemBuffer, leaveOpen: true);
				var pemReader = new PemObjectReader(pemBufferReader, () => keyFilePassphrase);
				var pemObjects = pemReader.ReadAllObjects().ToList();
				keyPairs = pemObjects.OfType<KeyPair>().Concat(pemObjects.OfType<PrivateKey>().Select(pk => pk.DeriveKeyPair())).ToList();
			}

			var mismatchedKeyPair = keyPairs.FirstOrDefault(kp => !certs.Any(cert => cert.PublicKey.Equals(kp.Public)));
			if (mismatchedKeyPair != null) {
				throw new ArgumentException($"The certificate file contains no certificate for key {mismatchedKeyPair.Public.CalculateId()}.");
			}
			var mismatchedCert = certs.FirstOrDefault(cert => !keyPairs.Any(kp => cert.PublicKey.Equals(kp.Public)));
			if (mismatchedCert != null) {
				throw new ArgumentException($"The intermediate key file contains no private key for certificate {mismatchedCert.SubjectDN}. Id of missing key: {mismatchedCert.PublicKey.CalculateId()}");
			}

			using var outputPemBuffer = new MemoryStream();
			var random = new RandomGenerator();
			using (var pemBufferWriter = new StreamWriter(outputPemBuffer, leaveOpen: true)) {
				foreach (var cert in certs) {
					cert.StoreToPem(pemBufferWriter);
				}
				foreach (var keyPair in keyPairs) {
					if (keyFilePassphrase.Length == 0) {
						keyPair.StoreToPem(pemBufferWriter, PemEncryptionMode.UNENCRYPTED, keyFilePassphrase, random);
					}
					else {
						keyPair.StoreToPem(pemBufferWriter, PemEncryptionMode.AES_256_CBC, keyFilePassphrase, random);
					}
				}
			}
			outputPemBuffer.Position = 0;
			using (var keyOutputFile = File.Create(keyFileOutputPath)) {
				await outputPemBuffer.CopyToAsync(keyOutputFile);
			}
		}
	}
}
