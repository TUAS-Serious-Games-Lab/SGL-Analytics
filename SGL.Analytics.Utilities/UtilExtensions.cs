using System.Collections.Generic;
using System.IO;

namespace SGL.Analytics.Utilities {
	public static class UtilExtensions {
		public static IEnumerable<string> EnumerateLines(this TextReader reader) {
			string? line;
			while ((line = reader.ReadLine()) != null) {
				yield return line;
			}
		}
	}
}
