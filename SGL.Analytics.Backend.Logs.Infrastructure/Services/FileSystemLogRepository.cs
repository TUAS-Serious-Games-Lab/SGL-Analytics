using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {

	/// <summary>
	/// Encapsulates the configuration options for <see cref="Services.FileSystemLogRepository"/>
	/// </summary>
	public class FileSystemLogRepositoryOptions {
		/// <summary>
		/// The config key under which the options are looked up, <c>FileSystemLogRepository</c>.
		/// </summary>
		public const string FileSystemLogRepository = "FileSystemLogRepository";

		/// <summary>
		/// The directory where the log files shall be stored in per-application and per-user subdirectories.
		/// Defaults to the directory <c>LogStorage</c> under the current directory.
		/// </summary>
		public string StorageDirectory { get; set; } = Path.Combine(Environment.CurrentDirectory, "LogStorage");
	}

	/// <summary>
	/// Provides the <see cref="UseFileSystemCollectorLogStorage(IServiceCollection, IConfiguration)"/> extension method.
	/// </summary>
	public static class FileSystemLogRepositoryExtensions {
		/// <summary>
		/// Adds <see cref="FileSystemLogRepository"/> as the implementation for <see cref="ILogFileRepository"/>
		/// with its configuration options obtained from the configuration root object <paramref name="config"/> in the service collection.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="config">The config root to use.</param>
		/// <returns>A reference to <paramref name="services"/> for chaining.</returns>
		public static IServiceCollection UseFileSystemCollectorLogStorage(this IServiceCollection services, IConfiguration config) {
			services.Configure<FileSystemLogRepositoryOptions>(config.GetSection(FileSystemLogRepositoryOptions.FileSystemLogRepository));
			services.AddScoped<ILogFileRepository, FileSystemLogRepository>();
			return services;
		}
	}

	/// <summary>
	/// Provides an imlementation of <see cref="ILogFileRepository"/> that stores the analytics log files under a specified directory
	/// with subdirectories for the application, containing per-user subdirectories that contain the log files.
	/// </summary>
	public class FileSystemLogRepository : ILogFileRepository {
		private static readonly int guidLength = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx".Length;
		private static readonly string tempSeparator = ".temp-";
		private static readonly int tempSeparatorLength = tempSeparator.Length;
		private static readonly int tempSuffixLength = makeTempSuffix().Length;
		private readonly string storageDirectory;

		/// <summary>
		/// Creates a repository using the given configuration options.
		/// </summary>
		/// <param name="configOptions">The configuration options.</param>
		public FileSystemLogRepository(IOptions<FileSystemLogRepositoryOptions> configOptions) : this(configOptions.Value) { }
		/// <summary>
		/// Creates a repository using the given configuration options.
		/// </summary>
		/// <param name="options">The configuration options.</param>
		public FileSystemLogRepository(FileSystemLogRepositoryOptions options) : this(options.StorageDirectory) { }
		/// <summary>
		/// Creates a repository using the given directory path as its root directory under which the subdirectories and log files are stored.
		/// </summary>
		/// <param name="storageDirectory"></param>
		public FileSystemLogRepository(string storageDirectory) {
			this.storageDirectory = storageDirectory;
		}
		private static string makeTempSuffix() {
			return tempSeparator + StringGenerator.GenerateRandomWord(6);
		}

		private string makeFilePath(string appName, Guid userId, Guid logId, string suffix) {
			return Path.Combine(storageDirectory, appName, userId.ToString(), logId.ToString() + suffix);
		}
		private string makeDirectoryPath(string appName, Guid userId) {
			return Path.Combine(storageDirectory, appName, userId.ToString());
		}

		private bool doesDirectoryExist(string appName, Guid userId) {
			return Directory.Exists(makeDirectoryPath(appName, userId));
		}

		private void ensureDirectoryExists(string appName, Guid userId) {
			Directory.CreateDirectory(makeDirectoryPath(appName, userId));
		}

		private static LogPath? tryParseFilename(string appName, Guid userId, ReadOnlySpan<char> filename) {
			var guidSpan = filename.Slice(0, guidLength);
			if (!Guid.TryParse(guidSpan, out var logId)) return null;
			var afterGuidSpan = filename.Slice(guidLength);
			if (afterGuidSpan.Length >= tempSuffixLength) {
				var potentialTempSuffix = afterGuidSpan.Slice(afterGuidSpan.Length - tempSuffixLength);
				if (potentialTempSuffix.StartsWith(".temp-")) {
					return null;
				}
			}
			return new LogPath {
				AppName = appName,
				UserId = userId,
				LogId = logId,
				Suffix = afterGuidSpan.ToString()
			};
		}

		private IEnumerable<LogPath> enumerateDirectory(string appName, Guid userId) {
			if (!doesDirectoryExist(appName, userId)) return Enumerable.Empty<LogPath>();
			var files = Directory.EnumerateFiles(Path.Combine(storageDirectory, appName, userId.ToString()));
			return from file in files
				   let logPath = tryParseFilename(appName, userId, Path.GetFileName(file.AsSpan()))
				   where logPath.HasValue
				   select logPath.Value;
		}

		/// <inheritdoc/>
		public async Task CopyLogIntoAsync(string appName, Guid userId, Guid logId, string suffix, Stream contentDestination, CancellationToken ct = default) {
			await using (var stream = await ReadLogAsync(appName, userId, logId, suffix, ct)) {
				await stream.CopyToAsync(contentDestination, ct);
			}
		}

		/// <inheritdoc/>
		public Task DeleteLogAsync(string appName, Guid userId, Guid logId, string suffix, CancellationToken ct = default) {
			return Task.Run(() => {
				File.Delete(makeFilePath(appName, userId, logId, suffix));
			});
		}

		/// <inheritdoc/>
		public IEnumerable<LogPath> EnumerateLogs(string appName, Guid userId) {
			return enumerateDirectory(appName, userId);
		}

		/// <inheritdoc/>
		public IEnumerable<LogPath> EnumerateLogs(string appName) {
			var dirs = Directory.EnumerateDirectories(Path.Combine(storageDirectory, appName));
			var userIds = from dir in dirs
						  let userId = Guid.TryParse(Path.GetFileName(dir), out var guid) ? guid : (Guid?)null
						  where userId.HasValue
						  select userId.Value;
			foreach (var userId in userIds) {
				foreach (var logPath in EnumerateLogs(appName, userId)) {
					yield return logPath;
				}
			}
		}

		/// <inheritdoc/>
		public IEnumerable<LogPath> EnumerateLogs() {
			var dirs = from dir in Directory.EnumerateDirectories(storageDirectory)
					   select Path.GetFileName(dir);
			foreach (var appName in dirs) {
				foreach (var logPath in EnumerateLogs(appName)) {
					yield return logPath;
				}
			}
		}

		/// <summary>
		/// Represents a temporary file left by a failed <see cref="StoreLogAsync(string, Guid, Guid, string, Stream, CancellationToken)"/>
		/// operation where the temporary file could not be removed, e.g. because of a server crash.
		/// </summary>
		public struct TempFilePath {
			/// <summary>
			/// The <see cref="LogPath.AppName"/> for the file.
			/// </summary>
			public string AppName { get; set; }
			/// <summary>
			/// The <see cref="LogPath.UserId"/> for the file.
			/// </summary>
			public string UserDir { get; set; }
			/// <summary>
			/// The name of the file within the application and user directory.
			/// </summary>
			public string FileName { get; set; }

			/// <summary>
			/// Returns the combined path relative to the storage directory as a string representation.
			/// </summary>
			public override string ToString() {
				return Path.Combine(AppName, UserDir, FileName);
			}

			internal TempFilePath(string appName, string userDir, string fileName) {
				AppName = appName;
				UserDir = userDir;
				FileName = fileName;
			}
		}

		/// <summary>
		/// Enumerates temporary files left by failed <see cref="StoreLogAsync(string, Guid, Guid, string, Stream, CancellationToken)"/>
		/// operations where the temporary file could not be removed, e.g. because of a server crash.
		/// These files can be removed using <see cref="DeleteTempFile(TempFilePath)"/>.
		/// </summary>
		/// <returns>An enumerable to iterate over the relative paths of the files.</returns>
		public IEnumerable<TempFilePath> EnumerateTempFiles() {
			string searchPattern = $"*{tempSeparator}{new string('?', tempSuffixLength)}";
			var appDirs = from dir in Directory.EnumerateDirectories(storageDirectory)
						  select Path.GetFileName(dir);
			foreach (var appName in appDirs) {
				if (appName is null) continue;
				var userDirs = from dir in Directory.EnumerateDirectories(Path.Combine(storageDirectory, appName))
							   let dirName = Path.GetFileName(dir)
							   let userId = Guid.TryParse(dirName, out var guid) ? guid : (Guid?)null
							   where userId.HasValue
							   select dirName;
				foreach (var userDir in userDirs) {
					foreach (var logFile in Directory.EnumerateFiles(Path.Combine(storageDirectory, appName, userDir), searchPattern)) {
						yield return new TempFilePath(appName, userDir, Path.GetFileName(logFile));
					}
				}
			}
		}

		/// <summary>
		/// Deletes the temporary file represented by the given <see cref="TempFilePath"/>.
		/// </summary>
		/// <param name="tempFile">The path of the file to remove.</param>
		public void DeleteTempFile(TempFilePath tempFile) {
			File.Delete(Path.Combine(storageDirectory, tempFile.AppName, tempFile.UserDir, tempFile.FileName));
		}

		/// <inheritdoc/>
		public Task<Stream> ReadLogAsync(string appName, Guid userId, Guid logId, string suffix, CancellationToken ct = default) {
			return Task.Run(() => {
				try {
					var filePath = makeFilePath(appName, userId, logId, suffix);
					ct.ThrowIfCancellationRequested();
					return (Stream)new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
				}
				catch (OperationCanceledException) {
					throw;
				}
				catch (Exception ex) {
					throw new LogFileNotAvailableException(new LogPath { AppName = appName, UserId = userId, LogId = logId, Suffix = suffix }, ex);
				}
			}, ct);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// The log is first written to a temporary file that is not found by the enumerating and reading methods.
		/// and then renamed to the correct final filename upon successful completion.
		/// Thus, if an error occurs during the writing process, the incomplete contents are not visible.
		/// Instead the temporary file is removed if transfer fails.
		/// Furthermore, this strategy provides a last-writer wins resolution for concurrent uploads of the same log, where 'last' refers to the operation the finishes last.
		/// </remarks>
		public Task StoreLogAsync(string appName, Guid userId, Guid logId, string suffix, Stream content, CancellationToken ct = default) {
			return Task.Run(async () => {
				ct.ThrowIfCancellationRequested();
				ensureDirectoryExists(appName, userId);
				// Create target file with temporary name to not make it visible to other operations while it is still being written.
				var filePath = Path.Combine(storageDirectory, appName, userId.ToString(), logId.ToString() + suffix + makeTempSuffix());
				try {
					ct.ThrowIfCancellationRequested();
					using (var writeStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
						ct.ThrowIfCancellationRequested();
						await content.CopyToAsync(writeStream, ct);
					}
					ct.ThrowIfCancellationRequested();
				}
				catch {
					// The store operation failed, most likely due to the content stream producing an I/O error (e.g. because it is reading from a network connection that was interrupted).
					try {
						// Delete the temporary file before rethrowing.
						File.Delete(filePath);
					}
					// However, if the deletion also fails, e.g. due to some server-side I/O error or permission problem, rethrow the original exception, not the new one.
					catch { }
					throw;
				}
				// Rename to final file name to make it visible to other operations.
				File.Move(filePath, makeFilePath(appName, userId, logId, suffix), overwrite: true);
			}, ct);
		}

		public async Task CheckHealthAsync(CancellationToken ct = default) {
			await Task.Yield();
			byte[] probe_data = Encoding.UTF8.GetBytes("Health Check Probe");
			ct.ThrowIfCancellationRequested();
			var health_check_dir = Path.Combine(storageDirectory, ".server_health_check");
			try {
				Directory.Delete(health_check_dir, true);
			}
			catch (Exception) { }
			Directory.CreateDirectory(health_check_dir);
			ct.ThrowIfCancellationRequested();
			var probe_file = Path.Combine(health_check_dir, "probe.file");
			using (var writeStream = new FileStream(probe_file, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
				ct.ThrowIfCancellationRequested();
				await writeStream.WriteAsync(probe_data,ct);
			}
			ct.ThrowIfCancellationRequested();
			using (var readStream = new FileStream(probe_file, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true)) {
				ct.ThrowIfCancellationRequested();
				byte[] read_probe = new byte[4096];
				var read_amt = await readStream.ReadAsync(read_probe, ct);
				if(read_amt != probe_data.Length) {
					throw new Exception("Read probe data length did not match written probe data length.");
				}
				if (!Enumerable.SequenceEqual(probe_data, read_probe.Take(read_amt))) {
					throw new Exception("Read probe data did not match written probe data.");
				}
				ct.ThrowIfCancellationRequested();
			}
			Directory.Delete(health_check_dir, true);
		}
	}
}
