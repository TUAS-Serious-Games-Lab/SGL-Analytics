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
	public record LogMetadataDTO(Guid LogFileId, DateTime CreationTime, DateTime EndTime, [PlainName][StringLength(16)] string NameSuffix, LogContentEncoding LogContentEncoding);
}
