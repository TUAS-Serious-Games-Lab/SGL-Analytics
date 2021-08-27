using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {

	public class FileSystemLogRepositoryOptions {
		public const string FileSystemLogRepository = "FileSystemLogRepository";

		public string StorageDirectory { get; set; } = Path.Combine(Environment.CurrentDirectory, "LogStorage");
	}

	public static class FileSystemLogRepositoryExtensions {
		public static IServiceCollection UseFileSystemCollectorLogStorage(this IServiceCollection services, IConfiguration config) {
			services.Configure<FileSystemLogRepositoryOptions>(config.GetSection(FileSystemLogRepositoryOptions.FileSystemLogRepository));
			services.AddScoped<ILogFileRepository, FileSystemLogRepository>();
			return services;
		}
	}

	public class FileSystemLogRepository : ILogFileRepository {
		private static readonly int guidLength = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx".Length;
		private static readonly string tempSeparator = ".temp-";
		private static readonly int tempSeparatorLength = tempSeparator.Length;
		private static readonly int tempSuffixLength = makeTempSuffix().Length;
		private readonly string storageDirectory;

		public FileSystemLogRepository(IOptions<FileSystemLogRepositoryOptions> configOptions) : this(configOptions.Value) { }
		public FileSystemLogRepository(FileSystemLogRepositoryOptions options) : this(options.StorageDirectory) { }
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

		public async Task CopyLogIntoAsync(string appName, Guid userId, Guid logId, string suffix, Stream contentDestination) {
			await using (var stream = await ReadLogAsync(appName, userId, logId, suffix)) {
				await stream.CopyToAsync(contentDestination);
			}
		}

		public Task DeleteLogAsync(string appName, Guid userId, Guid logId, string suffix) {
			return Task.Run(() => {
				File.Delete(makeFilePath(appName, userId, logId, suffix));
			});
		}

		public IEnumerable<LogPath> EnumerateLogs(string appName, Guid userId) {
			return enumerateDirectory(appName, userId);
		}

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

		public IEnumerable<LogPath> EnumerateLogs() {
			var dirs = from dir in Directory.EnumerateDirectories(Path.Combine(storageDirectory))
					   select Path.GetFileName(dir);
			foreach (var appName in dirs) {
				foreach (var logPath in EnumerateLogs(appName)) {
					yield return logPath;
				}
			}
		}

		public Task<Stream> ReadLogAsync(string appName, Guid userId, Guid logId, string suffix) {
			return Task.Run(() => {
				try {
					var filePath = makeFilePath(appName, userId, logId, suffix);
					return (Stream)new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
				}
				catch (Exception ex) {
					throw new LogFileNotAvailableException(new LogPath { AppName = appName, UserId = userId, LogId = logId, Suffix = suffix }, ex);
				}
			});
		}

		public Task StoreLogAsync(string appName, Guid userId, Guid logId, string suffix, Stream content) {
			return Task.Run(async () => {
				ensureDirectoryExists(appName, userId);
				// Create target file with temporary name to not make it visible to other operations while it is still being written.
				var filePath = Path.Combine(storageDirectory, appName, userId.ToString(), logId.ToString() + suffix + makeTempSuffix());
				using (var writeStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
					await content.CopyToAsync(writeStream);
				}
				// Rename to final file name to make it visible to other operations.
				File.Move(filePath, makeFilePath(appName, userId, logId, suffix), overwrite: true);
			});
		}
	}
}
