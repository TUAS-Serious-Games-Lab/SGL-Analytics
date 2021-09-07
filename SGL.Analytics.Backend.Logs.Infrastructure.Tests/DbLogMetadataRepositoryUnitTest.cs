using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Analytics.Backend.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Tests {
	public class DbLogMetadataRepositoryUnitTest : IDisposable {
		TestDatabase<LogsContext> testDb = new();

		private LogsContext createContext() {
			return new LogsContext(testDb.ContextOptions);
		}

		[Fact]
		public async Task AddedMetadataEntryCanBeRetrievedThroughDb() {
			var logId = Guid.NewGuid();
			var logMd = new LogMetadata(logId, Guid.Empty, Guid.NewGuid(), logId, DateTime.Now.AddMinutes(-15), DateTime.Now.AddMinutes(-1), DateTime.Now, ".log.gz");
			var app = new Domain.Entity.Application(Guid.NewGuid(), "DbLogMetadataRepositoryUnitTest", "FakeApiToken");
			await using (var context = createContext()) {
				context.Applications.Add(app);
				logMd.App = app;
				var repo = new DbLogMetadataRepository(context);
				logMd = await repo.AddLogMetadataAsync(logMd);
			}
			LogMetadata? logMdRead;
			await using (var context = createContext()) {
				var repo = new DbLogMetadataRepository(context);
				logMdRead = await repo.GetLogMetadataByIdAsync(logId);
			}
			Assert.NotNull(logMdRead);
			Assert.Equal(logId, logMdRead?.Id);
			Assert.Equal(logMd.Id, logMdRead?.Id);
			Assert.Equal(logMd.AppId, logMdRead?.AppId);
			Assert.Equal(logMd.UserId, logMdRead?.UserId);
			Assert.Equal(logMd.LocalLogId, logMdRead?.LocalLogId);
			Assert.Equal(logMd.CreationTime.ToUniversalTime(), logMdRead?.CreationTime);
			Assert.Equal(logMd.EndTime.ToUniversalTime(), logMdRead?.EndTime);
			Assert.Equal(logMd.UploadTime.ToUniversalTime(), logMdRead?.UploadTime);
		}

		[Fact]
		public async Task MetadataEntryIsCorrectlyUpdatedInDb() {
			var logId = Guid.NewGuid();
			var logMd = new LogMetadata(logId, Guid.Empty, Guid.NewGuid(), logId, DateTime.Now.AddMinutes(-15), DateTime.Now.AddMinutes(-1), DateTime.Now.AddSeconds(-30), ".log.gz");
			var app = new Domain.Entity.Application(Guid.NewGuid(), "DbLogMetadataRepositoryUnitTest", "FakeApiToken");
			await using (var context = createContext()) {
				context.Applications.Add(app);
				logMd.App = app;
				var repo = new DbLogMetadataRepository(context);
				logMd = await repo.AddLogMetadataAsync(logMd);
			}
			LogMetadata? logMd2;
			await using (var context = createContext()) {
				var repo = new DbLogMetadataRepository(context);
				logMd2 = await repo.GetLogMetadataByIdAsync(logId);
				Assert.NotNull(logMd2);
				if (logMd2 is null) throw new NotNullException();
				logMd2.LocalLogId = Guid.NewGuid();
				logMd2.UploadTime = DateTime.Now;
				await repo.UpdateLogMetadataAsync(logMd2);
			}
			LogMetadata? logMdRead;
			await using (var context = createContext()) {
				var repo = new DbLogMetadataRepository(context);
				logMdRead = await repo.GetLogMetadataByIdAsync(logId);
			}
			Assert.NotNull(logMdRead);
			Assert.Equal(logId, logMdRead?.Id);
			Assert.Equal(logMd2.Id, logMdRead?.Id);
			Assert.Equal(logMd2.AppId, logMdRead?.AppId);
			Assert.Equal(logMd2.UserId, logMdRead?.UserId);
			Assert.Equal(logMd2.LocalLogId, logMdRead?.LocalLogId);
			Assert.Equal(logMd2.CreationTime.ToUniversalTime(), logMdRead?.CreationTime);
			Assert.Equal(logMd2.EndTime.ToUniversalTime(), logMdRead?.EndTime);
			Assert.Equal(logMd2.UploadTime.ToUniversalTime(), logMdRead?.UploadTime);
		}

		[Fact]
		public async Task RequestForNonExistentLogMetadataReturnsNull() {
			var logId = Guid.NewGuid();
			LogMetadata? logMdRead;
			await using (var context = createContext()) {
				var repo = new DbLogMetadataRepository(context);
				logMdRead = await repo.GetLogMetadataByIdAsync(logId);
			}
			Assert.Null(logMdRead);
		}

		[Fact]
		public async Task AttemptingToCreateApplicationWithDuplicateIdThrowsCorrectException() {
			var id = Guid.NewGuid();
			var app = new Domain.Entity.Application(Guid.NewGuid(), "DbLogMetadataRepositoryUnitTest", "FakeApiToken");
			await using (var context = createContext()) {
				context.Applications.Add(app);
				await context.SaveChangesAsync();
				var repo = new DbLogMetadataRepository(context);
				var logMd = new LogMetadata(id, app.Id, Guid.NewGuid(), id, DateTime.Now, DateTime.Now, DateTime.Now, ".log.gz");
				logMd.App = app;
				await repo.AddLogMetadataAsync(logMd);
			}
			await using (var context = createContext()) {
				app = await context.Applications.SingleOrDefaultAsync(a => a.Id == app.Id);
				var repo = new DbLogMetadataRepository(context);
				var logMd = new LogMetadata(id, app.Id, Guid.NewGuid(), id, DateTime.Now, DateTime.Now, DateTime.Now, ".log.gz");
				logMd.App = app;
				await Assert.ThrowsAsync<EntityUniquenessConflictException>(async () => await repo.AddLogMetadataAsync(logMd));
			}
		}

		public void Dispose() {
			testDb.Dispose();
		}
	}
}
