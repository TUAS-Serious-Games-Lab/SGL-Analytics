using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public interface ILogFileSink {
		Task ProcessLogFileAsync(LogFileMetadata metadata, Stream content, CancellationToken ct);
	}
}
