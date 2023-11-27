using SGL.Analytics.DTO;
using SGL.Utilities.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace SGL.Analytics.Client {
	/// <summary>
	/// An implementation of <see cref="ILogStorage"/> that uses a directory on the local filesystem to store the logs as files.
	/// It uses the filename to associate the <see cref="ILogStorage.ILogFile.ID"/>, and the file modification timestamp to store <see cref="ILogStorage.ILogFile.EndTime"/>.
	/// As not all platforms support a creation timestamp, <see cref="ILogStorage.ILogFile.CreationTime"/> is instead encoded as a decimal unix time in the filename.
	/// The <see cref="ILogStorage.ILogFile.Encoding"/> field is handled using different file suffixes.
	/// Thus, no separate metadata storage is needed.
	/// The implementation supports optional compression of the files (controlled by <see cref="CompressFiles"/>, on by default) 
	/// as well as archiving locally removed files in a separate directory (controlled by <see cref="Archiving"/>, mainly for testing and diagnostic purposes, off by default).
	/// </summary>
	public class DirectoryLogStorage : ILogStorage {
		private string directory;
		private object stateLock = new object();
		private HashSet<Guid> logFilesOpenForWriting = new();
		private string compressedFileSuffix = ".log.gz";
		private string uncompressedFileSuffix = ".log";
		private string unfinishedFileSuffix = ".log.pending";
		private object configLock = new object();
		private bool compressFiles = true;
		private bool archiving = false;

		/// <summary>
		/// Specifies whether the log files should be compressed.
		/// This property applies when a log file is finished, but <see cref="ListLogFiles"/> and <see cref="ListAllLogFiles"/> return both,
		/// compressed and uncompressed log files.
		/// </summary>
		public bool CompressFiles {
			get {
				lock (configLock) {
					return compressFiles;
				}
			}
			set {
				lock (configLock) {
					compressFiles = value;
				}
			}
		}
		/// <summary>
		/// Specifies the filename suffix for stored log files that are compressed, because they were stored with <see cref="CompressFiles"/> set to true.
		/// This property must not be changed during normal operation but only when no <see cref="SglAnalytics"/> object uses this object.
		/// Changing it while a <see cref="SglAnalytics"/> is using it can cause problems with files not being found or listed correctly, depending on when the change happens.
		/// </summary>
		public string CompressedFileSuffix {
			get => compressedFileSuffix;
			set {
				var vc = new ValidationContext(this);
				vc.DisplayName = vc.MemberName = nameof(CompressedFileSuffix);
				Validator.ValidateValue(value, vc, new ValidationAttribute[] { new PlainNameAttribute(), new StringLengthAttribute(16) });
				if (uncompressedFileSuffix.EndsWith(value, StringComparison.OrdinalIgnoreCase) || unfinishedFileSuffix.EndsWith(value, StringComparison.OrdinalIgnoreCase)) {
					throw new ArgumentException("Compressed, uncompressed, and unfinished suffix must be distinct.", nameof(value));
				}
				compressedFileSuffix = value;
			}
		}
		/// <summary>
		/// Specifies the filename suffix for stored log files that are uncompressed, because they were stored with <see cref="CompressFiles"/> set to false.
		/// This property must not be changed during normal operation but only when no <see cref="SglAnalytics"/> object uses this object.
		/// Changing it while a <see cref="SglAnalytics"/> is using it can cause problems with files not being found or listed correctly, depending on when the change happens.
		/// </summary>
		public string UncompressedFileSuffix {
			get => uncompressedFileSuffix;
			set {
				var vc = new ValidationContext(this);
				vc.DisplayName = vc.MemberName = nameof(UncompressedFileSuffix);
				Validator.ValidateValue(value, vc, new ValidationAttribute[] { new PlainNameAttribute(), new StringLengthAttribute(16) });
				if (compressedFileSuffix.EndsWith(value, StringComparison.OrdinalIgnoreCase) || unfinishedFileSuffix.EndsWith(value, StringComparison.OrdinalIgnoreCase)) {
					throw new ArgumentException("Compressed, uncompressed, and unfinished suffix must be distinct.", nameof(value));
				}
				uncompressedFileSuffix = value;
			}
		}
		/// <summary>
		/// Specifies the filename suffix for log files for which <see cref="ILogStorage.ILogFile.FinishAsync(CancellationToken)"/> has not completed.
		/// This property must not be changed during normal operation but only when no <see cref="SglAnalytics"/> object uses this object.
		/// Changing it while a <see cref="SglAnalytics"/> is using it can cause problems with files not being found or listed correctly, depending on when the change happens.
		/// </summary>
		public string UnfinishedFileSuffix {
			get => unfinishedFileSuffix;
			set {
				var vc = new ValidationContext(this);
				vc.DisplayName = vc.MemberName = nameof(UnfinishedFileSuffix);
				Validator.ValidateValue(value, vc, new ValidationAttribute[] { new PlainNameAttribute(), new StringLengthAttribute(16) });
				if (uncompressedFileSuffix.EndsWith(value, StringComparison.OrdinalIgnoreCase) || compressedFileSuffix.EndsWith(value, StringComparison.OrdinalIgnoreCase)) {
					throw new ArgumentException("Compressed, uncompressed, and unfinished suffix must be distinct.", nameof(value));
				}
				unfinishedFileSuffix = value;
			}
		}
		/// <summary>
		/// Specifies whether removed files are archived in an <c>archive</c> subdirectory, otherwise they are actually deleted.
		/// </summary>
		public bool Archiving {
			get {
				lock (configLock) {
					return archiving;
				}
			}
			set {
				lock (configLock) {
					archiving = value;
				}
			}
		}
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
				await wrapped.DisposeAsync();
				if (storage != null) {
					lock (storage.stateLock) {
						storage.logFilesOpenForWriting.Remove(logObject.ID);
					}
				}
				logObject.OpenForWriting = false;
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
				wrapped.Dispose();
				if (storage != null) {
					lock (storage.stateLock) {
						storage.logFilesOpenForWriting.Remove(logObject.ID);
					}
				}
				logObject.OpenForWriting = false;
				if (disposing) storage = null;
			}
		}

		private class LogFile : ILogStorage.ILogFile {
			private readonly DateTime? creationTime;
			private DirectoryLogStorage storage;
			public Guid ID { get; }
			public DateTime CreationTime => (creationTime ?? File.GetCreationTimeUtc(FullFileName)).ToLocalTime();
			public DateTime EndTime => File.GetLastWriteTime(FullFileName);

			public string FullFileName => GetFullFileName(storage.directory, creationTime, ID, Suffix);

			public static string GetFullFileName(string directory, DateTime? creationTime, Guid id, string suffix) {
				return Path.Combine(directory, creationTime.HasValue ?
								$"{id}_{(long)(creationTime.Value.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds}{suffix}" :
								$"{id}{suffix}");
			}

			public string Suffix { get; set; }
			public LogContentEncoding Encoding { get; set; }

			public bool OpenForWriting { get; set; } = false;

			public LogFile(Guid id, DateTime? creationTime, string suffix, LogContentEncoding encoding, DirectoryLogStorage storage) {
				this.storage = storage;
				ID = id;
				this.creationTime = creationTime;
				Suffix = suffix;
				Encoding = encoding;
			}

			public Stream OpenReadContent() {
				var fileStream = OpenReadEncoded();
				if (Encoding == LogContentEncoding.GZipCompressed) {
					return new GZipStream(fileStream, CompressionMode.Decompress);
				}
				else {
					return fileStream;
				}
			}

			public Stream OpenReadEncoded() => new FileStream(FullFileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

			public void Remove() {
				if (storage.Archiving) {
					var targetDir = Path.Combine(storage.directory, "archive");
					Directory.CreateDirectory(targetDir);
					File.Move(FullFileName, Path.Combine(targetDir, Path.GetFileName(FullFileName)));
				}
				else {
					File.Delete(FullFileName);
				}
			}

			public Task FinishAsync(CancellationToken ct = default) {
				return storage.FinishLogFileAsync(this, ct);
			}

			public bool Equals(ILogStorage.ILogFile? other) => other is LogFile lfo ? (ID == other.ID && storage.directory == lfo.storage.directory) : false;
		}

		/// <inheritdoc/>
		public Stream CreateLogFile(out ILogStorage.ILogFile logFileMetadata) {
			var id = Guid.NewGuid();
			var logFile = new LogFile(id, DateTime.UtcNow, UnfinishedFileSuffix, LogContentEncoding.Plain, storage: this) {
				OpenForWriting = true
			};
			// Before creating file, mark it as open for writing to prevent time window where
			// the syscall for creation is done but the file is not yet marked:
			lock (stateLock) {
				logFilesOpenForWriting.Add(logFile.ID);
			}
			var fileStream = new FileStream(logFile.FullFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
			logFileMetadata = logFile;
			return new StreamWrapper(fileStream, this, logFile);
		}

		private (Guid? id, DateTime? creationTime) getFilenameMeta(string path, string suffix) {
			path = Path.GetFileName(path);
			if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) {
				var baseName = path.Remove(path.Length - suffix.Length);
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
		/// Lists all log files in the directory with the currently set <see cref="CompressedFileSuffix"/>, <see cref="UncompressedFileSuffix"/>, 
		/// and <see cref="UnfinishedFileSuffix"/>, except those currently being written.
		/// </summary>
		/// <returns>A list of the logs.</returns>
		public IList<ILogStorage.ILogFile> ListAllLogFiles() {
			var compressedLogs = EnumerateLogs(CompressedFileSuffix, LogContentEncoding.GZipCompressed);
			var uncompressedLogs = EnumerateLogs(UncompressedFileSuffix, LogContentEncoding.Plain);
			var unfinishedLogs = ListUnfinishedLogFilesForRecovery();
			var allLogs = uncompressedLogs.Concat(compressedLogs).Concat(unfinishedLogs);
			return allLogs.ToList();
		}
		private IEnumerable<ILogStorage.ILogFile> EnumerateLogs(string suffix, LogContentEncoding encoding) =>
			from file in (from filename in Directory.EnumerateFiles(directory, "*" + suffix)
						  let fileNameMeta = getFilenameMeta(filename, suffix)
						  let id = fileNameMeta.id
						  let creationTime = fileNameMeta.creationTime
						  where id.HasValue
						  select new LogFile(id.Value, creationTime, suffix, encoding, storage: this))
			orderby file.CreationTime
			select file;

		/// <summary>
		/// Lists the log files in the directory with the currently set <see cref="CompressedFileSuffix"/> and 
		/// <see cref="UncompressedFileSuffix"/>, excluding those for which a corresponding unfinished file also exists.
		/// The latter restriction excludes files for which the finishing operation may not have completed yet.
		/// </summary>
		/// <returns>A list of the logs.</returns>
		public IList<ILogStorage.ILogFile> ListLogFiles() {
			var compressedLogs = EnumerateLogs(CompressedFileSuffix, LogContentEncoding.GZipCompressed);
			var uncompressedLogs = EnumerateLogs(UncompressedFileSuffix, LogContentEncoding.Plain);
			var unfinishedLogIds = ListUnfinishedLogFilesForRecovery().Select(log => log.ID).ToHashSet();
			var result = uncompressedLogs.Concat(compressedLogs).Where(log => !unfinishedLogIds.Contains(log.ID)).ToList();
			return result;
		}

		private async Task FinishLogFileAsync(LogFile logFileMetadata, CancellationToken ct = default) {
			if (logFileMetadata.OpenForWriting) {
				throw new InvalidOperationException("Log file that is still open for writing can't be finished.");
			}
			if (logFileMetadata.Encoding != LogContentEncoding.Plain) {
				throw new ArgumentException("Can only finish unfinished log files, which are expected to be in plain encoding.", nameof(logFileMetadata));
			}
			if (logFileMetadata.Suffix != UnfinishedFileSuffix) {
				throw new ArgumentException($"Can only finish unfinished log files, which are expected to use suffix '{logFileMetadata.Suffix}'.", nameof(logFileMetadata));
			}
			if (CompressFiles) {
				var dstFilename = LogFile.GetFullFileName(directory, logFileMetadata.CreationTime, logFileMetadata.ID, CompressedFileSuffix);
				var srcFilename = logFileMetadata.FullFileName;
				await using (var dstStream = new GZipStream(new FileStream(dstFilename, FileMode.Create, FileAccess.Write,
							FileShare.None, bufferSize: 4096, FileOptions.Asynchronous | FileOptions.WriteThrough),
						CompressionLevel.Optimal)) {
					await using var srcStream = new FileStream(srcFilename, FileMode.Open, FileAccess.Read,
						FileShare.Read, bufferSize: 4096, useAsync: true);
					await srcStream.CopyToAsync(dstStream, ct);
				}
				File.Delete(srcFilename);
				logFileMetadata.Suffix = CompressedFileSuffix;
				logFileMetadata.Encoding = LogContentEncoding.GZipCompressed;
			}
			else {
				var dstFilename = LogFile.GetFullFileName(directory, logFileMetadata.CreationTime, logFileMetadata.ID, UncompressedFileSuffix);
				await Task.Run(() => {
#if NETCOREAPP3_0_OR_GREATER
					File.Move(logFileMetadata.FullFileName, dstFilename, overwrite: true);
#else
					if (File.Exists(dstFilename)) {
						File.Delete(dstFilename);
					}
					File.Move(logFileMetadata.FullFileName, dstFilename);
#endif
				}, ct);
				logFileMetadata.Suffix = UncompressedFileSuffix;
				logFileMetadata.Encoding = LogContentEncoding.Plain;
			}
		}

		/// <summary>
		/// Lists the unfinished log files in the directory with the currently set <see cref="UnfinishedFileSuffix"/>,
		/// except those that are currently open for writing.
		/// </summary>
		/// <returns>A list of the log file objects.</returns>
		/// <remarks>
		/// Note that simlutaneous access by multiple processes or by multiple <see cref="DirectoryLogStorage"/> in the same process to the directory is not supported and
		/// thus, files open for writing by other processes can not be properly excluded.
		/// </remarks>
		public IList<ILogStorage.ILogFile> ListUnfinishedLogFilesForRecovery() {
			var allUnfinishedLogs = EnumerateLogs(UnfinishedFileSuffix, LogContentEncoding.Plain).ToList();
			lock (stateLock) {
				allUnfinishedLogs.RemoveAll(log => logFilesOpenForWriting.Contains(log.ID));
			}
			return allUnfinishedLogs;
		}
	}
}
