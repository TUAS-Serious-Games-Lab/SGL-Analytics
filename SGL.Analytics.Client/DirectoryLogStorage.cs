using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public class DirectoryLogStorage : ILogStorage {
		private string directory;

		public string FileSuffix { get; set; } = ".log";
		public bool Archiving { get; set; } = false;

		public DirectoryLogStorage(string directory) {
			this.directory = directory;
		}

		public class LogFile : ILogStorage.ILogFile {
			private DirectoryLogStorage storage;
			public Guid ID { get; private set; }
			public DateTime CreationTime => File.GetCreationTime(FullFileName);
			public DateTime EndTime => File.GetLastWriteTime(FullFileName);

			public string FullFileName => Path.Combine(storage.directory, ID.ToString() + storage.FileSuffix);

			public LogFile(Guid id, DirectoryLogStorage storage) {
				this.storage = storage;
				ID = id;
			}

			public Stream OpenRead() {
				return new FileStream(FullFileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
			}

			public void Remove() {
				if (storage.Archiving) {
					File.Move(FullFileName, Path.Combine(storage.directory, "archive", ID.ToString() + storage.FileSuffix));
				}
				else {
					File.Delete(FullFileName);
				}
			}
		}

		public (Stream, ILogStorage.ILogFile) CreateLogFile(string appID) {
			var id = Guid.NewGuid();
			var logFile = new LogFile(id, this);
			var writeStream = new FileStream(logFile.FullFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
			return (writeStream, logFile);
		}

		public IEnumerable<ILogStorage.ILogFile> EnumerateLogs(string appID) {
			return from filename in Directory.EnumerateFiles(directory, "*" + FileSuffix)
				   let idString = Path.GetFileNameWithoutExtension(filename)
				   let id = Guid.TryParse(idString, out var guid) ? guid : (Guid?)null
				   where id.HasValue
				   select new LogFile(id.Value, this);
		}
	}
}
