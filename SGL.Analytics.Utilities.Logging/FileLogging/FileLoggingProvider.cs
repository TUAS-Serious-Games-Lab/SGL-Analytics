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

		private Action<INamedPlaceholderFormatterFactoryBuilder<LogMessage>> formaterFactoryBuilder;
		private NamedPlaceholderFormatterFactory<LogMessage> formatterFactory;
		private NamedPlaceholderFormatterFactory<LogMessage> formatterFactoryFixedTime;
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
			formaterFactoryBuilder = builder => {
				builder.AddPlaceholder("AppDomainName", m => AppDomain.CurrentDomain.FriendlyName);
				builder.AddPlaceholder("Category", m => m.Category);
				builder.AddPlaceholder("ScopesJoined", m => string.Join(";", m.Scopes));
				builder.AddPlaceholder("Scope0", m => m.Scopes.FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope1", m => m.Scopes.Skip(1).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope2", m => m.Scopes.Skip(2).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope3", m => m.Scopes.Skip(3).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope4", m => m.Scopes.Skip(4).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope5", m => m.Scopes.Skip(5).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope6", m => m.Scopes.Skip(6).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Scope7", m => m.Scopes.Skip(7).FirstOrDefault() ?? "");
				builder.AddPlaceholder("Level", m => m.Level);
				builder.AddPlaceholder("EventId", m => m.EventId);
				builder.AddPlaceholder("Time", m => m.Time);
				builder.AddPlaceholder("Text", m => m.Text);
				builder.AddPlaceholder("Exception", m => m.Exception?.ToString() ?? "");
			};
			formatterFactory = new NamedPlaceholderFormatterFactory<LogMessage>(formaterFactoryBuilder);
			formatterFactoryFixedTime = new NamedPlaceholderFormatterFactory<LogMessage>(builder => {
				formaterFactoryBuilder(builder);
				// For writers with time-based filenames we need to replace out-dated writers instead of just opening new ones.
				// Therefore, we need a version of the formatted name that is time-independent as a key to compare if an existing
				// writer is otherwise for the same file, i.e. for the predecessor.
				// For this, we use this factory to always format the strings with epoch for the time-based placeholders.
				builder.AddPlaceholder("Time", m => DateTime.UnixEpoch);
			});
			sinks = options.Sinks.Select(s => new FileLoggingSink(s, formatterFactory, formatterFactoryFixedTime)).ToList();
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
