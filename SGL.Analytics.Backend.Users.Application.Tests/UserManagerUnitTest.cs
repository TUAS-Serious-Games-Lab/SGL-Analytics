using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Application.Services;
using SGL.Analytics.Backend.Users.Application.Tests.Dummies;
using SGL.Analytics.DTO;
using SGL.Analytics.TestUtilities;
using SGL.Analytics.Utilities;
using System;
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
	}
}
