using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class InMemoryLogStorage : ILogStorage {
		public class LogFile : ILogStorage.ILogFile {
			private InMemoryLogStorage storage;
			private MemoryStream content = new();

			public Guid ID { get; private set; }

			public DateTime CreationTime { get; } = DateTime.Now;

			// TODO: Update when write stream for content is disposed.
			public DateTime EndTime { get; set; } = DateTime.Now;

			public MemoryStream Content => content;

			internal LogFile(InMemoryLogStorage storage, Guid id) {
				this.storage = storage;
				ID = id;
			}

			public bool Equals(ILogStorage.ILogFile? other) => other is LogFile lfo ? (ID == other.ID && storage == lfo.storage) : false;

			public Stream OpenRead() {
				content.Position = 0;
				return content;
			}

			public void Remove() {
				storage.logs.Remove(this);
			}
		}

		private List<LogFile> logs = new();

		public Stream CreateLogFile(out ILogStorage.ILogFile logFileMetadata) {
			var log = new LogFile(this, Guid.NewGuid());
			logs.Add(log);
			logFileMetadata = log;
			return log.Content;
		}

		public IEnumerable<ILogStorage.ILogFile> EnumerateLogs() {
			return logs;
		}
	}
}
