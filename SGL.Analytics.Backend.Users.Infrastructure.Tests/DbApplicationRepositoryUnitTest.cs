using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.TestUtilities;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.Backend.Users.Infrastructure.Services;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Analytics.Backend.Users.Infrastructure.Tests {
	public class DbApplicationRepositoryUnitTest : IDisposable {
		TestDatabase<UsersContext> testDb = new();

		public void Dispose() => testDb.Dispose();

		private UsersContext createContext() {
			return new UsersContext(testDb.ContextOptions);
		}

		[Fact]
		public async Task ApplicationsCanBeCreatedAndThenRetrievedByName() {
			var app1 = new ApplicationWithUserProperties(Guid.NewGuid(), "DbApplicationRepositoryUnitTest_1", StringGenerator.GenerateRandomWord(32));
			var app2 = new ApplicationWithUserProperties(Guid.NewGuid(), "DbApplicationRepositoryUnitTest_2", StringGenerator.GenerateRandomWord(32));
			var app3 = new ApplicationWithUserProperties(Guid.NewGuid(), "DbApplicationRepositoryUnitTest_3", StringGenerator.GenerateRandomWord(32));
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				await repo.AddApplicationAsync(app1);
				app2 = await repo.AddApplicationAsync(app2);
				await repo.AddApplicationAsync(app3);
			}
			ApplicationWithUserProperties? appRead;
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				appRead = await repo.GetApplicationByNameAsync("DbApplicationRepositoryUnitTest_2");
			}
			Assert.NotNull(appRead);
			Assert.Equal(app2.Id, appRead?.Id);
			Assert.Equal(app2.Name, appRead?.Name);
			Assert.Equal(app2.ApiToken, appRead?.ApiToken);
		}
	}
}
