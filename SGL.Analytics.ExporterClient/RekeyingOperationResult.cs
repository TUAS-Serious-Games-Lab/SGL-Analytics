namespace SGL.Analytics.ExporterClient {
	/// <summary>
	/// Describes the result of a rekeying operation, where a recipient user grants access for end-to-end encrypted data to another recipient user.
	/// </summary>
	public class RekeyingOperationResult {
		/// <summary>
		/// The total number of data objects in the rekeying operation.
		/// </summary>
		public int TotalToRekey { get; internal set; } = 0;
		/// <summary>
		/// The number of successfully rekeyed data objects.
		/// </summary>
		public int Successful { get; internal set; } = 0;
		/// <summary>
		/// The number of data objects that could not be rekeyed due to an error,
		/// either server-side ones or decryption problems.
		/// </summary>
		public int SkippedDueToError { get; internal set; } = 0;
		/// <summary>
		/// The number of data objects that were not rekeyed because they were not encrypted at all, usually this is only the case for legacy data.
		/// </summary>
		public int Unencrypted { get; internal set; } = 0;
	}
}
