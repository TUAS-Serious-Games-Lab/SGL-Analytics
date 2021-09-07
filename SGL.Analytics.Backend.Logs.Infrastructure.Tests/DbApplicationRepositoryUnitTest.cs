using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Logs.Infrastructure.Services;
using SGL.Analytics.Backend.TestUtilities;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Tests {
	public class DbApplicationRepositoryUnitTest : IDisposable {
		TestDatabase<LogsContext> testDb = new();

		public void Dispose() => testDb.Dispose();

		private LogsContext createContext() {
			return new LogsContext(testDb.ContextOptions);
		}

		[Fact]
		public async Task ApplicationsCanBeCreatedAndThenRetrivedByName() {
			var app1 = new Domain.Entity.Application(Guid.NewGuid(), "DbApplicationRepositoryUnitTest_1", StringGenerator.GenerateRandomWord(32));
			var app2 = new Domain.Entity.Application(Guid.NewGuid(), "DbApplicationRepositoryUnitTest_2", StringGenerator.GenerateRandomWord(32));
			var app3 = new Domain.Entity.Application(Guid.NewGuid(), "DbApplicationRepositoryUnitTest_3", StringGenerator.GenerateRandomWord(32));
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				await repo.AddApplicationAsync(app1);
				app2 = await repo.AddApplicationAsync(app2);
				await repo.AddApplicationAsync(app3);
			}
			Domain.Entity.Application? appRead;
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				appRead = await repo.GetApplicationByNameAsync("DbApplicationRepositoryUnitTest_2");
			}
			Assert.NotNull(appRead);
			Assert.Equal(app2.Id, appRead?.Id);
			Assert.Equal(app2.Name, appRead?.Name);
			Assert.Equal(app2.ApiToken, appRead?.ApiToken);
		}

		[Fact]
		public async Task RequestForNonExistentApplicationReturnsNull() {
			Domain.Entity.Application? appRead;
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				appRead = await repo.GetApplicationByNameAsync("DoesNotExist");
			}
			Assert.Null(appRead);
		}

		[Fact]
		public async Task AttemptingToCreateApplicationWithDuplicateNameThrowsCorrectException() {
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				await repo.AddApplicationAsync(new Domain.Entity.Application(Guid.NewGuid(), "DbApplicationRepositoryUnitTest", StringGenerator.GenerateRandomWord(32)));
			}
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				Assert.Equal("Name", (await Assert.ThrowsAsync<EntityUniquenessConflictException>(async () => await repo.AddApplicationAsync(new Domain.Entity.Application(Guid.NewGuid(), "DbApplicationRepositoryUnitTest", StringGenerator.GenerateRandomWord(32))))).ConflictingPropertyName);
			}
		}

		[Fact]
		public async Task AttemptingToCreateApplicationWithDuplicateIdThrowsCorrectException() {
			var id = Guid.NewGuid();
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				await repo.AddApplicationAsync(new Domain.Entity.Application(id, "DbApplicationRepositoryUnitTest_1", StringGenerator.GenerateRandomWord(32)));
			}
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				Assert.Equal("Id", (await Assert.ThrowsAsync<EntityUniquenessConflictException>(async () => await repo.AddApplicationAsync(new Domain.Entity.Application(id, "DbApplicationRepositoryUnitTest_2", StringGenerator.GenerateRandomWord(32))))).ConflictingPropertyName);
			}
		}
	}
}
