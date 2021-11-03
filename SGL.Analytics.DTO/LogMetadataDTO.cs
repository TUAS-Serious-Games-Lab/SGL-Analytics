using System;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies the metadata transferred along whith the contents when a client upload a game log file to the server.
	/// These properties are passed by custom headers, as the request body is already taken by the file content.
	/// </summary>
	public record LogMetadataDTO(Guid LogFileId, DateTime CreationTime, DateTime EndTime);
}
