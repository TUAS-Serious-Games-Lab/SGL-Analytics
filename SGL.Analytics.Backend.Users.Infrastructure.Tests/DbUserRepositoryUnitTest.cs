using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.TestUtilities;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.Backend.Users.Infrastructure.Services;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
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

		[Fact]
		public async Task UserWithPropertiesCanBeRegisteredAndThenRetrievedById() {
			var appOrig = ApplicationWithUserProperties.Create("DbUserRepositoryUnitTest", StringGenerator.GenerateRandomWord(32));
			var propDef1 = ApplicationUserPropertyDefinition.Create(appOrig, "TestInt", UserPropertyType.Integer, true);
			var propDef2 = ApplicationUserPropertyDefinition.Create(appOrig, "TestFP", UserPropertyType.FloatingPoint, false);
			var propDef3 = ApplicationUserPropertyDefinition.Create(appOrig, "TestString", UserPropertyType.String, true);
			var propDef4 = ApplicationUserPropertyDefinition.Create(appOrig, "TestDateTime", UserPropertyType.DateTime, false);
			var propDef5 = ApplicationUserPropertyDefinition.Create(appOrig, "TestGuid", UserPropertyType.Guid, true);
			var propDef6 = ApplicationUserPropertyDefinition.Create(appOrig, "TestJson", UserPropertyType.Json, false);

			appOrig.UserProperties.Add(propDef1);
			appOrig.UserProperties.Add(propDef2);
			appOrig.UserProperties.Add(propDef3);
			appOrig.UserProperties.Add(propDef4);
			appOrig.UserProperties.Add(propDef5);
			appOrig.UserProperties.Add(propDef6);

			Guid userId;
			DateTime dateTime = DateTime.UtcNow;
			Guid guid = Guid.NewGuid();
			var arr = new object[] { 1234, "Test", Guid.NewGuid() };
			await using (var context = createContext()) {
				var repo = new DbUserRepository(context);
				context.Applications.Add(appOrig);
				await context.SaveChangesAsync();
				var user = UserRegistration.Create(appOrig, "TestUser");

				user.AppSpecificProperties.Add(ApplicationUserPropertyInstance.Create(propDef1, user, 42));
				user.AppSpecificProperties.Add(ApplicationUserPropertyInstance.Create(propDef2, user, 123.45));
				user.AppSpecificProperties.Add(ApplicationUserPropertyInstance.Create(propDef3, user, "Hello World"));
				user.AppSpecificProperties.Add(ApplicationUserPropertyInstance.Create(propDef4, user, dateTime));
				user.AppSpecificProperties.Add(ApplicationUserPropertyInstance.Create(propDef5, user, guid));
				user.AppSpecificProperties.Add(ApplicationUserPropertyInstance.Create(propDef6, user, arr));

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

			Assert.Equal(42, userRead?.AppSpecificProperties?.Where(p => p.Definition.Name == "TestInt")?.SingleOrDefault()?.Value);
			Assert.Equal(123.45, userRead?.AppSpecificProperties?.Where(p => p.Definition.Name == "TestFP")?.SingleOrDefault()?.Value);
			Assert.Equal("Hello World", userRead?.AppSpecificProperties?.Where(p => p.Definition.Name == "TestString")?.SingleOrDefault()?.Value);
			Assert.Equal(dateTime, userRead?.AppSpecificProperties?.Where(p => p.Definition.Name == "TestDateTime")?.SingleOrDefault()?.Value);
			Assert.Equal(guid, userRead?.AppSpecificProperties?.Where(p => p.Definition.Name == "TestGuid")?.SingleOrDefault()?.Value);
			Assert.Equal(arr, userRead?.AppSpecificProperties?.Where(p => p.Definition.Name == "TestJson")?.SingleOrDefault()?.Value as IEnumerable<object?>);
		}

		[Fact]
		public async Task RequestForNonExistentUserReturnsNull() {
			var appOrig = ApplicationWithUserProperties.Create("DbUserRepositoryUnitTest", StringGenerator.GenerateRandomWord(32));
			await using (var context = createContext()) {
				var repo = new DbUserRepository(context);
				context.Applications.Add(appOrig);
				await context.SaveChangesAsync();
				var user = UserRegistration.Create(appOrig, "TestUser");
				await repo.RegisterUserAsync(user);
			}
			await using (var context = createContext()) {
				var repo = new DbUserRepository(context);
				var userRead = await repo.GetUserByIdAsync(Guid.NewGuid());
				Assert.Null(userRead);
			}
		}
	}
}
