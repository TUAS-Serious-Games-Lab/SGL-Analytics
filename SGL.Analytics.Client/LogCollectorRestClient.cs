using System;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public class LogCollectorRestClient : ILogCollectorClient {

		public Task UploadLogFileAsync(string appName, string appAPIToken, Guid userID, ILogStorage.ILogFile logFile) {
			throw new NotImplementedException();
		}
	}
}