using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	/// <summary>
	/// Represents the properties of a log message that is processed by an <see cref="ILogger{TCategoryName}"/> created from <see cref="FileLoggingProvider"/>.
	/// These properties are used for (custom) placeholder getters in message and filename formatting (as well as for internally passing the messages from the loggers to the sinks).
	/// </summary>
	public struct LogMessage {
		/// <summary>
		/// The category name of the <see cref="ILogger"/> that logged the message.
		/// </summary>
		public string Category { get; }
		/// <summary>
		/// String representations (using <see cref="object.ToString"/>) of the scopes that apply to the message.
		/// </summary>
		public List<string> Scopes { get; }
		/// <summary>
		/// The log verbosity level of the message.
		/// </summary>
		public LogLevel Level { get; }
		/// <summary>
		/// The event id for the message that was passed to the logger.
		/// </summary>
		public EventId EventId { get; }
		/// <summary>
		/// The timestamp when the call to the logging method was made.
		/// </summary>
		public DateTime Time { get; }
		/// <summary>
		/// The actual text of the log message with log method level placeholders already inserted.
		/// </summary>
		public string Text { get; }
		/// <summary>
		/// The exception associated with the message, or <see langword="null"/> if no exception was passed to the logging method.
		/// </summary>
		public Exception? Exception { get; }

		internal LogMessage(string category, List<string> scopes, LogLevel level, EventId eventId, DateTime time, string text, Exception? exception) {
			Category = category;
			Scopes = scopes;
			Level = level;
			EventId = eventId;
			Time = time;
			Text = text;
			Exception = exception;
		}
	}
}
