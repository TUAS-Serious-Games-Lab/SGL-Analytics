using SGL.Analytics.Backend.Domain.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	public interface ILogMetadataRepository {
		Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId);
		Task<LogMetadata?> GetLogMetadataByUserLocalIdAsync(Guid userAppId, Guid userId, Guid localLogId);
		Task<LogMetadata> AddLogMetadataAsync(LogMetadata logMetadata);
		Task<LogMetadata> UpdateLogMetadataAsync(LogMetadata logMetadata);
	}
}
