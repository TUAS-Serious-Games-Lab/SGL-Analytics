using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class FakeLogCollectorClient : ILogCollectorClient {
		public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.NoContent;
		public List<Guid> UploadedLogFileIds { get; set; } = new();

		public async Task UploadLogFileAsync(string appName, string appAPIToken, Guid userID, ILogStorage.ILogFile logFile) {
			await Task.CompletedTask;
			var resp = new HttpResponseMessage(StatusCode);
			resp.EnsureSuccessStatusCode();
			UploadedLogFileIds.Add(logFile.ID);
		}
	}
}
