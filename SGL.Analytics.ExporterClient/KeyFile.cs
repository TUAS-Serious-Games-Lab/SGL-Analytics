using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SGL.Utilities.Crypto;
using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public class KeyFile {
		public Certificate AuthenticationCertificate { get; set; }
		public Certificate RecipientCertificate { get; set; }
		public KeyPair AuthenticationKeyPair { get; set; }
		public KeyPair RecipientKeyPair { get; set; }
		public KeyId AuthenticationKeyId { get; set; }
		public KeyId RecipientKeyId { get; set; }

		private KeyFile(Certificate authenticationCertificate, Certificate recipientCertificate, KeyPair authenticationKeyPair, KeyPair recipientKeyPair, KeyId authenticationKeyId, KeyId recipientKeyId) {
			AuthenticationCertificate = authenticationCertificate;
			RecipientCertificate = recipientCertificate;
			AuthenticationKeyPair = authenticationKeyPair;
			RecipientKeyPair = recipientKeyPair;
			AuthenticationKeyId = authenticationKeyId;
			RecipientKeyId = recipientKeyId;
		}

		public static KeyFile Load(TextReader reader, string sourceName, Func<char[]> getPassword, CancellationToken ct = default) =>
			Load(reader, sourceName, getPassword, NullLogger.Instance, ct);
		public static KeyFile Load(TextReader reader, string sourceName, Func<char[]> getPassword, ILogger logger, CancellationToken ct = default) {
			var pemReader = new PemObjectReader(reader, getPassword);
			var readData = InnerLoad(pemReader, sourceName, logger, ct);
			return new KeyFile(readData.AuthenticationCertificate, readData.RecipientCertificate, readData.AuthenticationKeyPair, readData.RecipientKeyPair, readData.AuthenticationKeyId, readData.RecipientKeyId);
		}

		public static Task<KeyFile> LoadAsync(TextReader reader, string sourceName, Func<char[]> getPassword, CancellationToken ct = default) =>
			LoadAsync(reader, sourceName, getPassword, NullLogger.Instance, ct);
		public static async Task<KeyFile> LoadAsync(TextReader reader, string sourceName, Func<char[]> getPassword, ILogger logger, CancellationToken ct = default) {
			var pemReader = new PemObjectReader(reader, getPassword);
			var readData = await Task.Run(() => InnerLoad(pemReader, sourceName, logger, ct));
			return new KeyFile(readData.AuthenticationCertificate, readData.RecipientCertificate, readData.AuthenticationKeyPair, readData.RecipientKeyPair, readData.AuthenticationKeyId, readData.RecipientKeyId);
		}

		private static (Certificate AuthenticationCertificate, Certificate RecipientCertificate, KeyPair AuthenticationKeyPair, KeyPair RecipientKeyPair,
		 KeyId AuthenticationKeyId, KeyId RecipientKeyId) InnerLoad(PemObjectReader reader, string sourceName, ILogger logger, CancellationToken ct = default) {
			List<object> pemObjects;
			ct.ThrowIfCancellationRequested();
			try {
				pemObjects = reader.ReadAllObjects().ToList();
			}
			catch (Exception ex) {
				logger.LogError(ex, "Failed to read PEM data from key file {src}.", sourceName);
				throw new KeyFileException($"Error while reading key file {sourceName}.", ex);
			}
			ct.ThrowIfCancellationRequested();
			var keyPairs = pemObjects.OfType<KeyPair>().Concat(pemObjects.OfType<PrivateKey>().Select(privKey => privKey.DeriveKeyPair())).ToDictionary(keyPair => keyPair.Public.CalculateId());
			var certificates = pemObjects.OfType<Certificate>().ToList();
			var authCert = certificates.FirstOrDefault(cert => cert.AllowedKeyUsages.GetValueOrDefault(KeyUsages.NoneDefined).HasFlag(KeyUsages.DigitalSignature));
			var recipientCert = certificates.FirstOrDefault(cert => cert.AllowedKeyUsages.GetValueOrDefault(KeyUsages.NoneDefined).HasFlag(KeyUsages.KeyEncipherment));
			if (authCert == null) {
				logger.LogError("Couldn't find a certificate suitable for authentication in key file {keyFile}. " +
					"The file needs to contain a certificate (and the associated key pair) with the DigitalSignature key usage extension.", sourceName);
				throw new KeyFileException($"The given key file {sourceName} does not contain a certificate with DigitalSignature key usage for authentication.");
			}
			if (recipientCert == null) {
				logger.LogError("Couldn't find a certificate suitable for decryption in key file {keyFile}. " +
					"The file needs to contain a certificate (and the associated key pair) with the KeyEncipherment key usage extension.", sourceName);
				throw new KeyFileException($"The given key file {sourceName} does not contain a certificate with KeyEncipherment key usage for decryption.");
			}
			ct.ThrowIfCancellationRequested();
			var authKeyId = authCert.PublicKey.CalculateId();
			var recipientKeyId = recipientCert.PublicKey.CalculateId();
			var authKeyPair = keyPairs.GetValueOrDefault(authKeyId);
			var recipientKeyPair = keyPairs.GetValueOrDefault(recipientKeyId);
			if (authKeyPair == null) {
				logger.LogError("Couldn't find the private key for the key {keyId} used by the authentication certificate in key file {keyFile}.", authKeyId, sourceName);
				throw new KeyFileException($"The given key file {sourceName} does not contain a key pair for the certififacte with DigitalSignature key usage for authentication.");
			}
			if (recipientKeyPair == null) {
				logger.LogError("Couldn't find the private key for the key {keyId} used by the decryption certificate in key file {keyFile}.", recipientKeyId, sourceName);
				throw new KeyFileException($"The given key file {sourceName} does not contain a key pair for the certificate with KeyEncipherment key usage for decryption.");
			}
			ct.ThrowIfCancellationRequested();
			return (authCert, recipientCert, authKeyPair, recipientKeyPair, authKeyId, recipientKeyId);
		}
	}
}
