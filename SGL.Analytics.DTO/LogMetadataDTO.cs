using System;

namespace SGL.Analytics.DTO {
	public record LogMetadataDTO(Guid LogFileId, DateTime CreationTime, DateTime EndTime);
}
