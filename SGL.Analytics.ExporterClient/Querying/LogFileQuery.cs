using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	internal class LogFileQuery : ILogFileQuery {
		public ILogFileQuery EndedAfter(DateTime timestamp) {
			throw new NotImplementedException();
		}

		public ILogFileQuery EndedBefore(DateTime timestamp) {
			throw new NotImplementedException();
		}

		public ILogFileQuery StartedAfter(DateTime timestamp) {
			throw new NotImplementedException();
		}

		public ILogFileQuery StartedBefore(DateTime timestamp) {
			throw new NotImplementedException();
		}

		public ILogFileQuery UploadedAfter(DateTime timestamp) {
			throw new NotImplementedException();
		}

		public ILogFileQuery UploadedBefore(DateTime timestamp) {
			throw new NotImplementedException();
		}

		public ILogFileQuery UserOneOf(IEnumerable<Guid> userIds) {
			throw new NotImplementedException();
		}
	}
}
