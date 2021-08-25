using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit.Abstractions;

namespace SGL.Analytics.Utilities {
	public static class StringGenerator {
		private static Random rnd = new Random();
		private static char[] characters = Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26)).Concat(Enumerable.Range('0', 10)).Append(' ').Select(c => (char)c).ToArray();

		public static string GenerateRandomString(int length) {
			return new string(Enumerable.Range(0, length).Select(_ => characters[rnd.Next(characters.Length)]).ToArray());
		}
	}
}
