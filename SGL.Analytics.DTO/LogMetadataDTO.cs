using System;
using System.ComponentModel.DataAnnotations;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Represents the different supported content encodings that can be used for an analytics log file.
	/// </summary>
	public enum LogContentEncoding {
		/// <summary>
		/// The log file content is uploaded as plain text JSON.
		/// </summary>
		Plain,
		/// <summary>
		/// The content as it would be for <see cref="Plain"/>, but compressed using the gzip algorithm.
		/// </summary>
		GZipCompressed
	}

	/// <summary>
	/// Specifies the metadata transferred along whith the contents when a client upload a game log file to the server.
	/// These properties are passed by custom headers, as the request body is already taken by the file content.
	/// </summary>
	public record LogMetadataDTO {
		public Guid LogFileId { get; init; }
		public DateTime CreationTime { get; init; }
		public DateTime EndTime { get; init; }
		[PlainName]
		[StringLength(16)]
		public string NameSuffix { get; init; }
		public LogContentEncoding LogContentEncoding { get; init; }

		public LogMetadataDTO(Guid logFileId, DateTime creationTime, DateTime endTime,
			[PlainName][StringLength(16)] string nameSuffix, LogContentEncoding logContentEncoding) =>
			(LogFileId, CreationTime, EndTime, NameSuffix, LogContentEncoding) = (logFileId, creationTime, endTime, nameSuffix, logContentEncoding);

		public void Deconstruct(out Guid logFileId, out DateTime creationTime, out DateTime endTime,
			out string nameSuffix, out LogContentEncoding logContentEncoding) {
			logFileId = LogFileId;
			creationTime = CreationTime;
			endTime = EndTime;
			nameSuffix = NameSuffix;
			logContentEncoding = LogContentEncoding;
		}
	}
}
