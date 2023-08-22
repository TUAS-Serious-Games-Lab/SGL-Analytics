using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	/// <summary>
	/// An exception type thrown when a user key file couldn't be loaded correctly.
	/// </summary>
	public class KeyFileException : Exception {
		/// <summary>
		/// Creates a new exception object with the given data.
		/// </summary>
		public KeyFileException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
}
