using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SGL.Analytics.Client {
	public class DirectoryLogStorage : ILogStorage {
		private string directory;
		private bool useCompressedFiles = true;

		public bool UseCompressedFiles {
			get => useCompressedFiles;
			set {
				useCompressedFiles = value;
				FileSuffix = useCompressedFiles ? ".log.gz" : ".log";
			}
		}
		public string FileSuffix { get; set; } = ".log.gz";
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
				var fileStream = OpenReadRaw();
				if (storage.UseCompressedFiles) {
					return new GZipStream(fileStream, CompressionMode.Decompress);
				}
				else {
					return fileStream;
				}
			}

			public Stream OpenReadRaw() => new FileStream(FullFileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

			public void Remove() {
				if (storage.Archiving) {
					File.Move(FullFileName, Path.Combine(storage.directory, "archive", ID.ToString() + storage.FileSuffix));
				}
				else {
					File.Delete(FullFileName);
				}
			}

			public bool Equals(ILogStorage.ILogFile? other) => other is LogFile lfo ? (ID == other.ID && storage.directory == lfo.storage.directory) : false;
		}

		public Stream CreateLogFile(out ILogStorage.ILogFile logFileMetadata) {
			var id = Guid.NewGuid();
			var logFile = new LogFile(id, this);
			logFileMetadata = logFile;
			var fileStream = new FileStream(logFile.FullFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
			if (UseCompressedFiles) {
				return new GZipStream(fileStream, CompressionLevel.Optimal);
			}
			else {
				return fileStream;
			}
		}

		private string getFilename(string path) {
			path = Path.GetFileName(path);
			if (path.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase)) {
				return path.Remove(path.Length - FileSuffix.Length);
			}
			else {
				return path;
			}
		}
		public IEnumerable<ILogStorage.ILogFile> EnumerateLogs() => from file in (from filename in Directory.EnumerateFiles(directory, "*" + FileSuffix)
																				  let idString = getFilename(filename)
																				  let id = Guid.TryParse(idString, out var guid) ? guid : (Guid?)null
																				  where id.HasValue
																				  select new LogFile(id.Value, this))
																	orderby file.CreationTime
																	select file;
		// TODO: Add EnumerateFinishedLogs to filter out log files that are currently open for writing, so we don't attempt to upload unfinished logs.
	}
}
