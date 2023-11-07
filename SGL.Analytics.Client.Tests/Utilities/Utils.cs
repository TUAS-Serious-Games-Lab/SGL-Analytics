using SGL.Utilities;
using System.IO;
using Xunit.Abstractions;

namespace SGL.Analytics.Client.Tests {
	public static class UtilExtensions {
		public static void WriteLogContents(this ITestOutputHelper output, ILogStorage.ILogFile logFile) {
			using (var rdr = new StreamReader(logFile.OpenReadContent())) {
				foreach (var line in rdr.EnumerateLines()) {
					output.WriteLine(line);
				}
			}
		}
	}
}
