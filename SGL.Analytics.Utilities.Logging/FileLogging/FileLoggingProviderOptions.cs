using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities.Logging.FileLogging {

	public class FileLoggingSinkOptions {
		public string BaseDirectory { get; set; } = Path.Combine(Environment.CurrentDirectory, "logs");
		public List<string> SubdirectoryComponents { get; set; } = new();
		public List<string> FilenameComponents { get; set; } = new();
		public string FilenameSuffix { get; set; } = ".log";
	}

	public class FileLoggingProviderOptions {
		public List<FileLoggingSinkOptions> Sinks { get; set; } = new();
	}
}
