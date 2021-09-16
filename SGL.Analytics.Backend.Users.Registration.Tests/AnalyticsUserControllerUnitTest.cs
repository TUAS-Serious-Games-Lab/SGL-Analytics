﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Security;
using SGL.Analytics.Backend.TestUtilities;
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
using Xunit.Sdk;

namespace SGL.Analytics.Backend.Users.Registration.Tests {
	public class AnalyticsUserControllerUnitTest {
		private const string appName = "AnalyticsUserControllerUnitTest";
		private string appApiToken = StringGenerator.GenerateRandomWord(32);
		private ITestOutputHelper output;
		private DummyUserManager userManager;
		private ILoggerFactory loggerFactory;
		private JwtOptions jwtOptions;
		private JwtLoginService loginService;
		private JwtTokenValidator tokenValidator;
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
			tokenValidator = new JwtTokenValidator(jwtOptions.Issuer, jwtOptions.Audience, jwtOptions.SymmetricKey);
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
		[Fact]
		public async Task UserRegistrationUsernameAlreadyInUseFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			await userManager.RegisterUserAsync(userRegDTO);
			var result = await controller.RegisterUser(appApiToken, userRegDTO);
			var res = Assert.IsType<ConflictObjectResult>(result.Result);
			Assert.Equal(StatusCodes.Status409Conflict, res.StatusCode);
			Assert.IsType<string>(res.Value);
		}
		[Fact]
		public async Task UserRegistrationWithUnknownPropertyFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Baz"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			var result = await controller.RegisterUser(appApiToken, userRegDTO);
			var res = Assert.IsType<BadRequestObjectResult>(result.Result);
			Assert.Equal(StatusCodes.Status400BadRequest, res.StatusCode);
			Assert.IsType<string>(res.Value);
		}
		[Fact]
		public async Task UserRegistrationWithMissingRequiredPropertyFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			var result = await controller.RegisterUser(appApiToken, userRegDTO);
			var res = Assert.IsType<BadRequestObjectResult>(result.Result);
			Assert.Equal(StatusCodes.Status400BadRequest, res.StatusCode);
			Assert.IsType<string>(res.Value);
		}
		[Fact]
		public async Task UserRegistrationWithPropertyOfWrongTypeFailsWithExpectedError() {
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = 42 };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser",
				StringGenerator.GenerateRandomWord(16),// Not cryptographic, but ok for test
				props);
			var result = await controller.RegisterUser(appApiToken, userRegDTO);
			var res = Assert.IsType<BadRequestObjectResult>(result.Result);
			Assert.Equal(StatusCodes.Status400BadRequest, res.StatusCode);
			Assert.IsType<string>(res.Value);
		}
		[Fact]
		public async Task ValidUserCanSuccessfullyLoginWithCorrectCredentials() {
			var secret = StringGenerator.GenerateRandomWord(16);// Not cryptographic, but ok for test
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser", secret, props);
			var regResult = await controller.RegisterUser(appApiToken, userRegDTO);
			var userId = ((regResult.Result as ObjectResult)?.Value as UserRegistrationResultDTO)?.UserId ?? throw new XunitException("Registration failed.");
			var loginRequest = new LoginRequestDTO(appName, appApiToken, userId, secret);
			var loginResult = await controller.Login(loginRequest);
			Assert.NotNull(loginResult.Value);
			var (principal, validatedToken) = tokenValidator.Validate(loginResult.Value.BearerToken);
			Assert.Equal(jwtOptions.Issuer, validatedToken.Issuer);
			Assert.True(Guid.TryParse(Assert.Single(principal.Claims, c => c.Type.Equals("userid", StringComparison.OrdinalIgnoreCase)).Value, out var tokenUserId));
			Assert.Equal(userId, tokenUserId);
		}
		[Fact]
		public async Task LoginWithNonExistentUserFailsWithExpectedError() {
			var secret = StringGenerator.GenerateRandomWord(16);// Not cryptographic, but ok for test
			var loginRequest = new LoginRequestDTO(appName, appApiToken, Guid.NewGuid(), secret);
			var loginResult = await controller.Login(loginRequest);
			Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<UnauthorizedObjectResult>(loginResult.Result).StatusCode);
		}
		[Fact]
		public async Task LoginWithIncorrectSecretFailsWithExpectedError() {
			var secret = StringGenerator.GenerateRandomWord(16);// Not cryptographic, but ok for test
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser", secret, props);
			var regResult = await controller.RegisterUser(appApiToken, userRegDTO);
			var userId = ((regResult.Result as ObjectResult)?.Value as UserRegistrationResultDTO)?.UserId ?? throw new XunitException("Registration failed.");
			var loginRequest = new LoginRequestDTO(appName, appApiToken, userId, "Wrong");
			var loginResult = await controller.Login(loginRequest);
			Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<UnauthorizedObjectResult>(loginResult.Result).StatusCode);
		}
		[Fact]
		public async Task LoginWithNonExistentAppFailsWithExpectedError() {
			var secret = StringGenerator.GenerateRandomWord(16);// Not cryptographic, but ok for test
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser", secret, props);
			var regResult = await controller.RegisterUser(appApiToken, userRegDTO);
			var userId = ((regResult.Result as ObjectResult)?.Value as UserRegistrationResultDTO)?.UserId ?? throw new XunitException("Registration failed.");
			var loginRequest = new LoginRequestDTO("DoesNotExist", appApiToken, userId, secret);
			var loginResult = await controller.Login(loginRequest);
			Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<UnauthorizedObjectResult>(loginResult.Result).StatusCode);
		}
		[Fact]
		public async Task LoginWithIncorrectAppApiTokenFailsWithExpectedError() {
			var secret = StringGenerator.GenerateRandomWord(16);// Not cryptographic, but ok for test
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser", secret, props);
			var regResult = await controller.RegisterUser(appApiToken, userRegDTO);
			var userId = ((regResult.Result as ObjectResult)?.Value as UserRegistrationResultDTO)?.UserId ?? throw new XunitException("Registration failed.");
			var loginRequest = new LoginRequestDTO(appName, "Wrong", userId, secret);
			var loginResult = await controller.Login(loginRequest);
			Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<UnauthorizedObjectResult>(loginResult.Result).StatusCode);
		}
		[Fact]
		public async Task LoginWithUnmatchingAppAndUserFailsWithExpectedError() {
			var appName2 = "AnalyticsUserControllerUnitTest_2";
			var appApiToken2 = StringGenerator.GenerateRandomWord(32);
			await userManager.AddApplicationAsync(ApplicationWithUserProperties.Create(appName2, appApiToken2));
			var secret = StringGenerator.GenerateRandomWord(16);// Not cryptographic, but ok for test
			Dictionary<string, object?> props = new Dictionary<string, object?> { ["Foo"] = "Test", ["Bar"] = "Hello" };
			var userRegDTO = new UserRegistrationDTO(appName, "Testuser", secret, props);
			var regResult = await controller.RegisterUser(appApiToken, userRegDTO);
			var userId = ((regResult.Result as ObjectResult)?.Value as UserRegistrationResultDTO)?.UserId ?? throw new XunitException("Registration failed.");
			var loginRequest = new LoginRequestDTO(appName2, appApiToken2, userId, secret);
			var loginResult = await controller.Login(loginRequest);
			Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<UnauthorizedObjectResult>(loginResult.Result).StatusCode);
		}
	}
}
