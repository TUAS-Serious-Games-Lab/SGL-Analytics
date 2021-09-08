using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Services;
using SGL.Analytics.Backend.Users.Application.Tests.Dummies;
using SGL.Analytics.DTO;
using SGL.Analytics.TestUtilities;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Users.Application.Tests {
	public class UserManagerUnitTest {
		private DummyApplicationRepository appRepo = new DummyApplicationRepository();
		private DummyUserRepository userRepo = new DummyUserRepository();
		private ITestOutputHelper output;
		private ILoggerFactory loggerFactory;
		private UserManager userMgr;

		private const string appName = "UserManagerUnitTest";
		private readonly string appApiKey = StringGenerator.GenerateRandomWord(32);

		public UserManagerUnitTest(ITestOutputHelper output) {
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			userMgr = new UserManager(appRepo, userRepo, loggerFactory.CreateLogger<UserManager>());
		}

		[Fact]
		public async Task SimpleUserCanBeRegisteredAndThenRetrieved() {
			var app = await appRepo.AddApplicationAsync(ApplicationWithUserProperties.Create(appName, appApiKey));
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser", new());
			var user = await userMgr.RegisterUserAsync(userRegDTO);
			Assert.Equal("Testuser", user.Username);
			Assert.NotEqual(Guid.Empty, user.Id);
			Assert.Equal(appName, user.App.Name);
			Assert.Empty(user.AppSpecificProperties);
		}

		[Fact]
		public async Task UserWithAppSpecificPropertiesCanBeRegisteredAndThenRetrievedWithCorrectProperties() {
			var app = ApplicationWithUserProperties.Create(appName, appApiKey);
			app.AddProperty("Number", UserPropertyType.Integer);
			app.AddProperty("String", UserPropertyType.String);
			app.AddProperty("Date", UserPropertyType.DateTime);
			app.AddProperty("Mapping", UserPropertyType.Json);

			var date = DateTime.Today;
			app = await appRepo.AddApplicationAsync(app);
			var mapping = new Dictionary<string, object?>() {
				["A"] = 42,
				["B"] = "Test"
			};
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser", new() {
				["Number"] = 1234,
				["String"] = "Hello World",
				["Date"] = date,
				["Mapping"] = mapping
			});
			var user = await userMgr.RegisterUserAsync(userRegDTO);
			Assert.Equal("Testuser", user.Username);
			Assert.NotEqual(Guid.Empty, user.Id);
			Assert.Equal(appName, user.App.Name);
			IDictionary<string, object?> props = user.AppSpecificProperties;
			Assert.Equal(1234, Assert.Contains("Number", props));
			Assert.Equal("Hello World", Assert.Contains("String", props));
			Assert.Equal(date.ToUniversalTime(), (Assert.Contains("Date", props) as DateTime?)?.ToUniversalTime());
			var mappingRead = Assert.IsAssignableFrom<IDictionary<string, object?>>(Assert.Contains("Mapping", props));
			Assert.All(mapping, kvp => Assert.Equal(kvp.Value, Assert.Contains(kvp.Key, mappingRead)));
		}

		[Fact]
		public async Task AttemptToRegisterUserWithNonExistentPropertyThrowsCorrectException() {
			var app = ApplicationWithUserProperties.Create(appName, appApiKey);
			app.AddProperty("Number", UserPropertyType.Integer);
			app = await appRepo.AddApplicationAsync(app);

			var userRegDTO = new UserRegistrationDTO(appName, "Testuser", new() {
				["Number"] = 42,
				["DoesNotExist"] = "Hello World"
			});
			await Assert.ThrowsAsync<UndefinedPropertyException>(async () => await userMgr.RegisterUserAsync(userRegDTO));
		}
	}
}
