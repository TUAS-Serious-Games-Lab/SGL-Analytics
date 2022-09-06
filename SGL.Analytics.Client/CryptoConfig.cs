using SGL.Utilities.Crypto.EndToEnd;

namespace SGL.Analytics.Client {
	internal class CryptoConfig {
		public bool AllowSharedMessageKeyPair { get; internal set; }
		public DataEncryptionMode DataEncryptionMode { get; internal set; }
	}
}