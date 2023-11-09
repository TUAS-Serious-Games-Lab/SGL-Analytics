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
	/// An implementation of <see cref="ILogStorage"/> that used a directory on the local filesystem to store the logs as files.
	/// It uses the filename to associate the <see cref="ILogStorage.ILogFile.ID"/>, and the file timestamps to store the <see cref="ILogStorage.ILogFile.CreationTime"/> and
	/// <see cref="ILogStorage.ILogFile.EndTime"/> and thus doesn't need a separate metadata storage.
	/// The implementations also supports optional compression of the files as well as archiving locally removed files in a separate directory.
	/// </summary>
	public class DirectoryLogStorage : ILogStorage {
		private string directory;
		private HashSet<Guid> logFilesOpenForWriting = new();
		private string compressedFileSuffix = ".log.gz";
		private string uncompressedFileSuffix = ".log";
		private string unfinishedFileSuffix = ".log.pending";

		/// <summary>
		/// Specifies whether the log files should be compressed.
		/// This property must not be changed during normal operation but only when no <see cref="SglAnalytics"/> object uses this object.
		/// Changing it while a <see cref="SglAnalytics"/> is using it can cause problems with files not being found or listed correctly, depending on when the change happens.
		/// </summary>
		public bool CompressFiles { get; set; } = true;

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
		/// Specifies the filename suffix for log files for which <see cref="FinishLogFileAsync(ILogStorage.ILogFile, CancellationToken)"/> has not completed.
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
				storage?.logFilesOpenForWriting?.Remove(logObject.ID);
				wrapped.Dispose();
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
			logFilesOpenForWriting.Add(logFile.ID);
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
		/// Enumerates all log files in the directory with the currently set <see cref="CompressedFileSuffix"/>.
		/// </summary>
		/// <returns>An enumerable to iterate over the logs.</returns>
		public IList<ILogStorage.ILogFile> ListAllLogs() {
			var compressedLogs = EnumerateLogs(CompressedFileSuffix, LogContentEncoding.GZipCompressed);
			var uncompressedLogs = EnumerateLogs(UncompressedFileSuffix, LogContentEncoding.Plain);
			var unfinishedLogs = ListUnfinishedLogsForRecovery();
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
		/// Enumerates the log files in the directory with the currently set <see cref="CompressedFileSuffix"/> and 
		/// <see cref="UncompressedFileSuffix"/>, excluding those for which a corresponding unfinished file also exists.
		/// The latter restriction excludes files for which the finishing operation may not have completed yet.
		/// </summary>
		/// <remarks>
		/// Note that simlutaneous access by multiple processes or by multiple <see cref="DirectoryLogStorage"/> in the same process to the directory is not supported and
		/// thus, files open for writing by other processes can not be properly excluded.
		/// </remarks>
		/// <returns>A list of the logs.</returns>
		public IList<ILogStorage.ILogFile> ListLogs() {
			var compressedLogs = EnumerateLogs(CompressedFileSuffix, LogContentEncoding.GZipCompressed);
			var uncompressedLogs = EnumerateLogs(UncompressedFileSuffix, LogContentEncoding.Plain);
			var unfinishedLogIds = ListUnfinishedLogsForRecovery().Select(log => log.ID).ToHashSet();
			var result = uncompressedLogs.Concat(compressedLogs).Where(log => !unfinishedLogIds.Contains(log.ID)).ToList();
			return result;
		}

		public async Task FinishLogFileAsync(ILogStorage.ILogFile logFileMetadata, CancellationToken ct = default) {
			var log = logFileMetadata as LogFile;
			if (log == null) {
				throw new ArgumentException("Incompatible log ILogFile object.", nameof(logFileMetadata));
			}
			if (log.OpenForWriting) {
				throw new InvalidOperationException("Log file that is still open for writing can't be finished.");
			}
			if (log.Encoding != LogContentEncoding.Plain) {
				throw new ArgumentException("Can only finish unfinished log files, which are expected to be in plain encoding.", nameof(logFileMetadata));
			}
			if (log.Suffix != UnfinishedFileSuffix) {
				throw new ArgumentException($"Can only finish unfinished log files, which are expected to use suffix '{log.Suffix}'.", nameof(logFileMetadata));
			}
			if (CompressFiles) {
				var dstFilename = LogFile.GetFullFileName(directory, log.CreationTime, logFileMetadata.ID, CompressedFileSuffix);
				var srcFilename = log.FullFileName;
				await using (var dstStream = new GZipStream(new FileStream(dstFilename, FileMode.Create, FileAccess.Write,
							FileShare.None, bufferSize: 4096, FileOptions.Asynchronous | FileOptions.WriteThrough),
						CompressionLevel.Optimal)) {
					await using var srcStream = new FileStream(srcFilename, FileMode.Open, FileAccess.Read,
						FileShare.Read, bufferSize: 4096, useAsync: true);
					await srcStream.CopyToAsync(dstStream, ct);
				}
				File.Delete(srcFilename);
				log.Suffix = CompressedFileSuffix;
				log.Encoding = LogContentEncoding.GZipCompressed;
			}
			else {
				var dstFilename = LogFile.GetFullFileName(directory, log.CreationTime, logFileMetadata.ID, UncompressedFileSuffix);
				await Task.Run(() => {
#if NETCOREAPP3_0_OR_GREATER
					File.Move(log.FullFileName, dstFilename, overwrite: true);
#else
					if (File.Exists(dstFilename)) {
						File.Delete(dstFilename);
					}
					File.Move(log.FullFileName, dstFilename);
#endif
				}, ct);
				log.Suffix = UncompressedFileSuffix;
				log.Encoding = LogContentEncoding.Plain;
			}
		}

		public IList<ILogStorage.ILogFile> ListUnfinishedLogsForRecovery() =>
			EnumerateLogs(UnfinishedFileSuffix, LogContentEncoding.Plain)
			.Where(log => !logFilesOpenForWriting.Contains(log.ID))
			.ToList();
	}
}
