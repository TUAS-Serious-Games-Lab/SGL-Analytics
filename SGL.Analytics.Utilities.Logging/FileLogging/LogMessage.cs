using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	public struct LogMessage {
		public string Category { get; set; }
		public List<string> Scopes { get; set; }
		public LogLevel Level { get; set; }
		public EventId EventId { get; set; }
		public DateTime Time { get; set; }
		public string Text { get; set; }
		public Exception? Exception { get; set; }

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
