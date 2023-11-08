using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	/// <summary>
	/// The interface for local analytics log file storages.
	/// </summary>
	public interface ILogStorage {
		/// <summary>
		/// The interface for the objects representing a stored log file in implementations.
		/// </summary>
		public interface ILogFile : IEquatable<ILogFile> {
			/// <summary>
			/// Gets the id of the log file.
			/// </summary>
			public Guid ID { get; }
			/// <summary>
			/// Gets the time when the file was created.
			/// </summary>
			public DateTime CreationTime { get; }
			/// <summary>
			/// Gets the time when recording of the log file ended.
			/// </summary>
			public DateTime EndTime { get; }
			/// <summary>
			/// Gets the name suffix of the log file.
			/// </summary>
			public string Suffix { get; }
			/// <summary>
			/// Gets the encoding used for the files content.
			/// </summary>
			public LogContentEncoding Encoding { get; }
			/// <summary>
			/// Opens the represented log file for reading.
			/// If the implementation encodes or compresses the files, this needs to provide the uncompressed / decoded content.
			/// </summary>
			/// <returns>A <see cref="Stream"/> for reading the log file content.</returns>
			public Stream OpenReadContent();
			/// <summary>
			/// Opens the represented log file for reading in raw form.
			/// Implementations may provide the content in compressed or encoded form.
			/// </summary>
			/// <returns>A <see cref="Stream"/> for reading the log file content in raw form, with possibly encoded or compressed content.</returns>
			public Stream OpenReadEncoded();

			/// <summary>
			/// Removes the represented log files.
			/// Implementations need to ensure that removed files do not appear in <see cref="EnumerateLogs"/> or <see cref="EnumerateUnfinishedLogsForRecovery"/>,
			/// but they may only logically remove them instead of actually deleting the data.
			/// E.g., an implementation working with the local file system could just move the removed file to a different directory.
			/// </summary>
			public void Remove();
		}
		/// <summary>
		/// Creates a new analytics log file, opens it for writing, and provides a Stream for writing the content as well as an object representing the log file.
		/// Note that the file can only be written with this stream, as the file can not be reopened for writing later.
		/// </summary>
		/// <param name="logFileMetadata">The variable to which the object representing the file shall be assigned.</param>
		/// <returns>A <see cref="Stream"/> for writing the content of the created file.</returns>
		Stream CreateLogFile(out ILogFile logFileMetadata);

		Task FinishLogFileAsync(ILogFile logFileMetadata, CancellationToken ct = default);

		/// <summary>
		/// Enumerates log files stored in the system, that are not currently open for writing,
		/// but for which <see cref="FinishLogFileAsync(ILogFile, CancellationToken)"/> was not called (or has not completed).
		/// As indicated by the method name, such files are usually left behind after a previous crash or process termination 
		/// before the file was finished (by completing <see cref="FinishLogFileAsync(ILogFile, CancellationToken)"/>.
		/// This enumeration is used to invoke <see cref="FinishLogFileAsync(ILogFile, CancellationToken)"/> 
		/// on them as part of a crash recovery procedure.
		/// </summary>
		/// <returns>An enumerable to iterate over the objects representing the files and their metadata.</returns>
		IList<ILogFile> EnumerateUnfinishedLogsForRecovery();

		/// <summary>
		/// Enumerates analytics log files stored by the storage system, excluding unfinished ones.
		/// The logs enumerated by this shall be ready for uploading.
		/// 
		/// Unfinished logs are ones that are either currently open for writing, i.e. the <see cref="Stream"/> returned by the call to <see cref="CreateLogFile(out ILogFile)"/> for the log file has not yet been disposed,
		/// or their writing <see cref="Stream"/> has been closed, but no call to <see cref="FinishLogFileAsync(ILogFile, CancellationToken)"/>  has completed for them.
		/// </summary>
		/// <returns>An enumerable to iterate over the objects representing the files and their metadata.</returns>
		IList<ILogFile> EnumerateLogs();
	}
}
