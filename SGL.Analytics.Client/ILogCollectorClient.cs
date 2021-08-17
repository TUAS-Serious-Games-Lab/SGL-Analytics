using System;
using System.Threading.Tasks;

namespace SGL.Analytics.Client {
	public interface ILogCollectorClient {
		Task UploadLogFileAsync(string appName, string appAPIToken, Guid userID, ILogStorage.ILogFile logFile);
	}
}