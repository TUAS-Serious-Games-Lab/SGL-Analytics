namespace SGL.Analytics.ExporterClient {
	public interface ILogFileQuery {
		ILogFileQuery StartedAfter(DateTime timestamp);
		ILogFileQuery StartedBefore(DateTime timestamp);
		ILogFileQuery EndedAfter(DateTime timestamp);
		ILogFileQuery EndedBefore(DateTime timestamp);
		ILogFileQuery UploadedAfter(DateTime timestamp);
		ILogFileQuery UploadedBefore(DateTime timestamp);
		ILogFileQuery UserOneOf(IEnumerable<Guid> userIds);
	}
}
