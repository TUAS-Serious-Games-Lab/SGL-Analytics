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
}
