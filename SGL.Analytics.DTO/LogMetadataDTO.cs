using System;

namespace SGL.Analytics.DTO {
	public record LogMetadataDTO(string AppName, Guid UserId, Guid LogFileId, DateTime CreationTime, DateTime EndTime);
}
