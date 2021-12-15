using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Tests.Dummies {
	public class DummyLogMetadataRepository : ILogMetadataRepository {
		private Dictionary<Guid, LogMetadata> logs = new();

		public Dictionary<Guid, LogMetadata> Logs => logs;

		public async Task<LogMetadata> AddLogMetadataAsync(LogMetadata logMetadata, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (logs.ContainsKey(logMetadata.Id)) throw new EntityUniquenessConflictException("LogMetadata", "Id", logMetadata.Id);
			logs.Add(logMetadata.Id, logMetadata);
			return logMetadata;
		}

		public async Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (logs.TryGetValue(logId, out var logMd)) {
				return logMd;
			}
			else {
				return null;
			}
		}

		public async Task<LogMetadata?> GetLogMetadataByUserLocalIdAsync(Guid userAppId, Guid userId, Guid localLogId, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			return logs.Values.Where(lm => lm.AppId == userAppId && lm.UserId == userId && lm.LocalLogId == localLogId).SingleOrDefault<LogMetadata?>();
		}

		public async Task<IDictionary<string, int>> GetLogsCountPerAppAsync(CancellationToken ct = default) {
			await Task.CompletedTask;
			var query = from lm in logs.Values
						group lm by lm.App.Id into a
						select new { AppName = a.First().App.Name, LogsCount = a.Count() };
			return query.ToDictionary(e => e.AppName, e => e.LogsCount);
		}

		public async Task<IDictionary<string, double>> GetLogSizeAvgPerAppAsync(CancellationToken ct = default) {
			await Task.CompletedTask;
			var query = from lm in logs.Values
						group lm.Size by lm.App.Name into a
						select new { AppName = a.Key, LogSizeAvg = a.Average() };
			return query.ToDictionary(e => e.AppName, e => e.LogSizeAvg ?? 0);
		}

		public async Task<LogMetadata> UpdateLogMetadataAsync(LogMetadata logMetadata, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			Debug.Assert(logs.ContainsKey(logMetadata.Id));
			logs[logMetadata.Id] = logMetadata;
			return logMetadata;
		}
	}
}
