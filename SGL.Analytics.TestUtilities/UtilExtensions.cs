using SGL.Analytics.Utilities;
using System.IO;
using Xunit.Abstractions;

namespace SGL.Analytics.TestUtilities {
	public static class UtilExtensions {
		public static void WriteStreamContents(this ITestOutputHelper output, Stream textStream) {
			using (var rdr = new StreamReader(textStream, leaveOpen: true)) {
				foreach (var line in rdr.EnumerateLines()) {
					output.WriteLine(line);
				}
			}
		}

	}
}
