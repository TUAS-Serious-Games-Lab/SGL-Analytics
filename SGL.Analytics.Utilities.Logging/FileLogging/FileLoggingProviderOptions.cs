using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities.Logging.FileLogging {

	public class FileLoggingSinkOptions {
		public string BaseDirectory { get; set; } = Path.Combine(Environment.CurrentDirectory, "logs");
		public string FilenameFormat { get; set; } = "{Time:yyyy}/{Time:yyyyMMdd}_{AppDomainName}.log";
		public string MessageFormat { get; set; } = "[{Time:O}] [{Level}] [{Category}] {Text}";
		public string MessageFormatException { get; set; } = "[{Time:O}] [{Level}] [{Category}] {Text}\n=> {Exception}";
		public int MaxOpenStreams { get; set; } = 16;
	}

	public class FileLoggingProviderOptions {
		public List<FileLoggingSinkOptions> Sinks { get; set; } = new();
		public Dictionary<string, string> Constants { get; set; } = new();
	}
}
