using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	[ProviderAlias("File")]
	public class FileLoggingProvider : ILoggerProvider, IAsyncDisposable {
		internal AsyncConsumerQueue<LogMessage> WriterQueue = new();
		internal LoggerExternalScopeProvider Scopes = new LoggerExternalScopeProvider();
		private List<FileLoggingSink> sinks;
		private Task writerWorkerHandle;
		private bool disposed = false;

		private Task startWriterWorker() {
			return Task.Run(async () => {
				await foreach (var msg in WriterQueue.DequeueAllAsync()) {
					foreach (var sink in sinks) {
						await sink.WriteAsync(msg);
					}
				}
			});
		}

		public FileLoggingProvider(IOptions<FileLoggingProviderOptions> options) : this(options.Value) { }

		public FileLoggingProvider(FileLoggingProviderOptions options) {
			sinks = options.Sinks.Select(s => new FileLoggingSink(s)).ToList();
			writerWorkerHandle = startWriterWorker();
		}

		public ILogger CreateLogger(string categoryName) {
			return new FileLogger(categoryName, this, LogLevel.Trace);
		}

		public async ValueTask DisposeAsync() {
			if (disposed) return;
			disposed = true;
			WriterQueue.Finish();
			await writerWorkerHandle;
			foreach (var sink in sinks) {
				await sink.DisposeAsync();
			}
		}

		public void Dispose() {
			DisposeAsync().AsTask().Wait();
		}
	}
}
