using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {
	public class DbLogMetadataRepository : ILogMetadataRepository {
		private LogsContext context;

		public DbLogMetadataRepository(LogsContext context) {
			this.context = context;
		}

		public async Task<LogMetadata> AddLogMetadataAsync(LogMetadata logMetadata) {
			await context.LogMetadata.AddAsync(logMetadata);
			await context.SaveChangesAsync();
			return logMetadata;
		}

		public async Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId) {
			return await context.LogMetadata.Include(lmd => lmd.App).Where(lmd => lmd.Id == logId).SingleOrDefaultAsync<LogMetadata?>();
		}

		public async Task<LogMetadata?> GetLogMetadataByUserLocalIdAsync(Guid userAppId, Guid userId, Guid localLogId) {
			return await context.LogMetadata.Include(lmd => lmd.App).Where(lmd => lmd.AppId == userAppId && lmd.UserId == userId && lmd.LocalLogId == localLogId).SingleOrDefaultAsync<LogMetadata?>();
		}

		public async Task<LogMetadata> UpdateLogMetadataAsync(LogMetadata logMetadata) {
			Debug.Assert(context.Entry(logMetadata).State == EntityState.Modified);
			await context.SaveChangesAsync();
			return logMetadata;
		}
	}
}
