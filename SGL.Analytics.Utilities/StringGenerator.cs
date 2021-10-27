using System;
using System.Linq;

namespace SGL.Analytics.Utilities {
	/// <summary>
	/// A simple utility class to generate pseudo-random strings.
	/// Note: The randomness used by this class is NOT cryptographically secure true randomness but just from a regular PRNG.
	/// If true randomness is needed, see <see cref="SecretGenerator"/>.
	/// </summary>
	public static class StringGenerator {
		private static Random rnd = new Random();
		private static char[] wordCharacters = Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26)).Concat(Enumerable.Range('0', 10)).Select(c => (char)c).ToArray();
		private static char[] charactersWithSpace = Enumerable.Range('A', 26).Concat(Enumerable.Range('a', 26)).Concat(Enumerable.Range('0', 10)).Append(' ').Select(c => (char)c).ToArray();

		/// <summary>
		/// Generates a random string of the given length, consisting of lower-case letters, upper-case letters, digits and spaces.
		/// </summary>
		/// <param name="length">The number of characters to generate.</param>
		/// <returns>The generated string.</returns>
		public static string GenerateRandomString(int length) {
			return new string(Enumerable.Range(0, length).Select(_ => charactersWithSpace[rnd.Next(charactersWithSpace.Length)]).ToArray());
		}
		/// <summary>
		/// Generates a random string of the given length, consisting of lower-case letters, upper-case letters, and digits, but no spaces.
		/// </summary>
		/// <param name="length">The number of characters to generate.</param>
		/// <returns>The generated string.</returns>
		public static string GenerateRandomWord(int length) {
			return new string(Enumerable.Range(0, length).Select(_ => wordCharacters[rnd.Next(wordCharacters.Length)]).ToArray());
		}
	}
}
