using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Security;
using SGL.Analytics.Backend.Users.Registration.Controllers;
using SGL.Analytics.Backend.Users.Registration.Tests.Dummies;
using SGL.Analytics.DTO;
using SGL.Analytics.TestUtilities;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Users.Registration.Tests {
	public class AnalyticsUserControllerUnitTest {
		private const string appName = "AnalyticsUserControllerUnitTest";
		private string appApiToken = StringGenerator.GenerateRandomWord(32);
		private ITestOutputHelper output;
		private DummyUserManager userManager;
		private ILoggerFactory loggerFactory;
		private JwtOptions jwtOptions;
		private JwtLoginService loginService;
		private AnalyticsUserController controller;

		public AnalyticsUserControllerUnitTest(ITestOutputHelper output) {
			this.output = output;
			var app = ApplicationWithUserProperties.Create(appName, appApiToken);
			app.AddProperty("Foo", UserPropertyType.String, true);
			app.AddProperty("Bar", UserPropertyType.String);
			userManager = new DummyUserManager(new List<ApplicationWithUserProperties>() { app });
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			jwtOptions = new JwtOptions() {
				Audience = "AnalyticsUserControllerUnitTest",
				Issuer = "AnalyticsUserControllerUnitTest",
				SymmetricKey = "TestingSecretKeyTestingSecretKeyTestingSecretKey",
				LoginService = new JwtLoginServiceOptions() {
					ExpirationTime = TimeSpan.FromMinutes(5),
					FailureDelay = TimeSpan.FromMilliseconds(450)
				}
			};
			loginService = new JwtLoginService(loggerFactory.CreateLogger<JwtLoginService>(), Options.Create(jwtOptions));
			controller = new AnalyticsUserController(userManager, loginService, userManager, loggerFactory.CreateLogger<AnalyticsUserController>());
		}

		[Fact]
		public async Task ValidUserRegistrationIsSuccessfullyCompleted() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			var result = await controller.RegisterUser(appApiToken, userRegDTO);
			var res = Assert.IsType<ObjectResult>(result.Result);
			Assert.Equal(StatusCodes.Status201Created, res.StatusCode);
			Guid userId = Assert.IsType<UserRegistrationResultDTO>(res.Value).UserId;
			Assert.NotEqual(Guid.Empty, userId);
			var user = await userManager.GetUserByIdAsync(userId);
			Assert.NotNull(user);
			Assert.Equal("Testuser", user!.Username);
			Assert.Equal(appName, user!.App.Name);
			Assert.All(props, kvp => Assert.Equal(kvp.Value, (Assert.Contains(kvp.Key, user.AppSpecificProperties as IDictionary<string, object?>))));
		}
		[Fact]
		public async Task UserRegistrationWithNonExistentAppFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO("DoesNotExist", "Testuser",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			var result = await controller.RegisterUser(appApiToken, userRegDTO);
			var res = Assert.IsType<UnauthorizedObjectResult>(result.Result);
			Assert.Equal(StatusCodes.Status401Unauthorized, res.StatusCode);
			Assert.IsType<string>(res.Value);
		}
		[Fact]
		public async Task UserRegistrationWithIncorrectAppApiTokenFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			var result = await controller.RegisterUser("Wrong", userRegDTO);
			var res = Assert.IsType<UnauthorizedObjectResult>(result.Result);
			Assert.Equal(StatusCodes.Status401Unauthorized, res.StatusCode);
			Assert.IsType<string>(res.Value);
		}
	}
}
