using System;
using System.Text;

namespace SGL.Analytics.Utilities.Logging.FileLogging {
	public static class LogMessageFormatters {
		public static Action<LogMessage, StringBuilder> Default { get; } = (msg, sb) => {
			sb.AppendFormat("[{0:O}] [{1}] [{2}]", msg.Time, msg.Level, msg.Category);
			if (msg.Scopes.Count > 0) {
				sb.AppendFormat(" [{0}]", msg.Scopes[0]);
			}
			sb.AppendFormat(": {0}", msg.Text);
			if (msg.Exception != null) {
				sb.AppendLine();
				sb.AppendFormat("\t{0}", msg.Exception);
			}
		};
	}
}
