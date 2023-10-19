using SGL.Analytics.DTO;
using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;


namespace SGL.Analytics.Client {
	/// <summary>
	/// An implementation of <see cref="ILogStorage"/> that used a directory on the local filesystem to store the logs as files.
	/// It uses the filename to associate the <see cref="ILogStorage.ILogFile.ID"/>, and the file timestamps to store the <see cref="ILogStorage.ILogFile.CreationTime"/> and
	/// <see cref="ILogStorage.ILogFile.EndTime"/> and thus doesn't need a separate metadata storage.
	/// The implementations also supports optional compression of the files as well as archiving locally removed files in a separate directory.
	/// </summary>
	public class DirectoryLogStorage : ILogStorage {
		private string directory;
		private bool useCompressedFiles = true;
		private List<Guid> logFilesOpenForWriting = new();
		private string fileSuffix = ".log.gz";

		/// <summary>
		/// Specifies whether the log files should be compressed.
		/// This property must not be changed during normal operation but only when no <see cref="SglAnalytics"/> object uses this object.
		/// Changing it while a <see cref="SglAnalytics"/> is using it can cause problems with files not being found or listed correctly, depending on when the change happens.
		/// </summary>
		public bool UseCompressedFiles {
			get => useCompressedFiles;
			set {
				useCompressedFiles = value;
				FileSuffix = useCompressedFiles ? ".log.gz" : ".log";
			}
		}
		/// <summary>
		/// Specifies the currently used filename suffix for the stored log files.
		/// This property must not be changed during normal operation but only when no <see cref="SglAnalytics"/> object uses this object.
		/// Changing it while a <see cref="SglAnalytics"/> is using it can cause problems with files not being found or listed correctly, depending on when the change happens.
		/// </summary>
		public string FileSuffix {
			get => fileSuffix;
			set {
				var vc = new ValidationContext(this);
				vc.DisplayName = vc.MemberName = nameof(FileSuffix);
				Validator.ValidateValue(value, vc, new ValidationAttribute[] { new PlainNameAttribute(), new StringLengthAttribute(16) });
				fileSuffix = value;
			}
		}
		/// <summary>
		/// Specifies whether removed files are archived in an <c>archive</c> subdirectory, otherwise they are actually deleted.
		/// </summary>
		public bool Archiving { get; set; } = false;

		/// <summary>
		/// Instantiates the log storage using the given directory.
		/// </summary>
		/// <param name="directory">The path of the directory in which the logs shall be stored.</param>
		public DirectoryLogStorage(string directory) {
			this.directory = directory;
			Directory.CreateDirectory(directory);
		}

		private class StreamWrapper : Stream {
			private Stream wrapped;
			private DirectoryLogStorage? storage;
			private LogFile logObject;

			public StreamWrapper(Stream wrapped, DirectoryLogStorage storage, LogFile logObject) {
				this.wrapped = wrapped;
				this.storage = storage;
				this.logObject = logObject;
			}

			public override bool CanRead => wrapped.CanRead;

			public override bool CanSeek => wrapped.CanSeek;

			public override bool CanWrite => wrapped.CanWrite;

			public override long Length => wrapped.Length;

			public override long Position { get => wrapped.Position; set => wrapped.Position = value; }

			public override async ValueTask DisposeAsync() {
				storage?.logFilesOpenForWriting?.Remove(logObject.ID);
				await wrapped.DisposeAsync();
				if (storage != null) {
					File.SetLastWriteTime(logObject.FullFileName, DateTime.Now);
				}
				storage = null;
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
				storage?.logFilesOpenForWriting?.Remove(logObject.ID);
				wrapped.Dispose();
				if (storage != null && disposing) {
					File.SetLastWriteTime(logObject.FullFileName, DateTime.Now);
				}
				if (disposing) storage = null;
			}
		}

		private class LogFile : ILogStorage.ILogFile {
			private readonly DateTime? creationTime;
			private DirectoryLogStorage storage;
			public Guid ID { get; private set; }
			public DateTime CreationTime => (creationTime ?? File.GetCreationTimeUtc(FullFileName)).ToLocalTime();
			public DateTime EndTime => File.GetLastWriteTime(FullFileName);

