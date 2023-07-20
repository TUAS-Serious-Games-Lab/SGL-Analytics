using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Utilities.Backend;
using SGL.Utilities.Crypto.Keys;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

		private IQueryable<LogMetadata> ApplyQueryOptions(IQueryable<LogMetadata> query, LogMetadataQueryOptions? queryOptions) {
			if (queryOptions == null) {
				queryOptions = new LogMetadataQueryOptions();
			}
			query = query.Include(lmd => lmd.App);
			if (queryOptions.FetchRecipientKeys) {
				query = query.Include(lmd => lmd.RecipientKeys);
			}
			else if (queryOptions.FetchRecipientKey != null) {
				query = query.Include(lmd => lmd.RecipientKeys.Where(rk => rk.RecipientKeyId == queryOptions.FetchRecipientKey));
			}
			switch (queryOptions.Ordering) {
				case LogMetadataQuerySortCriteria.UserIdThenCreateTime:
					query = query.OrderBy(log => log.UserId).ThenBy(log => log.CreationTime);
					break;
				default:
					break;
			}
			if (queryOptions.Limit > 0) {
				query = query.Take(queryOptions.Limit);
			}
			if (queryOptions.Offset > 0) {
				query = query.Skip(queryOptions.Offset);
			}
			if (!queryOptions.ForUpdating) {
				query = query.AsNoTracking();
			}
			return query;
		}

		/// <inheritdoc/>
		public async Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId, LogMetadataQueryOptions? queryOptions = null, CancellationToken ct = default) {
			var query = context.LogMetadata.Where(lmd => lmd.Id == logId);
			query = ApplyQueryOptions(query, queryOptions);
			return await query.SingleOrDefaultAsync<LogMetadata?>(ct);
		}

		/// <inheritdoc/>
		public async Task<LogMetadata?> GetLogMetadataByUserLocalIdAsync(Guid userAppId, Guid userId, Guid localLogId, LogMetadataQueryOptions? queryOptions = null, CancellationToken ct = default) {
			var query = context.LogMetadata.Where(lmd => lmd.AppId == userAppId && lmd.UserId == userId && lmd.LocalLogId == localLogId);
			query = ApplyQueryOptions(query, queryOptions);
			return await query.SingleOrDefaultAsync<LogMetadata?>(ct);
		}

		/// <inheritdoc/>
		public async Task<IDictionary<string, int>> GetLogsCountPerAppAsync(CancellationToken ct = default) {
			var query = from lm in context.LogMetadata.Include(lmd => lmd.App)
						group lm by lm.App.Name into a
						select new { AppName = a.Key, LogsCount = a.Count() };
			return await query.AsNoTracking().ToDictionaryAsync(e => e.AppName, e => e.LogsCount, ct);
		}

		/// <inheritdoc/>
		public async Task<IDictionary<string, double>> GetLogSizeAvgPerAppAsync(CancellationToken ct = default) {
			var query = from lm in context.LogMetadata.Include(lmd => lmd.App)
						group lm.Size by lm.App.Name into a
						select new { AppName = a.Key, LogSizeAvg = a.Average() };
			return await query.AsNoTracking().ToDictionaryAsync(e => e.AppName, e => e.LogSizeAvg ?? 0, ct);
		}

		/// <inheritdoc/>
		public async Task<IEnumerable<LogMetadata>> ListLogMetadataForApp(Guid appId, bool? completenessFilter = null, KeyId? notForKeyId = null, LogMetadataQueryOptions? queryOptions = null, CancellationToken ct = default) {
			var query = context.LogMetadata.Where(lmd => lmd.AppId == appId);
			if (completenessFilter != null) {
				query = query.Where(lmd => lmd.Complete == completenessFilter);
			}
			if (notForKeyId != null) {
				query = query.Where(lmd => !lmd.RecipientKeys.Any(rk => rk.RecipientKeyId == notForKeyId));
			}
			query = ApplyQueryOptions(query, queryOptions);
			return await query.ToListAsync(ct);
		}

		/// <inheritdoc/>
		public async Task<LogMetadata> UpdateLogMetadataAsync(LogMetadata logMetadata, CancellationToken ct = default) {
			Debug.Assert(context.Entry(logMetadata).State is EntityState.Modified or EntityState.Unchanged);
			await context.SaveChangesAsync(ct);
			return logMetadata;
		}

		public async Task<IList<LogMetadata>> UpdateLogMetadataAsync(IList<LogMetadata> logMetadata, CancellationToken ct = default) {
			Debug.Assert(logMetadata.All(logMd => context.Entry(logMd).State is EntityState.Modified or EntityState.Unchanged));
			await context.SaveChangesAsync(ct);
			return logMetadata;
		}

		public async Task<IEnumerable<LogMetadata>> GetLogMetadataByIdsAsync(IReadOnlyCollection<Guid> logIds, LogMetadataQueryOptions? queryOptions = null, CancellationToken ct = default) {
			var logIdsSet = logIds.ToHashSet();
			var query = context.LogMetadata
				.Where(lmd => logIdsSet.Contains(lmd.Id));
			query = ApplyQueryOptions(query, queryOptions);
			return await query.ToListAsync(ct);
		}
	}
}
