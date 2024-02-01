using SGL.Utilities.Crypto.Certificates;
using SGL.Utilities.Crypto.Keys;

namespace SGL.Analytics.ExporterClient {
	public partial class SglAnalyticsExporter : IAsyncDisposable {
		/// <summary>
		/// Acts as an alias for <see cref="RekeyLogFilesForRecipientKeyAsync(KeyId, ICertificateValidator, CancellationToken)"/>, kept for backwards compatibility.
		/// Use <see cref="RekeyLogFilesForRecipientKeyAsync(KeyId, ICertificateValidator, CancellationToken)"/> instead.
		/// </summary>
		[Obsolete("Use " + nameof(RekeyLogFilesForRecipientKeyAsync) + " instead.")]
		public Task<RekeyingOperationResult> RekeyLogFilesForRecipientKey(KeyId keyIdToGrantAccessTo, ICertificateValidator keyCertValidator, CancellationToken ct = default)
			=> RekeyLogFilesForRecipientKeyAsync(keyIdToGrantAccessTo, keyCertValidator, ct);
		/// <summary>
		/// Acts as an alias for <see cref="RekeyUserRegistrationsForRecipientKeyAsync(KeyId, ICertificateValidator, CancellationToken)"/>, kept for backwards compatibility.
		/// Use <see cref="RekeyUserRegistrationsForRecipientKeyAsync(KeyId, ICertificateValidator, CancellationToken)"/> instead.
		/// </summary>
		[Obsolete("Use " + nameof(RekeyUserRegistrationsForRecipientKeyAsync) + " instead.")]
		public Task<RekeyingOperationResult> RekeyUserRegistrationsForRecipientKey(KeyId keyIdToGrantAccessTo, ICertificateValidator keyCertValidator, CancellationToken ct = default)
			=> RekeyUserRegistrationsForRecipientKeyAsync(keyIdToGrantAccessTo, keyCertValidator, ct);
	}
}