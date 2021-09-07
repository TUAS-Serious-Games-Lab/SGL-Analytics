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
	public class DbUserRepositoryUnitTest : IDisposable {
		TestDatabase<UsersContext> testDb = new();

		public void Dispose() => testDb.Dispose();

		private UsersContext createContext() {
			return new UsersContext(testDb.ContextOptions);
		}

		[Fact]
		public async Task SimpleUserCanBeRegisteredAndThenRetrievedById() {
			var appOrig = ApplicationWithUserProperties.Create("DbUserRepositoryUnitTest", StringGenerator.GenerateRandomWord(32));
			Guid userId;
			await using (var context = createContext()) {
				var repo = new DbUserRepository(context);
				context.Applications.Add(appOrig);
				await context.SaveChangesAsync();
				var user = UserRegistration.Create(appOrig, "TestUser");
				userId = user.Id;
				await repo.RegisterUserAsync(user);
			}
			UserRegistration? userRead;
			await using (var context = createContext()) {
				var repo = new DbUserRepository(context);
				userRead = await repo.GetUserByIdAsync(userId);
			}
			Assert.NotNull(userRead);
			Assert.Equal(userId, userRead?.Id);
			Assert.Equal(appOrig.Id, userRead?.AppId);
			Assert.Equal("TestUser", userRead?.Username);
			Assert.Equal(appOrig.Name, userRead?.App.Name);
			Assert.Equal(appOrig.ApiToken, userRead?.App.ApiToken);
		}
	}
}
