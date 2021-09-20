using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
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
			context.LogMetadata.Add(logMetadata);
			try {
				await context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException ex) {
				throw new ConcurrencyConflictException(ex);
			}
			catch (DbUpdateException ex) {
				// Should happen rarely and unfortunately, at the time of writing, there is no portable way (between databases) of further classifying the error.
				// To check if ex is a unique constraint violation, we would need to inspect its inner exception and switch over exception types for all supported providers and their internal error classifications.
				// To avoid this coupling, rather pay the perf cost of querrying again in this rare case.
				if (await context.LogMetadata.CountAsync(lm => lm.Id == logMetadata.Id) > 0) {
					throw new EntityUniquenessConflictException("LogMetadata", "Id", logMetadata.Id, ex);
				}
				else throw;
			}

			return logMetadata;
		}

		public async Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId) {
			return await context.LogMetadata.Include(lmd => lmd.App).Where(lmd => lmd.Id == logId).SingleOrDefaultAsync<LogMetadata?>();
		}

		public async Task<LogMetadata?> GetLogMetadataByUserLocalIdAsync(Guid userAppId, Guid userId, Guid localLogId) {
			return await context.LogMetadata.Include(lmd => lmd.App).Where(lmd => lmd.AppId == userAppId && lmd.UserId == userId && lmd.LocalLogId == localLogId).SingleOrDefaultAsync<LogMetadata?>();
		}

		public async Task<LogMetadata> UpdateLogMetadataAsync(LogMetadata logMetadata) {
			Debug.Assert(context.Entry(logMetadata).State is EntityState.Modified or EntityState.Unchanged);
			await context.SaveChangesAsync();
			return logMetadata;
		}
	}
}
