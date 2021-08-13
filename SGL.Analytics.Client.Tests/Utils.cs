using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public static class UtilExtensions {
		public static IEnumerable<string> EnumerateLines(this TextReader reader) {
			string? line;
			while ((line = reader.ReadLine()) != null) {
				yield return line;
			}
		}

	}

	public static class StringGenerator {
		private static Random rnd = new Random();
		private static char[] characters = Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26)).Concat(Enumerable.Range('0', 10)).Append(' ').Select(c => (char)c).ToArray();

		public static string GenerateRandomString(int length) {
			return new string(Enumerable.Range(0, 256).Select(_ => characters[rnd.Next(characters.Length)]).ToArray());
		}
	}
}
