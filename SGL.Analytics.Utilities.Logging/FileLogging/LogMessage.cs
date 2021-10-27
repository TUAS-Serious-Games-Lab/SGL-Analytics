using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	public struct LogMessage {
		public string Category { get; }
		public List<string> Scopes { get; }
		public LogLevel Level { get; }
		public EventId EventId { get; }
		public DateTime Time { get; }
		public string Text { get; }
		public Exception? Exception { get; }

		public LogMessage(string category, List<string> scopes, LogLevel level, EventId eventId, DateTime time, string text, Exception? exception) {
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
