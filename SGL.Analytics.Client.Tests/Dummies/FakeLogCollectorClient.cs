using SGL.Analytics.DTO;
using SGL.Utilities;
using SGL.Utilities.Crypto.Certificates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class FakeLogCollectorClient : ILogCollectorClient {
		public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.NoContent;
		public List<Guid> UploadedLogFileIds { get; set; } = new();
		public List<Certificate> RecipientCertificates { get; set; } = new List<Certificate> { };

		/// <summary>
		/// Allows diabling the upload process completely instead of faking it or faking errors.
		/// Logfiles are then preserved locally.
		/// </summary>
		/// <remarks>Thread-safety is achieved by only allowing to set the value at the beginning of the object lifetime.</remarks>
		public bool IsActive { get; init; } = true;
		public AuthorizationData? Authorization { get; set; }

		public event AsyncEventHandler<AuthorizationExpiredEventArgs>? AuthorizationExpired;

		public Task LoadRecipientCertificatesAsync(CertificateStore targetCertificateStore, CancellationToken ct = default) {
			targetCertificateStore.AddCertificatesWithValidation(RecipientCertificates, nameof(FakeLogCollectorClient));
			return Task.CompletedTask;
		}

		public Task SetAuthorizationLockedAsync(AuthorizationData? value, CancellationToken ct = default) {
			Authorization = value;
			return Task.CompletedTask;
		}

		public async Task UploadLogFileAsync(LogMetadataDTO metadata, Stream content, CancellationToken ct = default) {
			await Task.CompletedTask;
			var resp = new HttpResponseMessage(StatusCode);
			resp.EnsureSuccessStatusCode();
			UploadedLogFileIds.Add(metadata.LogFileId);
		}
	}
}
