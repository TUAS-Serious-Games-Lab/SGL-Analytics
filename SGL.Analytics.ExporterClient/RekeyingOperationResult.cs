namespace SGL.Analytics.ExporterClient {
	public class RekeyingOperationResult {
		public int TotalToRekey { get; internal set; } = 0;
		public int Successful { get; internal set; } = 0;
		public int SkippedDueToError { get; internal set; } = 0;
		public int Unencrypted { get; internal set; } = 0;
	}
}