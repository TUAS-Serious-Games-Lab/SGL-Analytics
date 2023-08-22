using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Exceptions {
	/// <summary>
	/// An exception type thrown when a requested log file was not found.
	/// </summary>
	public class LogNotFoundException : Exception {
		/// <summary>
		/// Creates a new exception object with the given data.
		/// </summary>
		public LogNotFoundException(string? message, Guid logId, Exception? innerException = null) : base(message, innerException) {
			LogId = logId;
		}
		/// <summary>
		/// The id of the log that was not found.
		/// </summary>
		public Guid LogId { get; }
	}
	/// <summary>
	/// An exception type thrown when a requested user registration was not found.
	/// </summary>
	public class UserNotFoundException : Exception {
		/// <summary>
		/// Creates a new exception object with the given data.
		/// </summary>
		public UserNotFoundException(string? message, Guid userId, Exception? innerException = null) : base(message, innerException) {
			UserId = userId;
		}
		/// <summary>
		/// The id of the user registration that was not found.
		/// </summary>
		public Guid UserId { get; }
	}
}
