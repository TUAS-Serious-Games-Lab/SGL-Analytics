using SGL.Analytics.Backend.Domain.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Interfaces {
	public interface ILogMetadataRepository {
		Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId, CancellationToken ct = default);
		Task<LogMetadata?> GetLogMetadataByUserLocalIdAsync(Guid userAppId, Guid userId, Guid localLogId, CancellationToken ct = default);
		Task<LogMetadata> AddLogMetadataAsync(LogMetadata logMetadata, CancellationToken ct = default);
		Task<LogMetadata> UpdateLogMetadataAsync(LogMetadata logMetadata, CancellationToken ct = default);
	}
}
