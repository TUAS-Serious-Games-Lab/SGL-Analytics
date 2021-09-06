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

		[Fact]
		public async Task ApplicationWithPropertiesCanBeCreatedAndRetrievedWithPropertiesPreseved() {
			var appOrig = new ApplicationWithUserProperties(Guid.NewGuid(), "DbApplicationRepositoryUnitTest", StringGenerator.GenerateRandomWord(32));
			appOrig.UserProperties = new List<ApplicationUserPropertyDefinition>();
			appOrig.UserProperties.Add(new ApplicationUserPropertyDefinition(0, appOrig.Id, "TestInt", UserPropertyType.Integer, true));
			appOrig.UserProperties.Add(new ApplicationUserPropertyDefinition(0, appOrig.Id, "TestFP", UserPropertyType.FloatingPoint, false));
			appOrig.UserProperties.Add(new ApplicationUserPropertyDefinition(0, appOrig.Id, "TestString", UserPropertyType.String, true));
			appOrig.UserProperties.Add(new ApplicationUserPropertyDefinition(0, appOrig.Id, "TestDateTime", UserPropertyType.DateTime, false));
			appOrig.UserProperties.Add(new ApplicationUserPropertyDefinition(0, appOrig.Id, "TestGuid", UserPropertyType.Guid, true));
			appOrig.UserProperties.Add(new ApplicationUserPropertyDefinition(0, appOrig.Id, "TestJson", UserPropertyType.Json, false));
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				await repo.AddApplicationAsync(appOrig);
			}
			ApplicationWithUserProperties? appRead;
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				appRead = await repo.GetApplicationByNameAsync("DbApplicationRepositoryUnitTest");
			}
			Assert.NotNull(appRead);
			Assert.Equal(appOrig.Id, appRead?.Id);
			Assert.Equal(appOrig.Name, appRead?.Name);
			Assert.Equal(appOrig.ApiToken, appRead?.ApiToken);

			var intProp = Assert.Single(appRead?.UserProperties, pd => pd.Name == "TestInt");
			Assert.Equal(appRead?.Id, intProp.AppId);
			Assert.Equal(UserPropertyType.Integer, intProp.Type);
			Assert.True(intProp.Required);

			var fpProp = Assert.Single(appRead?.UserProperties, pd => pd.Name == "TestFP");
			Assert.Equal(appRead?.Id, fpProp.AppId);
			Assert.Equal(UserPropertyType.FloatingPoint, fpProp.Type);
			Assert.False(fpProp.Required);

			var strProp = Assert.Single(appRead?.UserProperties, pd => pd.Name == "TestString");
			Assert.Equal(appRead?.Id, strProp.AppId);
			Assert.Equal(UserPropertyType.String, strProp.Type);
			Assert.True(strProp.Required);

			var dtProp = Assert.Single(appRead?.UserProperties, pd => pd.Name == "TestDateTime");
			Assert.Equal(appRead?.Id, dtProp.AppId);
			Assert.Equal(UserPropertyType.DateTime, dtProp.Type);
			Assert.False(dtProp.Required);

			var guidProp = Assert.Single(appRead?.UserProperties, pd => pd.Name == "TestGuid");
			Assert.Equal(appRead?.Id, guidProp.AppId);
			Assert.Equal(UserPropertyType.Guid, guidProp.Type);
			Assert.True(guidProp.Required);

			var jsonProp = Assert.Single(appRead?.UserProperties, pd => pd.Name == "TestJson");
			Assert.Equal(appRead?.Id, jsonProp.AppId);
			Assert.Equal(UserPropertyType.Json, jsonProp.Type);
			Assert.False(jsonProp.Required);
		}

		[Fact]
		public async Task RequestForNonExistentApplicationReturnsNull() {
			ApplicationWithUserProperties? appRead;
			await using (var context = createContext()) {
				var repo = new DbApplicationRepository(context);
				appRead = await repo.GetApplicationByNameAsync("DoesNotExist");
			}
			Assert.Null(appRead);
		}
	}
}
