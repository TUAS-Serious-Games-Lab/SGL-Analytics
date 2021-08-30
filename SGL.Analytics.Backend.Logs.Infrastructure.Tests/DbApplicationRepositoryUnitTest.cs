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
			var app1 = new Domain.Entity.Application(0, "DbApplicationRepositoryUnitTest_1", StringGenerator.GenerateRandomWord(32));
			var app2 = new Domain.Entity.Application(0, "DbApplicationRepositoryUnitTest_2", StringGenerator.GenerateRandomWord(32));
			var app3 = new Domain.Entity.Application(0, "DbApplicationRepositoryUnitTest_3", StringGenerator.GenerateRandomWord(32));
			await using (var repo = new DbApplicationRepository(createContext())) {
				await repo.AddApplicationAsync(app1);
				app2 = await repo.AddApplicationAsync(app2);
				await repo.AddApplicationAsync(app3);
			}
			Domain.Entity.Application? appRead;
			await using (var repo = new DbApplicationRepository(createContext())) {
				appRead = await repo.GetApplicationByNameAsync("DbApplicationRepositoryUnitTest_2");
			}
			Assert.NotNull(appRead);
			Assert.Equal(app2.Id, appRead?.Id);
			Assert.Equal(app2.Name, appRead?.Name);
			Assert.Equal(app2.ApiToken, appRead?.ApiToken);
		}
	}
}
