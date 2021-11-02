using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	/// <summary>
	/// Represents the components needed to logically address a specific analytics log file.
	/// </summary>
	public struct LogPath {
		/// <summary>
		/// The technical name of the application from which the log file originates.
		/// </summary>
		public string AppName { get; set; }
		/// <summary>
		/// The id of the user that submitted the log file.
		/// </summary>
		public Guid UserId { get; set; }
		/// <summary>
		/// The unique id of the log file itself.
		/// </summary>
		public Guid LogId { get; set; }
		/// <summary>
		/// The file suffix for the file name.
		/// </summary>
		public string Suffix { get; set; }

		/// <summary>
		/// Generates a string representation of the logical path.
		/// </summary>
		/// <returns>A string representation of the path components.</returns>
		public override string ToString() => $"[{AppName}/{UserId}/{LogId}{Suffix}]";
	}

	/// <summary>
	/// The exception thrown when a requested log file is not available.
	/// This is most likely the case, if the file doesn't exist, but can also mean, that it exists but is not accessible, e.g. due to file permissions.
	/// </summary>
	public class LogFileNotAvailableException : Exception {
		/// <summary>
		/// Constructs an exception object for the given path and optionally the given inner exception to describe the root cause.
		/// </summary>
		/// <param name="logPath">The affected log path.</param>
		/// <param name="innerException">Another exception that describes the cause of this exception.</param>
		public LogFileNotAvailableException(LogPath logPath, Exception? innerException = null) : base($"The log file {logPath} is not available.", innerException) {
			LogPath = logPath;
		}

		/// <summary>
		/// The affected path, i.e. the path that was requested but is not available.
		/// </summary>
		public LogPath LogPath { get; set; }
	}

	/// <summary>
	/// Specifies the interface for a repository of analytics log files, used to store, retrieve, enumerate and delete analytics log file contents.
	/// </summary>
	/// <remarks>
	/// For a maximum of implementation flexibility, it allows operations to be performed asynchronously where possible, even if the default implementation (using files and directories) can only use synchronous APIs.
	/// E.g. while opening a stream to a local file uses a synchronous API, a possible alternate implementation might be backed by an object store where the opening operation involves a request that can be done asynchronously.
	/// However, <c>EnumerateLogs</c> methods need to provide synchronous versions, because LINQ extension methods don't apply to <see cref="IAsyncEnumerable{T}"/>, but only to <see cref="IEnumerable{T}"/>.
	/// </remarks>
	public interface ILogFileRepository {
		/// <summary>
		/// Asynchronously stores the data contained in <paramref name="content"/> under the logical path given in <paramref name="logPath"/>.
		/// </summary>
		/// <param name="logPath">The logical path to which the file should be stored.</param>
		/// <param name="content">A <see cref="Stream"/> with the desired content. The stream will be read to completion, copying all read data into the target file.</param>
		/// <param name="ct">A cancellation token to allow cancelling the store operation.</param>
		/// <returns>A task object representing the store operation.</returns>
		Task StoreLogAsync(LogPath logPath, Stream content, CancellationToken ct = default) {
			return StoreLogAsync(logPath.AppName, logPath.UserId, logPath.LogId, logPath.Suffix, content, ct);
		}
		/// <summary>
		/// Asynchronously stores the data contained in <paramref name="content"/> under the logical path given in <paramref name="appName"/>, <paramref name="userId"/>, <paramref name="userId"/>, and <paramref name="suffix"/>.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the log file originates.</param>
		/// <param name="userId">The id of the user that submitted the log file.</param>
		/// <param name="logId">The unique id of the log file itself.</param>
		/// <param name="suffix">The file suffix for the file name.</param>
		/// <param name="content">A <see cref="Stream"/> with the desired content. The stream will be read to completion, copying all read data into the target file.</param>
		/// <param name="ct">A cancellation token to allow cancelling the store operation.<</param>
		/// <returns>A task object representing the store operation.</returns>
		Task StoreLogAsync(string appName, Guid userId, Guid logId, string suffix, Stream content, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously opens the analytics log file under the logical path given in <paramref name="logPath"/> for reading.
		/// </summary>
		/// <param name="logPath">The logical path of the analytics log file to read from.</param>
		/// <param name="ct">A cancellation token to allow cancelling the opertation.</param>
		/// <returns>
		/// A task representing the operation, that contains the opened stream upon success.
		/// It is the responsibility of the caller to dispose of this stream.
		/// </returns>
		Task<Stream> ReadLogAsync(LogPath logPath, CancellationToken ct = default) {
			return ReadLogAsync(logPath.AppName, logPath.UserId, logPath.LogId, logPath.Suffix, ct);
		}
		/// <summary>
		/// Asynchronously opens the analytics log file under the logical path given in <paramref name="appName"/>, <paramref name="userId"/>, <paramref name="userId"/>, and <paramref name="suffix"/> for reading.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the log file originates.</param>
		/// <param name="userId">The id of the user that submitted the log file.</param>
		/// <param name="logId">The unique id of the log file itself.</param>
		/// <param name="suffix">The file suffix for the file name.</param>
		/// <param name="ct">A cancellation token to allow cancelling the opertation.</param>
		/// <returns>
		/// A task representing the operation, that contains the opened stream upon success.
		/// It is the responsibility of the caller to dispose of this stream.
		/// </returns>
		Task<Stream> ReadLogAsync(string appName, Guid userId, Guid logId, string suffix, CancellationToken ct = default);
		/// <summary>
		/// Asynchronously copies the contents of the analytics log file under the logical path given in <paramref name="logPath"/> into the stream given in <paramref name="contentDestination"/>.
		/// </summary>
		/// <param name="logPath">The logical path of the analytics log file to read from.</param>
		/// <param name="contentDestination">A stream to write the copied content to.</param>
		/// <param name="ct">A cancellation token to allow cancelling the opertation.</param>
		/// <returns>A task object representing the copy operation.</returns>
		Task CopyLogIntoAsync(LogPath logPath, Stream contentDestination, CancellationToken ct = default) {
			return CopyLogIntoAsync(logPath.AppName, logPath.UserId, logPath.LogId, logPath.Suffix, contentDestination, ct);
		}
		/// <summary>
		/// Asynchronously copies the contents of the analytics log file under the logical path given in <paramref name="appName"/>, <paramref name="userId"/>, <paramref name="userId"/>, and <paramref name="suffix"/> into the stream given in <paramref name="contentDestination"/>.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the log file originates.</param>
		/// <param name="userId">The id of the user that submitted the log file.</param>
		/// <param name="logId">The unique id of the log file itself.</param>
		/// <param name="suffix">The file suffix for the file name.</param>
		/// <param name="contentDestination">A stream to write the copied content to.</param>
		/// <param name="ct">A cancellation token to allow cancelling the opertation.</param>
		/// <returns>A task object representing the copy operation.</returns>
		Task CopyLogIntoAsync(string appName, Guid userId, Guid logId, string suffix, Stream contentDestination, CancellationToken ct = default);
		/// <summary>
		/// Enumerates over all known analytics log files belonging to the given user of the given application.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the log files originate.</param>
		/// <param name="userId">The id of the user that submitted the log files.</param>
		/// <returns>An enumerable to iterate over the paths.</returns>
		IEnumerable<LogPath> EnumerateLogs(string appName, Guid userId);
		/// <summary>
		/// Enumerates over all known analytics log files for a given application.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the log files originate.</param>
		/// <returns>An enumerable to iterate over the paths.</returns>
		IEnumerable<LogPath> EnumerateLogs(string appName);
		/// <summary>
		/// Enumerates the paths of all known analytics log files.
		/// </summary>
		/// <returns>An enumerable to iterate over the paths.</returns>
		IEnumerable<LogPath> EnumerateLogs();
		/// <summary>
		/// Asynchronously deletes the analytics log file under the logical path given in <paramref name="logPath"/>.
		/// </summary>
		/// <param name="logPath">The logical path of the analytics log file to delete.</param>
		/// <param name="ct">A cancellation token to allow cancelling (the waiting on) the opertation. Note the it is not guaranteed whether this prevents the deletion, as it is dependent on when in the process the task is interrupted.</param>
		/// <returns>A task object representing the delete operation.</returns>
		Task DeleteLogAsync(LogPath logPath, CancellationToken ct = default) {
			return DeleteLogAsync(logPath.AppName, logPath.UserId, logPath.LogId, logPath.Suffix, ct);
		}
		/// <summary>
		/// Asynchronously deletes the analytics log file under the logical path given in <paramref name="appName"/>, <paramref name="userId"/>, <paramref name="userId"/>, and <paramref name="suffix"/>.
		/// </summary>
		/// <param name="appName">The technical name of the application from which the log file originates.</param>
		/// <param name="userId">The id of the user that submitted the log file.</param>
		/// <param name="logId">The unique id of the log file itself.</param>
		/// <param name="suffix">The file suffix for the file name.</param>
		/// <param name="ct">A cancellation token to allow cancelling (the waiting on) the opertation. Note the it is not guaranteed whether this prevents the deletion, as it is dependent on when in the process the task is interrupted.</param>
		/// <returns>A task object representing the delete operation.</returns>
		Task DeleteLogAsync(string appName, Guid userId, Guid logId, string suffix, CancellationToken ct = default);
	}
}
