using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Analytics.Backend.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Tests {
	public class DbLogMetadataRepositoryUnitTest : IDisposable {
		TestDatabase<LogsContext> testDb = new();

		private LogsContext createContext() {
			return new LogsContext(testDb.ContextOptions);
		}

		[Fact]
		public async Task AddedMetadataEntryCanBeRetrievedThroughDb() {
			var logId = Guid.NewGuid();
			var logMd = new LogMetadata(logId, 0, Guid.NewGuid(), logId, DateTime.Now.AddMinutes(-15), DateTime.Now.AddMinutes(-1), DateTime.Now);
			var app = new Domain.Entity.Application(0, "DbLogMetadataRepositoryUnitTest", "FakeApiToken");
			await using (var context = createContext()) {
				context.Applications.Add(app);
				logMd.App = app;
				await using (var repo = new DbLogMetadataRepository(context)) {
					logMd = await repo.AddLogMetadataAsync(logMd);
				}
			}
			LogMetadata? logMdRead;
			await using (var repo = new DbLogMetadataRepository(createContext())) {
				logMdRead = await repo.GetLogMetadataByIdAsync(logId);
			}
			Assert.NotNull(logMdRead);
			Assert.Equal(logId, logMdRead?.Id);
			Assert.Equal(logMd.Id, logMdRead?.Id);
			Assert.Equal(logMd.AppId, logMdRead?.AppId);
			Assert.Equal(logMd.UserId, logMdRead?.UserId);
			Assert.Equal(logMd.LocalLogId, logMdRead?.LocalLogId);
			Assert.Equal(logMd.CreationTime, logMdRead?.CreationTime);
			Assert.Equal(logMd.EndTime, logMdRead?.EndTime);
			Assert.Equal(logMd.UploadTime, logMdRead?.UploadTime);
		}

		public void Dispose() {
			testDb.Dispose();
		}
	}
}
