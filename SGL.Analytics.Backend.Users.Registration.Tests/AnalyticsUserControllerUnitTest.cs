using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Security;
using SGL.Analytics.Backend.Users.Registration.Controllers;
using SGL.Analytics.Backend.Users.Registration.Tests.Dummies;
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
		private string appApiToken = StringGenerator.GenerateRandomWord(32);
		private ITestOutputHelper output;
		private DummyUserManager userManager;
		private ILoggerFactory loggerFactory;
		private JwtOptions jwtOptions;
		private JwtLoginService loginService;
		private AnalyticsUserController controller;

		public AnalyticsUserControllerUnitTest(ITestOutputHelper output) {
			this.output = output;
			userManager = new DummyUserManager(new List<ApplicationWithUserProperties>() { ApplicationWithUserProperties.Create("AnalyticsUserControllerUnitTest", appApiToken) });
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
	}
}
