using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities.Logging.FileLogging {

	/// <summary>
	/// A builder interface, used to customize initialization of a <see cref="FileLoggingProvider"/>, following the builder pattern.
	/// It can be used to add additional placeholders beyond the directly message-based placeholders provided by default.
	/// </summary>
	public interface IFileLoggingProviderBuilder {
		/// <summary>
		/// Adds a placeholder with the given name and using the given delegate to obtain its value for a given message.
		/// </summary>
		/// <param name="name">The placeholder name to define.</param>
		/// <param name="valueGetter">A getter delegate to obtain the value.</param>
		void AddPlaceholder(string name, PlaceholderValueGetter<LogMessage> valueGetter);
	}

	/// <summary>
	/// Provides logging functionality using simple log files written to the (local) filesystem.
	/// The messages logged to loggers created from this provider are queued and asynchronously written to arbitrarily many logging sinks in the background.
	/// </summary>
	/// <remarks>
	/// The logging sinks are configured using the options passed to the constructors and can be used to filter and categorize the messages into different log files,
	/// based on their properties and to format them appropriately for different purposes.
	/// Both, the log file paths and the message format of a sink use format strings with named placeholders for the log message data.
	/// See <see cref="NamedPlaceholderFormatterFactory{T}"/> for the format string syntax used.
	/// Using those placeholders for filenames allows things like per-category, per-day, per-month, per-loglevel, etc. files and subdirectories.
	/// For example, <c>logs/{Category}/{Time:yyyy}-{Level}.log</c> generates a per-category subdirectories, containing per-year-and-loglevel files.
	/// The resulting path strings are sanitized using a positive list of valid characters.
	/// This list consists of <see cref="System.IO.Path.DirectorySeparatorChar"/>, <see cref="System.IO.Path.AltDirectorySeparatorChar"/>, letters, digits, and the special characters <c>".-()[]"</c>.
	/// All other characters are replaced by <c>'_'</c>.
	///
	/// The following placeholders are supported by default:
	/// <list type="table">
	/// <listheader>
	/// <term>Name</term>
	/// <term>Type</term>
	/// <term>Value / Meaning</term>
	/// </listheader>
	/// <item><term>AppDomainName</term><term><see cref="string"/></term><description>The name of the application domain as given by <c>AppDomain.CurrentDomain.FriendlyName</c>.</description></item>
	/// <item><term>Category</term><term><see cref="string"/></term><description>The category name of the logger from which the message originates.</description></item>
	/// <item><term>ScopesJoined</term><term><see cref="string"/></term><description>The string representations of the scopes applicable for the message, joined using <c>';'</c>.</description></item>
	/// <item><term>Scope0</term><term><see cref="string"/></term><description>The string representation of the first scope applicable for the message.</description></item>
	/// <item><term>Scope1</term><term><see cref="string"/></term><description>The string representation of the second scope applicable for the message.</description></item>
	/// <item><term>Scope2</term><term><see cref="string"/></term><description>The string representation of the third scope applicable for the message.</description></item>
	/// <item><term>Scope3</term><term><see cref="string"/></term><description>The string representation of the fourth scope applicable for the message.</description></item>
	/// <item><term>Scope4</term><term><see cref="string"/></term><description>The string representation of the fifth scope applicable for the message.</description></item>
	/// <item><term>Scope5</term><term><see cref="string"/></term><description>The string representation of the sixth scope applicable for the message.</description></item>
	/// <item><term>Scope6</term><term><see cref="string"/></term><description>The string representation of the seventh scope applicable for the message.</description></item>
	/// <item><term>Scope7</term><term><see cref="string"/></term><description>The string representation of the eigth scope applicable for the message.</description></item>
	/// <item><term>Level</term><term><see cref="LogLevel"/></term><description>The log verbosity level with which the message is associated.</description></item>
	/// <item><term>EventId</term><term><see cref="EventId"/></term><description>The event id for the message that was passed to the logger.</description></item>
	/// <item><term>Time</term><term><see cref="DateTime"/></term><description>The timestamp when the call to the logging method was made.</description></item>
	/// <item><term>Text</term><term><see cref="string"/></term><description>The actual message text of the message with log method level placeholders already inserted. This should not be used when formatting filename as it is typically unreasonably long for this purpose.</description></item>
	/// <item><term>Exception</term><term><see cref="string"/></term><description>A string representation of the exception associated with the message, including the stack trace, or an empty string if the message has no associated exception. This should not be used when formatting filename as it is typically unreasonably long for this purpose.</description></item>
	/// </list>
	/// </remarks>
	[ProviderAlias("File")]
	public class FileLoggingProvider : ILoggerProvider, IAsyncDisposable {
		private FileLoggingProviderOptions options;
		internal AsyncConsumerQueue<LogMessage> WriterQueue = new();
		internal LoggerExternalScopeProvider Scopes = new LoggerExternalScopeProvider();
		private List<FileLoggingSink> sinks;
		private Task writerWorkerHandle;
		private bool disposed = false;
		internal LogLevel MinLevel;

		private Action<INamedPlaceholderFormatterFactoryBuilder<LogMessage>> formaterFactoryBuilder;
		private NamedPlaceholderFormatterFactory<LogMessage> formatterFactory;
		private NamedPlaceholderFormatterFactory<LogMessage> formatterFactoryFixedTime;

		private Task startWriterWorker() {
			return Task.Run(async () => {
				await foreach (var msg in WriterQueue.DequeueAllAsync().ConfigureAwait(false)) {
					await Task.WhenAll(sinks.Select(sink => sink.WriteAsync(msg)).ToArray()).ConfigureAwait(false);
				}
			});
		}

		private class Builder : IFileLoggingProviderBuilder {
			INamedPlaceholderFormatterFactoryBuilder<LogMessage> formatterFactoryBuilder;

			public Builder(INamedPlaceholderFormatterFactoryBuilder<LogMessage> formatterFactoryBuilder) {
				this.formatterFactoryBuilder = formatterFactoryBuilder;
			}

			public void AddPlaceholder(string name, PlaceholderValueGetter<LogMessage> valueGetter) {
				formatterFactoryBuilder.AddPlaceholder(name, valueGetter);
			}
		}

		/// <summary>
		/// Constructs a <see cref="FileLoggingProvider"/> by passing the unwrapped <see cref="IOptions{FileLoggingProviderOptions}"/> and the given builder to <see cref="FileLoggingProvider(FileLoggingProviderOptions,Action{IFileLoggingProviderBuilder})"/>.
		/// </summary>
		/// <param name="options">The configuration options for the file logging provider, wrapped in an <see cref="IOptions{FileLoggingProviderOptions}"/>.</param>
		/// <param name="logProvBuilder">A builder delegate that can be used to add custom placeholders to the provider.</param>
		public FileLoggingProvider(IOptions<FileLoggingProviderOptions> options, Action<IFileLoggingProviderBuilder> logProvBuilder) :
			this(options.Value, logProvBuilder) { }

		/// <summary>
		/// Constructs a <see cref="FileLoggingProvider"/>, configured using the given options and the given builder.
		/// The options describe general configuration parameters, default configuration parameters for sinks, and a list of sink configurations.
		/// Per-sink configuration parameters include a base directory, format strings for the messages and file paths, and filters based on category and level.
		/// </summary>
		/// <param name="options">The configuration options for the file logging provider.</param>
		/// <param name="logProvBuilder">A builder delegate that can be used to add custom placeholders to the provider.</param>
		public FileLoggingProvider(FileLoggingProviderOptions options, Action<IFileLoggingProviderBuilder> logProvBuilder) {
			this.options = options;
			MinLevel = options.Sinks.Any() ? options.Sinks.Select(s => s.MinLevel).Min() : LogLevel.None;
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
				builder.SetFallbackValueGetter((name, msg) => this.options.Constants.TryGetValue(name, out var val) ? val : "");
				logProvBuilder(new Builder(builder));
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
			sinks = options.Sinks.Select(s => new FileLoggingSink(s,
				formatterFactory.Create(s.BaseDirectory ?? options.BaseDirectory),
				formatterFactory.Create(s.MessageFormat ?? options.MessageFormat),
				formatterFactory.Create(s.MessageFormatException ?? options.MessageFormatException),
				formatterFactory.Create(s.FilenameFormat ?? options.FilenameFormat),
				formatterFactoryFixedTime.Create(s.FilenameFormat ?? options.FilenameFormat)
				)).ToList();
			writerWorkerHandle = startWriterWorker();
		}

		/// <inheritdoc/>
		public ILogger CreateLogger(string categoryName) {
			return new FileLogger(categoryName, this, LogLevel.Trace);
		}

		/// <summary>
		/// Cleanly shuts down the logging provider asynchronously by finishing the message queue, waiting for the background writer process to drain it and then disposing the sinks to close all open file streams.
		/// </summary>
		/// <returns>A task representing the asynchronous operation.</returns>
		public async ValueTask DisposeAsync() {
			if (disposed) return;
			disposed = true;
			WriterQueue.Finish();
			await writerWorkerHandle.ConfigureAwait(false);
			foreach (var sink in sinks) {
				await sink.DisposeAsync().ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Cleanly shuts down the logging provider by finishing the message queue, waiting for the background writer process to drain it and then disposing the sinks to close all open file streams.
		/// </summary>
		public void Dispose() {
			DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
		}
	}
}
