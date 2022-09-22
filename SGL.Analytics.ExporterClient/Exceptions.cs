using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	public class KeyFileException : Exception {
		public KeyFileException(string? message, Exception? innerException = null) : base(message, innerException) { }
	}
}
