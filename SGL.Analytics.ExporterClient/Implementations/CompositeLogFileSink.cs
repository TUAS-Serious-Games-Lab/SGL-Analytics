using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient.Implementations {
	/// <summary>
	/// Provides an <see cref="ILogFileSink"/> implementation that delegates to arbitrarily many other sink objects
	/// to allow using multiple sink objects on a <see cref="SglAnalyticsExporter.GetDecryptedLogFilesAsync(ILogFileSink, Func{ILogFileQuery, ILogFileQuery}, CancellationToken)"/>
	/// call, that should all receive the decrypted data.
	/// The <see cref="ILogFileSink.ProcessLogFileAsync(LogFileMetadata, Stream?, CancellationToken)"/>
	/// methods on constituent sinks can be invoked sequentially or concurrently, controlled by <see cref="RunConcurrently"/>.
	/// Incoming log file contents are cached in memory to allow each sink to read the content from the start,
	/// even though the original stream object is not necessarily rewindable.
	/// </summary>
	public class CompositeLogFileSink : ILogFileSink {
		/// <summary>
		/// Constructs a composite sink with the list of constituent sinks given in <paramref name="innerSinks"/>.
		/// </summary>
		/// <param name="innerSinks">The list of sinks to which calls shall be delegated.</param>
		/// <param name="runConcurrently">True if the sink shall invoke the <paramref name="innerSinks"/> concurrently on the thread pool.
		/// False if they shall be invoked sequentially.</param>
		public CompositeLogFileSink(IList<ILogFileSink> innerSinks, bool runConcurrently = false) {
			InnerSinks = innerSinks;
			RunConcurrently = runConcurrently;
		}

		/// <summary>
		/// True if the sink invokes the <see cref="InnerSinks"/> concurrently on the thread pool.
		/// False if they are invoked sequentially.
		/// </summary>
		public bool RunConcurrently { get; }

		/// <summary>
		/// The list of sinks to which calls shall be delegated.
		/// </summary>
		public IList<ILogFileSink> InnerSinks { get; }

		/// <summary>
		/// Implements <see cref="ILogFileSink.ProcessLogFileAsync(LogFileMetadata, Stream?, CancellationToken)"/> 
		/// by delegating each call to all sinks in <see cref="InnerSinks"/>.
		/// </summary>
		public async Task ProcessLogFileAsync(LogFileMetadata metadata, Stream? content, CancellationToken ct) {
			byte[] contentBuffer = Array.Empty<byte>();
			if (content != null) {
				using (var inputBuffer = new MemoryStream()) {
					await content.CopyToAsync(inputBuffer, ct);
					contentBuffer = inputBuffer.ToArray();
				}
			}
			if (RunConcurrently) {
				await Task.WhenAll(InnerSinks.Select(innerSink => {
					if (content != null) {
						using var contentStream = new MemoryStream(contentBuffer, writable: false);
						return Task.Run(() => innerSink.ProcessLogFileAsync(metadata, contentStream, ct), ct);
					}
					else {
						return Task.Run(() => innerSink.ProcessLogFileAsync(metadata, null, ct), ct);
					}
				}));
			}
			else {
				foreach (var innerSink in InnerSinks) {
					ct.ThrowIfCancellationRequested();
					if (content != null) {
						using var contentStream = new MemoryStream(contentBuffer, writable: false);
						await innerSink.ProcessLogFileAsync(metadata, contentStream, ct);
					}
					else {
						await innerSink.ProcessLogFileAsync(metadata, null, ct);
					}
				}
			}
		}
	}
}
