using System;
using System.Collections.Generic;
using System.IO;

namespace SGL.Analytics.Client {
	public interface ILogStorage {
		public interface ILogFile : IEquatable<ILogFile> {
			public Guid ID { get; }
			public DateTime CreationTime { get; }
			public DateTime EndTime { get; }
			public Stream OpenRead();
			public Stream OpenReadRaw();
			public void Remove();
		}

		Stream CreateLogFile(out ILogFile logFileMetadata);
		IEnumerable<ILogFile> EnumerateLogs();
		IEnumerable<ILogFile> EnumerateFinishedLogs();
	}
}
