using System;
using System.Linq;

namespace SGL.Analytics.Utilities {
	public static class StringGenerator {
		private static Random rnd = new Random();
		private static char[] wordCharacters = Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26)).Concat(Enumerable.Range('0', 10)).Select(c => (char)c).ToArray();
		private static char[] charactersWithSpace = Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26)).Concat(Enumerable.Range('0', 10)).Append(' ').Select(c => (char)c).ToArray();

		public static string GenerateRandomString(int length) {
			return new string(Enumerable.Range(0, length).Select(_ => charactersWithSpace[rnd.Next(charactersWithSpace.Length)]).ToArray());
		}
		public static string GenerateRandomWord(int length) {
			return new string(Enumerable.Range(0, length).Select(_ => wordCharacters[rnd.Next(wordCharacters.Length)]).ToArray());
		}
	}
}