			public string FullFileName => Path.Combine(storage.directory, creationTime.HasValue ?
				$"{ID}_{(long)(creationTime.Value.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds}{storage.FileSuffix}" :
				$"{ID}{storage.FileSuffix}");

			public string Suffix => storage.FileSuffix;

			public LogContentEncoding Encoding => storage.UseCompressedFiles ? LogContentEncoding.GZipCompressed : LogContentEncoding.Plain;

			public LogFile(Guid id, DateTime? creationTime, DirectoryLogStorage storage) {
				this.storage = storage;
				ID = id;
				this.creationTime = creationTime;
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
					var targetDir = Path.Combine(storage.directory, "archive");
					Directory.CreateDirectory(targetDir);
					File.Move(FullFileName, Path.Combine(targetDir, ID.ToString() + storage.FileSuffix));
				}
				else {
					File.Delete(FullFileName);
				}
			}

			public bool Equals(ILogStorage.ILogFile? other) => other is LogFile lfo ? (ID == other.ID && storage.directory == lfo.storage.directory) : false;
		}

		/// <inheritdoc/>
		public Stream CreateLogFile(out ILogStorage.ILogFile logFileMetadata) {
			var id = Guid.NewGuid();
			var logFile = new LogFile(id, DateTime.UtcNow, this);
			// Before creating file, mark it as open for writing to prevent time window where
			// the syscall for creation is done but the file is not yet marked:
			logFilesOpenForWriting.Add(logFile.ID);
			var fileStream = new FileStream(logFile.FullFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
			logFileMetadata = logFile;
			if (UseCompressedFiles) {
				return new StreamWrapper(new GZipStream(fileStream, CompressionLevel.Optimal), this, logFile);
			}
			else {
				return new StreamWrapper(fileStream, this, logFile);
			}
		}

		private (Guid? id, DateTime? creationTime) getFilenameMeta(string path) {
			path = Path.GetFileName(path);
			if (path.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase)) {
				var baseName = path.Remove(path.Length - FileSuffix.Length);
				var sepPos = baseName.IndexOf("_");
				DateTime? creationTime = null;
				if (sepPos > 0) {
					var idString = baseName.Substring(0, sepPos);
					var creationTimeString = baseName.Substring(sepPos + 1);
					if (long.TryParse(creationTimeString, out var creationTimeStamp)) {
						creationTime = DateTime.UnixEpoch.AddSeconds(creationTimeStamp);
					}
					baseName = idString;
				}
				if (Guid.TryParse(baseName, out var id)) {
					return (id, creationTime);
				}
				else {
					return (null, creationTime);
				}
			}
			else {
				return (null, null);
			}
		}
		/// <summary>
		/// Enumerates all log files in the directory with the currently set <see cref="FileSuffix"/>.
		/// </summary>
		/// <returns>An enumerable to iterate over the logs.</returns>
		public IEnumerable<ILogStorage.ILogFile> EnumerateLogs() => from file in (from filename in Directory.EnumerateFiles(directory, "*" + FileSuffix)
																				  let fileNameMeta = getFilenameMeta(filename)
																				  let id = fileNameMeta.id
																				  let creationTime = fileNameMeta.creationTime
																				  where id.HasValue
																				  select new LogFile(id.Value, creationTime, this))
																	orderby file.CreationTime
																	select file;

		/// <summary>
		/// Enumerates the log files in the directory with the currently set <see cref="FileSuffix"/>, excluding those that are currently open for writing by this object.
		/// Note that simlutaneous access by multiple processes or by multiple <see cref="DirectoryLogStorage"/> in the same process to the directory is not supported and
		/// thus, files open for writing by other processes can not be properly excluded.
		/// </summary>
		/// <returns>An enumerable to iterate over the logs.</returns>
		public IEnumerable<ILogStorage.ILogFile> EnumerateFinishedLogs() => EnumerateLogs().Where(log => !logFilesOpenForWriting.Contains(log.ID));
	}
}
