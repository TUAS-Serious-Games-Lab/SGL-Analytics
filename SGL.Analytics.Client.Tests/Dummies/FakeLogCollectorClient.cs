using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class FakeLogCollectorClient : ILogCollectorClient {
		public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.NoContent;
		public List<Guid> UploadedLogFileIds { get; set; } = new();

		public bool IsActive { get; init; } = true;

		public async Task UploadLogFileAsync(string appName, string appAPIToken, Guid userID, ILogStorage.ILogFile logFile) {
			await Task.CompletedTask;
			var resp = new HttpResponseMessage(StatusCode);
			resp.EnsureSuccessStatusCode();
			UploadedLogFileIds.Add(logFile.ID);
		}
	}
}
