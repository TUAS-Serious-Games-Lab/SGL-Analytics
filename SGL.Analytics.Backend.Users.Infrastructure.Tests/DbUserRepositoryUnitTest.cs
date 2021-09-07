using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
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

			var propDef1 = appOrig.AddProperty("TestInt", UserPropertyType.Integer, true);
			var propDef2 = appOrig.AddProperty("TestFP", UserPropertyType.FloatingPoint, false);
			var propDef3 = appOrig.AddProperty("TestString", UserPropertyType.String, true);
			var propDef4 = appOrig.AddProperty("TestDateTime", UserPropertyType.DateTime, false);
			var propDef5 = appOrig.AddProperty("TestGuid", UserPropertyType.Guid, true);
			var propDef6 = appOrig.AddProperty("TestJson", UserPropertyType.Json, false);

			Guid userId;
			DateTime dateTime = DateTime.UtcNow;
			Guid guid = Guid.NewGuid();
			var arr = new object[] { 1234, "Test", Guid.NewGuid() };
			await using (var context = createContext()) {
				var repo = new DbUserRepository(context);
				context.Applications.Add(appOrig);
				await context.SaveChangesAsync();
				var user = UserRegistration.Create(appOrig, "TestUser");

				user.SetAppSpecificProperty(propDef1, 42);
				user.SetAppSpecificProperty(propDef2, 123.45);
				user.SetAppSpecificProperty(propDef3, "Hello World");
				user.SetAppSpecificProperty(propDef4, dateTime);
				user.SetAppSpecificProperty(propDef5, guid);
				user.SetAppSpecificProperty(propDef6, arr);

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

			Assert.Equal(42, userRead?.GetAppSpecificProperty("TestInt"));
			Assert.Equal(123.45, userRead?.GetAppSpecificProperty("TestFP"));
			Assert.Equal("Hello World", userRead?.GetAppSpecificProperty("TestString"));
			Assert.Equal(dateTime, userRead?.GetAppSpecificProperty("TestDateTime"));
			Assert.Equal(guid, userRead?.GetAppSpecificProperty("TestGuid"));
			Assert.Equal(arr, userRead?.GetAppSpecificProperty("TestJson") as IEnumerable<object?>);
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

		[Fact]
		public async Task UserRegistrationPropertiesCanBeUpdated() {
			var appOrig = ApplicationWithUserProperties.Create("DbUserRepositoryUnitTest", StringGenerator.GenerateRandomWord(32));

			var propDef1 = appOrig.AddProperty("TestInt", UserPropertyType.Integer, true);
			var propDef2 = appOrig.AddProperty("TestFP", UserPropertyType.FloatingPoint, false);
			var propDef3 = appOrig.AddProperty("TestString", UserPropertyType.String, true);
			var propDef4 = appOrig.AddProperty("TestDateTime", UserPropertyType.DateTime, false);
			var propDef5 = appOrig.AddProperty("TestGuid", UserPropertyType.Guid, true);
			var propDef6 = appOrig.AddProperty("TestJson", UserPropertyType.Json, false);

			Guid userId;
			DateTime dateTime = DateTime.UtcNow;
			Guid guid1 = Guid.NewGuid();
			var arr = new object[] { 1234, "Test", Guid.NewGuid() };
			await using (var context = createContext()) {
				var repo = new DbUserRepository(context);
				context.Applications.Add(appOrig);
				await context.SaveChangesAsync();
				var user = UserRegistration.Create(appOrig, "TestUser");

				user.SetAppSpecificProperty(propDef1, 42);
				user.SetAppSpecificProperty(propDef3, "Hello World");
				user.SetAppSpecificProperty(propDef5, guid1);

				userId = user.Id;
				await repo.RegisterUserAsync(user);
			}

			Guid guid2 = Guid.NewGuid();
			await using (var context = createContext()) {
				var repo = new DbUserRepository(context);
				var user = await repo.GetUserByIdAsync(userId);
				Assert.NotNull(user);
				user?.SetAppSpecificProperty(propDef1.Name, 1234);
				user?.SetAppSpecificProperty(propDef2.Name, 987.654);
				user?.SetAppSpecificProperty(propDef3.Name, "Test, Test, Test");
				user?.SetAppSpecificProperty(propDef4.Name, dateTime);
				user?.SetAppSpecificProperty(propDef5.Name, guid2);
				user?.SetAppSpecificProperty(propDef6.Name, arr);

				await repo.UpdateUserAsync(user);
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

			Assert.Equal(1234, userRead?.GetAppSpecificProperty("TestInt"));
			Assert.Equal(987.654, userRead?.GetAppSpecificProperty("TestFP"));
			Assert.Equal("Test, Test, Test", userRead?.GetAppSpecificProperty("TestString"));
			Assert.Equal(dateTime, userRead?.GetAppSpecificProperty("TestDateTime"));
			Assert.Equal(guid2, userRead?.GetAppSpecificProperty("TestGuid"));
			Assert.Equal(arr, userRead?.GetAppSpecificProperty("TestJson") as IEnumerable<object?>);
		}

		[Fact]
		public async Task AttemptingToCreateUserWithDuplicateNameThrowsCorrectException() {
			var app = ApplicationWithUserProperties.Create("DbUserRepositoryUnitTest", StringGenerator.GenerateRandomWord(32));
			await using (var context = createContext()) {
				context.Applications.Add(app);
				await context.SaveChangesAsync();
			}
			await using (var context = createContext()) {
				app = await context.Applications.Include(a => a.UserProperties).SingleOrDefaultAsync(a => a.Id == app.Id);
				var repo = new DbUserRepository(context);
				await repo.RegisterUserAsync(UserRegistration.Create(app, "TestUser"));
			}
			await using (var context = createContext()) {
				app = await context.Applications.Include(a => a.UserProperties).SingleOrDefaultAsync(a => a.Id == app.Id);
				var repo = new DbUserRepository(context);
				Assert.Equal("Username", (await Assert.ThrowsAsync<EntityUniquenessConflictException>(async () => await repo.RegisterUserAsync(UserRegistration.Create(app, "TestUser")))).ConflictingPropertyName);
			}
		}

		[Fact]
		public async Task AttemptingToCreateUserWithDuplicateIdThrowsCorrectException() {
			var app = ApplicationWithUserProperties.Create("DbUserRepositoryUnitTest", StringGenerator.GenerateRandomWord(32));
			await using (var context = createContext()) {
				context.Applications.Add(app);
				await context.SaveChangesAsync();
			}
			var id = Guid.NewGuid();
			await using (var context = createContext()) {
				app = await context.Applications.Include(a => a.UserProperties).SingleOrDefaultAsync(a => a.Id == app.Id);
				var repo = new DbUserRepository(context);
				await repo.RegisterUserAsync(UserRegistration.Create(id, app, "TestUser_1"));
			}
			await using (var context = createContext()) {
				app = await context.Applications.Include(a => a.UserProperties).SingleOrDefaultAsync(a => a.Id == app.Id);
				var repo = new DbUserRepository(context);
				Assert.Equal("Id", (await Assert.ThrowsAsync<EntityUniquenessConflictException>(async () => await repo.RegisterUserAsync(UserRegistration.Create(id, app, "TestUser_2")))).ConflictingPropertyName);
			}
		}

		[Fact]
		public async Task AttemptingToUpdateUsernameToExistingOneThrowsCorrectException() {
			var app = ApplicationWithUserProperties.Create("DbUserRepositoryUnitTest", StringGenerator.GenerateRandomWord(32));
			await using (var context = createContext()) {
				context.Applications.Add(app);
				await context.SaveChangesAsync();
			}
			Guid userId;
			await using (var context = createContext()) {
				app = await context.Applications.Include(a => a.UserProperties).SingleOrDefaultAsync(a => a.Id == app.Id);
				var repo = new DbUserRepository(context);
				await repo.RegisterUserAsync(UserRegistration.Create(app, "TestUser_1"));
				var userReg = UserRegistration.Create(app, "TestUser_2");
				userReg = await repo.RegisterUserAsync(userReg);
				userId = userReg.Id;
			}
			await using (var context = createContext()) {
				app = await context.Applications.Include(a => a.UserProperties).SingleOrDefaultAsync(a => a.Id == app.Id);
				var repo = new DbUserRepository(context);
				var userReg = await repo.GetUserByIdAsync(userId);
				Assert.NotNull(userReg);
				userReg!.Username = "TestUser_1";
				Assert.Equal("Username", (await Assert.ThrowsAsync<EntityUniquenessConflictException>(async () => await repo.UpdateUserAsync(userReg))).ConflictingPropertyName);
			}
		}

	}
}
