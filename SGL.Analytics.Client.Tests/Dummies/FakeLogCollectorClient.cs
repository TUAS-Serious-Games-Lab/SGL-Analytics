using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class FakeLogCollectorClient : ILogCollectorClient {
		public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.NoContent;
		public List<Guid> UploadedLogFileIds { get; set; } = new();

		/// <summary>
		/// Allows diabling the upload process completely instead of faking it or faking errors.
		/// Logfiles are then preserved locally.
		/// </summary>
		/// <remarks>Thread-safety is achieved by only allowing to set the value at the beginning of the object lifetime.</remarks>
		public bool IsActive { get; init; } = true;

		public Task LoadRecipientCertificatesAsync(string appName, string appAPIToken, AuthorizationToken authToken, CertificateStore targetCertificateStore) {
			throw new NotImplementedException();
		}

		public async Task UploadLogFileAsync(string appName, string appAPIToken, AuthorizationToken authToken, ILogStorage.ILogFile logFile) {
			await Task.CompletedTask;
			var resp = new HttpResponseMessage(StatusCode);
			resp.EnsureSuccessStatusCode();
			UploadedLogFileIds.Add(logFile.ID);
		}
	}
}
