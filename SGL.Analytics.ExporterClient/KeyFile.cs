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
	/// <summary>
	/// Encalpsulates the cryptographic objects in a recipient user key file and the funtionality for loading such files.
	/// </summary>
	public class KeyFile {
		/// <summary>
		/// The certificate attesting the purpose of <see cref="AuthenticationKeyPair"/>.
		/// </summary>
		public Certificate AuthenticationCertificate { get; set; }
		/// <summary>
		/// The certificate attesting the purpose of <see cref="RecipientKeyPair"/>.
		/// </summary>
		public Certificate RecipientCertificate { get; set; }
		/// <summary>
		/// The asymmetric key pair used for authenticating the user against the backend API using a signature challenge.
		/// </summary>
		public KeyPair AuthenticationKeyPair { get; set; }
		/// <summary>
		/// The asymmetric key pair used for decrypting data keys of end-to-end encrypted data objects.
		/// </summary>
		public KeyPair RecipientKeyPair { get; set; }
		/// <summary>
		/// The key id of <see cref="AuthenticationKeyPair"/>.
		/// </summary>
		public KeyId AuthenticationKeyId { get; set; }
		/// <summary>
		/// The key id of <see cref="RecipientKeyPair"/>.
		/// </summary>
		public KeyId RecipientKeyId { get; set; }

		private KeyFile(Certificate authenticationCertificate, Certificate recipientCertificate, KeyPair authenticationKeyPair, KeyPair recipientKeyPair, KeyId authenticationKeyId, KeyId recipientKeyId) {
			AuthenticationCertificate = authenticationCertificate;
			RecipientCertificate = recipientCertificate;
			AuthenticationKeyPair = authenticationKeyPair;
			RecipientKeyPair = recipientKeyPair;
			AuthenticationKeyId = authenticationKeyId;
			RecipientKeyId = recipientKeyId;
		}

		/// <summary>
		/// An overload of <see cref="Load(TextReader, string, Func{char[]}, ILogger, CancellationToken)"/> without the logger.
		/// </summary>
		public static KeyFile Load(TextReader reader, string sourceName, Func<char[]> getPassword, CancellationToken ct = default) =>
			Load(reader, sourceName, getPassword, NullLogger.Instance, ct);
		/// <summary>
		/// Synchronously loads key file data from PEM data in <paramref name="reader"/>.
		/// </summary>
		/// <param name="reader">A reader providing the encoded PEM data.</param>
		/// <param name="sourceName">A name referring to <paramref name="reader"/>, e.g. the name of the file that <paramref name="reader"/> reads.</param>
		/// <param name="getPassword">A function object used to obtain the password / passphrase for encrypted private keys.</param>
		/// <param name="logger">A logger object used for logging problems during the loading process.</param>
		/// <param name="ct">An optional cancellation token to allow cancelling the operation. </param>
		/// <returns>An object containing the mapped cryptographic objects in the key file.</returns>
		/// <exception cref="KeyFileException">If the key file couldn't be loaded correctly.</exception>
		public static KeyFile Load(TextReader reader, string sourceName, Func<char[]> getPassword, ILogger logger, CancellationToken ct = default) {
			var pemReader = new PemObjectReader(reader, getPassword);
			var readData = InnerLoad(pemReader, sourceName, logger, ct);
			return new KeyFile(readData.AuthenticationCertificate, readData.RecipientCertificate, readData.AuthenticationKeyPair, readData.RecipientKeyPair, readData.AuthenticationKeyId, readData.RecipientKeyId);
		}

		/// <summary>
		/// An overload of <see cref="LoadAsync(TextReader, string, Func{char[]}, ILogger, CancellationToken)"/> without the logger.
		/// </summary>
		public static Task<KeyFile> LoadAsync(TextReader reader, string sourceName, Func<char[]> getPassword, CancellationToken ct = default) =>
			LoadAsync(reader, sourceName, getPassword, NullLogger.Instance, ct);
		/// <summary>
		/// Asynchronously loads key file data from PEM data in <paramref name="reader"/>.
		/// </summary>
		/// <param name="reader">A reader providing the encoded PEM data.</param>
		/// <param name="sourceName">A name referring to <paramref name="reader"/>, e.g. the name of the file that <paramref name="reader"/> reads.</param>
		/// <param name="getPassword">A function object used to obtain the password / passphrase for encrypted private keys.</param>
		/// <param name="logger">A logger object used for logging problems during the loading process.</param>
		/// <param name="ct">An optional cancellation token to allow cancelling the operation. </param>
		/// <returns>An object containing the mapped cryptographic objects in the key file.</returns>
		/// <exception cref="KeyFileException">If the key file couldn't be loaded correctly.</exception>
		public static async Task<KeyFile> LoadAsync(TextReader reader, string sourceName, Func<char[]> getPassword, ILogger logger, CancellationToken ct = default) {
			var pemReader = new PemObjectReader(reader, getPassword);
			var readData = await Task.Run(() => InnerLoad(pemReader, sourceName, logger, ct)).ConfigureAwait(false);
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
