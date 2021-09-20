﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	public struct LogPath {
		public string AppName { get; set; }
		public Guid UserId { get; set; }
		public Guid LogId { get; set; }
		public string Suffix { get; set; }

		public override string ToString() => $"[{AppName}/{UserId}/{LogId}{Suffix}]";
	}

	public class LogFileNotAvailableException : Exception {
		public LogFileNotAvailableException(LogPath logPath, Exception? innerException = null) : base($"The log file {logPath} is not available.", innerException) {
			LogPath = logPath;
		}

		public LogPath LogPath { get; set; }
	}

	public interface ILogFileRepository {
		// For maximum of implementation flexibility, allow operations to be performed asynchronously where possible, even if the default implementation can only use synchronous APIs.
		// E.g. while opening a stream to a local file uses a synchronous API, a possible alternate implementation might be backed by an object store where the opening operation involves a request that can be done asynchronously.
		// However, EnumerateLogs methods need to provide synchronous versions, because LINQ extension methods don't apply to IAsyncEnumerable<T> but only to IEnumerable<T>.
		Task StoreLogAsync(LogPath logPath, Stream content, CancellationToken ct = default) {
			return StoreLogAsync(logPath.AppName, logPath.UserId, logPath.LogId, logPath.Suffix, content, ct);
		}
		Task StoreLogAsync(string appName, Guid userId, Guid logId, string suffix, Stream content, CancellationToken ct = default);
		Task<Stream> ReadLogAsync(LogPath logPath, CancellationToken ct = default) {
			return ReadLogAsync(logPath.AppName, logPath.UserId, logPath.LogId, logPath.Suffix, ct);
		}
		Task<Stream> ReadLogAsync(string appName, Guid userId, Guid logId, string suffix, CancellationToken ct = default);
		Task CopyLogIntoAsync(LogPath logPath, Stream contentDestination, CancellationToken ct = default) {
			return CopyLogIntoAsync(logPath.AppName, logPath.UserId, logPath.LogId, logPath.Suffix, contentDestination, ct);
		}
		Task CopyLogIntoAsync(string appName, Guid userId, Guid logId, string suffix, Stream contentDestination, CancellationToken ct = default);
		IEnumerable<LogPath> EnumerateLogs(string appName, Guid userId);
		IEnumerable<LogPath> EnumerateLogs(string appName);
		IEnumerable<LogPath> EnumerateLogs();
		Task DeleteLogAsync(LogPath logPath, CancellationToken ct = default) {
			return DeleteLogAsync(logPath.AppName, logPath.UserId, logPath.LogId, logPath.Suffix, ct);
		}
		Task DeleteLogAsync(string appName, Guid userId, Guid logId, string suffix, CancellationToken ct = default);
	}
}
