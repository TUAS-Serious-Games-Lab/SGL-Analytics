using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
	public class SecretGenerator {
		private RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
		private object lockObject = new object();
		private SecretGenerator() { }

		public static SecretGenerator Instance = new SecretGenerator();

		public string GenerateSecret(int bytes) {
			byte[] buff = new byte[bytes];
			lock (lockObject) {
				rngCsp.GetBytes(buff);
			}
			return Convert.ToBase64String(buff);
		}
	}
}
