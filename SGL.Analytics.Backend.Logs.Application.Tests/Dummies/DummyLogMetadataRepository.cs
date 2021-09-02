using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Tests.Dummies {
	public class DummyLogMetadataRepository : ILogMetadataRepository {
		private Dictionary<Guid, LogMetadata> logs = new();

		public async Task<LogMetadata> AddLogMetadataAsync(LogMetadata logMetadata) {
			await Task.CompletedTask;
			if (logs.ContainsKey(logMetadata.Id)) throw new InvalidOperationException($"An log metadata entry with the given id '{logMetadata.Id}' is already present.");
			logs.Add(logMetadata.Id, logMetadata);
			return logMetadata;
		}

		public async Task<LogMetadata?> GetLogMetadataByIdAsync(Guid logId) {
			await Task.CompletedTask;
			if (logs.TryGetValue(logId, out var logMd)) {
				return logMd;
			}
			else {
				return null;
			}
		}

		public async Task<LogMetadata?> GetLogMetadataByUserLocalIdAsync(int userAppId, Guid userId, Guid localLogId) {
			await Task.CompletedTask;
			return logs.Values.Where(lm => lm.AppId == userAppId && lm.UserId == userId && lm.LocalLogId == localLogId).SingleOrDefault<LogMetadata?>();
		}

		public async Task<LogMetadata> UpdateLogMetadataAsync(LogMetadata logMetadata) {
			await Task.CompletedTask;
			Debug.Assert(logs.ContainsKey(logMetadata.Id));
			logs[logMetadata.Id] = logMetadata;
			return logMetadata;
		}
	}
}
