using System.Collections.Generic;
using System.IO;

namespace SGL.Analytics.Utilities {
	/// <summary>
	/// Provides miscellaneous utility extension methods.
	/// </summary>
	public static class UtilExtensions {
		/// <summary>
		/// Consumes the <see cref="TextReader"/> as an <see cref="IEnumerable{T}"/> of lines in string form.
		/// </summary>
		/// <param name="reader">The reader from which to enumerate lines.</param>
		/// <returns>An enumerable, enumerating over all lines from <c>reader</c>.</returns>
		public static IEnumerable<string> EnumerateLines(this TextReader reader) {
			string? line;
			while ((line = reader.ReadLine()) != null) {
				yield return line;
			}
		}
	}
}
