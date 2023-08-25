namespace SGL.Analytics.ExporterClient {
	/// <summary>
	/// Specifies the builder pattern interface for combining query criteria for log files
	/// in <see cref="SglAnalyticsExporter.GetLogFileMetadataAsync(Func{ILogFileQuery, ILogFileQuery}, CancellationToken)"/>,
	/// <see cref="SglAnalyticsExporter.GetDecryptedLogFilesAsync(ILogFileSink, Func{ILogFileQuery, ILogFileQuery}, CancellationToken)"/>,
	/// and <see cref="SglAnalyticsExporter.GetDecryptedLogFilesAsync(Func{ILogFileQuery, ILogFileQuery}, CancellationToken)"/>.
	/// </summary>
	public interface ILogFileQuery {
		/// <summary>
		/// Only return log files started after <paramref name="timestamp"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		ILogFileQuery StartedAfter(DateTime timestamp);
		/// <summary>
		/// Only return log files started before <paramref name="timestamp"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		ILogFileQuery StartedBefore(DateTime timestamp);
		/// <summary>
		/// Only return log files finished after <paramref name="timestamp"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		ILogFileQuery EndedAfter(DateTime timestamp);
		/// <summary>
		/// Only return log files finished before <paramref name="timestamp"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		ILogFileQuery EndedBefore(DateTime timestamp);
		/// <summary>
		/// Only return log files uploaded after <paramref name="timestamp"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		ILogFileQuery UploadedAfter(DateTime timestamp);
		/// <summary>
		/// Only return log files uploaded before <paramref name="timestamp"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		ILogFileQuery UploadedBefore(DateTime timestamp);
		/// <summary>
		/// Only return log files belonging to a user identified by one of the ids in <paramref name="userIds"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		ILogFileQuery UserOneOf(IEnumerable<Guid> userIds);
	}
}
