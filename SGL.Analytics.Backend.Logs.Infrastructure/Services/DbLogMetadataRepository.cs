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
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {
	/// <summary>
	/// Provides a persistent implementation of <see cref="ILogMetadataRepository"/> using Entity Framework Core to map the objects into a relational database.
	/// </summary>
	public class DbLogMetadataRepository : ILogMetadataRepository {
		private LogsContext context;

		/// <summary>
		/// Creates a repository object using the given database context object for data access.
		/// </summary>
		/// <param name="context">The <see cref="DbContext"/> implementation for the database.</param>
		public DbLogMetadataRepository(LogsContext context) {
			this.context = context;
		}

		/// <inheritdoc/>
		public async Task<LogMetadata> AddLogMetadataAsync(LogMetadata logMetadata, CancellationToken ct = default) {
			context.LogMetadata.Add(logMetadata);
			try {
				await context.SaveChangesAsync(ct);
			}
			catch (DbUpdateConcurrencyException ex) {
				throw new ConcurrencyConflictException(ex);
			}
			catch (DbUpdateException ex) {
				// Should happen rarely and unfortunately, at the time of writing, there is no portable way (between databases) of further classifying the error.
				// To check if ex is a unique constraint violation, we would need to inspect its inner exception and switch over exception types for all supported providers and their internal error classifications.
				// To avoid this coupling, rather pay the perf cost of querrying again in this rare case.
				if (await context.LogMetadata.CountAsync(lm => lm.Id == logMetadata.Id, ct) > 0) {
					throw new EntityUniquenessConflictException("LogMetadata", "Id", logMetadata.Id, ex);
				}
				else throw;
			}

			return logMetadata;
		}

		/// <inheritdoc/>
		public async Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId, CancellationToken ct = default) {
			return await context.LogMetadata.Include(lmd => lmd.App).Where(lmd => lmd.Id == logId).SingleOrDefaultAsync<LogMetadata?>(ct);
		}

		/// <inheritdoc/>
		public async Task<LogMetadata?> GetLogMetadataByUserLocalIdAsync(Guid userAppId, Guid userId, Guid localLogId, CancellationToken ct = default) {
			return await context.LogMetadata.Include(lmd => lmd.App).Where(lmd => lmd.AppId == userAppId && lmd.UserId == userId && lmd.LocalLogId == localLogId).SingleOrDefaultAsync<LogMetadata?>(ct);
		}

		/// <inheritdoc/>
		public async Task<LogMetadata> UpdateLogMetadataAsync(LogMetadata logMetadata, CancellationToken ct = default) {
			Debug.Assert(context.Entry(logMetadata).State is EntityState.Modified or EntityState.Unchanged);
			await context.SaveChangesAsync(ct);
			return logMetadata;
		}
	}
}
