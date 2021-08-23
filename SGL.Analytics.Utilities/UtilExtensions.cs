using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit.Abstractions;

namespace SGL.Analytics.Utilities {
	public static class UtilExtensions {
		public static IEnumerable<string> EnumerateLines(this TextReader reader) {
			string? line;
			while ((line = reader.ReadLine()) != null) {
				yield return line;
			}
		}
		public static void WriteStreamContents(this ITestOutputHelper output, Stream textStream) {
			using (var rdr = new StreamReader(textStream, leaveOpen: true)) {
				foreach (var line in rdr.EnumerateLines()) {
					output.WriteLine(line);
				}
			}
		}

	}
}
