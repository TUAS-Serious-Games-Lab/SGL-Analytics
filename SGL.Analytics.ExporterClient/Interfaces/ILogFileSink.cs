using SGL.Analytics.ExporterClient.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	/// <summary>
	/// Specifies the interface for a callback component that consumes retrieved and decrypted analytics log files for further processing.
	/// </summary>
	public interface ILogFileSink {
		/// <summary>
		/// Is called when the client has retrieved and decrypted an analytics log file and passes the data along for asynchronous processing.
		/// </summary>
		/// <param name="metadata">The metadata of the log file for processing.</param>
		/// <param name="content">
		/// The decrypted content of the log file for processing.
		/// If the log could not be decrypted, <see langword="null"/> is passed.
		/// </param>
		/// <param name="ct">A <see cref="CancellationToken"/> that is cancelled when the retrieval and processing method if cancelled.</param>
		/// <returns>A task object representing the asynchronous operation.</returns>
		/// <remarks>
		/// Implementations can use <see cref="SglAnalyticsExporter.ParseLogEntriesAsync"/> to parse <paramref name="content"/> into a stream of <see cref="LogFileEntry"/> values.
		/// </remarks>
		Task ProcessLogFileAsync(LogFileMetadata metadata, Stream? content, CancellationToken ct);
	}
}
