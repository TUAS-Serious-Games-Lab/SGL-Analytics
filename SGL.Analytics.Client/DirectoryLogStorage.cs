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
		private List<Guid> logFilesOpenForWriting = new();

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
			Directory.CreateDirectory(directory);
		}

		private class StreamWrapper : Stream {
			private Stream wrapped;
			private DirectoryLogStorage? storage;
			private Guid logId;

			public StreamWrapper(Stream wrapped, DirectoryLogStorage storage, Guid logId) {
				this.wrapped = wrapped;
				this.storage = storage;
				this.logId = logId;
			}

			public override bool CanRead => wrapped.CanRead;

			public override bool CanSeek => wrapped.CanSeek;

			public override bool CanWrite => wrapped.CanWrite;

			public override long Length => wrapped.Length;

			public override long Position { get => wrapped.Position; set => wrapped.Position = value; }

			public override ValueTask DisposeAsync() {
				storage?.logFilesOpenForWriting?.Remove(logId);
				storage = null;
				return wrapped.DisposeAsync();
			}

			public override void Flush() {
				wrapped.Flush();
			}

			public override int Read(byte[] buffer, int offset, int count) {
				return wrapped.Read(buffer, offset, count);
			}

			public override long Seek(long offset, SeekOrigin origin) {
				return wrapped.Seek(offset, origin);
			}

			public override void SetLength(long value) {
				wrapped.SetLength(value);
			}

			public override void Write(byte[] buffer, int offset, int count) {
				wrapped.Write(buffer, offset, count);
			}

			protected override void Dispose(bool disposing) {
				storage?.logFilesOpenForWriting?.Remove(logId);
				if (disposing) storage = null;
				wrapped.Dispose();
			}
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
			logFilesOpenForWriting.Add(logFile.ID);
			var fileStream = new FileStream(logFile.FullFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
			if (UseCompressedFiles) {
				return new StreamWrapper(new GZipStream(fileStream, CompressionLevel.Optimal), this, id);
			}
			else {
				return new StreamWrapper(fileStream, this, id);
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

		public IEnumerable<ILogStorage.ILogFile> EnumerateFinishedLogs() => EnumerateLogs().Where(log => !logFilesOpenForWriting.Contains(log.ID));
	}
}
