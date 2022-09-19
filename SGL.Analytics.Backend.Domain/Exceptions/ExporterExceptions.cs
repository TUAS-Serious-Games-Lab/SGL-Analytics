using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Exceptions {
	public class LogNotFoundException : Exception {
		public LogNotFoundException(string? message, Guid logId, Exception? innerException = null) : base(message, innerException) {
			LogId = logId;
		}
		public Guid LogId { get; }
	}
}
