using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	internal class FileLogger : ILogger {
		private string categoryName;
		private FileLoggingProvider provider;
		private LogLevel MinLogLevel { get; }

		public FileLogger(string categoryName, FileLoggingProvider provider, LogLevel minLogLevel) {
			this.categoryName = categoryName;
			this.provider = provider;
			MinLogLevel = minLogLevel;
		}

		public IDisposable BeginScope<TState>(TState state) {
			return provider.Scopes.Push(state);
		}

		public bool IsEnabled(LogLevel logLevel) {
			return logLevel >= MinLogLevel;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
			if (logLevel < provider.MinLevel) return;
			var time = DateTime.Now;
			var formattedSopes = new List<string>();
			provider.Scopes.ForEachScope((scope, formatted) => formatted.Add(scope.ToString() ?? ""), formattedSopes);
			var formattedState = formatter(state, exception);
			provider.WriterQueue.Enqueue(new LogMessage(categoryName, formattedSopes, logLevel, eventId, time, formattedState, exception));
		}
	}
}
