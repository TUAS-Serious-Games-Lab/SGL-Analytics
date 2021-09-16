using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGL.Analytics.Backend.TestUtilities;
using SGL.Analytics.TestUtilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace SGL.Analytics.Backend.Security.Tests {
	public class JwtLoginServiceUnitTest {
		private ITestOutputHelper output;
		private ILoggerFactory loggerFactory;
		private JwtOptions options;
		private ILoginService loginService;
		private JwtTokenValidator tokenValidator;
		private TimeSpan delayTolerance = TimeSpan.FromMilliseconds(16);

		private class User { }

		public JwtLoginServiceUnitTest(ITestOutputHelper output) {
			this.output = output;
			loggerFactory = LoggerFactory.Create(c => c.AddXUnit(output).SetMinimumLevel(LogLevel.Trace));
			options = new JwtOptions() {
				Audience = "JwtLoginServiceUnitTest",
				Issuer = "JwtLoginServiceUnitTest",
				SymmetricKey = "TestingSecretKeyTestingSecretKeyTestingSecretKey",
				LoginService = new JwtLoginServiceOptions() {
					ExpirationTime = TimeSpan.FromMinutes(5),
					FailureDelay = TimeSpan.FromMilliseconds(450)
				}
			};
			tokenValidator = new JwtTokenValidator(options.Issuer, options.Audience, options.SymmetricKey);
			loginService = new JwtLoginService(loggerFactory.CreateLogger<JwtLoginService>(), Options.Create(options));
		}

		[Fact]
		public async Task JwtLoginServiceIssuesValidTokenForValidCredentials() {
			var user = new User();
			var token = await loginService.LoginAsync(42, "UserSecret", async id => {
				await Task.CompletedTask;
				Assert.Equal(42, id);
				return user;
			}, u => {
				Assert.Same(user, u);
				return SecretHashing.CreateHashedSecret("UserSecret");
			}, (u, s) => {
				throw new XunitException("The login service should not need to rehash in this test case.");
			});
			Assert.NotNull(token);
			var (principal, validatedToken) = tokenValidator.Validate(token!);
			Assert.Equal("JwtLoginServiceUnitTest", validatedToken.Issuer);
			Assert.Equal("42", Assert.Single(principal.Claims, c => c.Type.Equals("userid", StringComparison.OrdinalIgnoreCase)).Value);
		}

		[Fact]
		public async Task JwtLoginServiceSupportsIssuingAdditionalClaimsForUser() {
			var user = new User();
			var token = await loginService.LoginAsync(42, "UserSecret", async id => {
				await Task.CompletedTask;
				Assert.Equal(42, id);
				return user;
			}, u => {
				Assert.Same(user, u);
				return SecretHashing.CreateHashedSecret("UserSecret");
			}, (u, s) => {
				throw new XunitException("The login service should not need to rehash in this test case.");
			},
			("message", u => "Hello World"),
			("number", u => 1234.ToString()));
			Assert.NotNull(token);
			var (principal, validatedToken) = tokenValidator.Validate(token!);
			Assert.Equal("JwtLoginServiceUnitTest", validatedToken.Issuer);
			Assert.Equal("42", Assert.Single(principal.Claims, c => c.Type.Equals("userid", StringComparison.OrdinalIgnoreCase)).Value);
			Assert.Equal("Hello World", Assert.Single(principal.Claims, c => c.Type.Equals("message", StringComparison.OrdinalIgnoreCase)).Value);
			Assert.Equal("1234", Assert.Single(principal.Claims, c => c.Type.Equals("number", StringComparison.OrdinalIgnoreCase)).Value);
		}

		[Fact]
		public async Task JwtLoginServiceIssuesNoTokenForNonExistentUserAndTakesAtLeastFailureDelay() {
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			var token = await loginService.LoginAsync(42, "UserSecret", async id => {
				await Task.CompletedTask;
				Assert.Equal(42, id);
				return (User?)null;
			}, u => {
				throw new XunitException("The login service should not call this because it got no user object.");
			}, (u, s) => {
				throw new XunitException("The login service should not need to rehash in this test case.");
			});
			stopwatch.Stop();
			Assert.Null(token);
			// Ensure that the login operation only fails after the minimum delay for failures.
			// This increases security by
			// - preventing timing attacks to differentiate between nonexistent users and incorrect secrets (the delay should be longer than the longest failure path takes)
			// - slowing down brute force attacks
			Assert.InRange(stopwatch.Elapsed, options.LoginService.FailureDelay - delayTolerance, TimeSpan.MaxValue);
		}

		[Fact]
		public async Task JwtLoginServiceIssuesNoTokenForIncorrectUserSecretAndTakesAtLeastFailureDelay() {
			var user = new User();
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			var token = await loginService.LoginAsync(42, "WrongSecret", async id => {
				await Task.CompletedTask;
				Assert.Equal(42, id);
				return user;
			}, u => {
				Assert.Same(user, u);
				return SecretHashing.CreateHashedSecret("UserSecret");
			}, (u, s) => {
				throw new XunitException("The login service should not need to rehash in this test case.");
			});
			stopwatch.Stop();
			Assert.Null(token);
			// Ensure that the login operation only fails after the minimum delay for failures.
			// This increases security by
			// - preventing timing attacks to differentiate between nonexistent users and incorrect secrets (the delay should be longer than the longest failure path takes)
			// - slowing down brute force attacks
			Assert.InRange(stopwatch.Elapsed, options.LoginService.FailureDelay - delayTolerance, TimeSpan.MaxValue);
		}

		[Fact]
		public async Task JwtLoginServiceCorrectlyHandlesRehashing() {
			var secret = "UserSecret";

			// Force creation of a hash with outdated parameters:
			var optionField = typeof(SecretHashing).GetField("options", BindingFlags.Static | BindingFlags.NonPublic);
			var hasherOptions = optionField!.GetValue(null) as IOptions<PasswordHasherOptions>;
			hasherOptions!.Value.IterationCount = 100;
			var hashedSecret = SecretHashing.CreateHashedSecret(secret);
			hasherOptions!.Value.IterationCount = 10000;

			bool calledHashUpdate = false;
			var user = new User();
			var token = await loginService.LoginAsync(42, secret, async id => {
				await Task.CompletedTask;
				Assert.Equal(42, id);
				return user;
			}, u => {
				Assert.Same(user, u);
				return hashedSecret;
			}, async (u, hs) => {
				await Task.CompletedTask;
				Assert.Same(user, u);
				// check if given hashed secret still matches the secret:
				var (success, rehashed) = SecretHashing.VerifyHashedSecret(ref hs, secret);
				Assert.True(success);
				Assert.False(rehashed);
				calledHashUpdate = true;
			});
			Assert.True(calledHashUpdate);
			Assert.NotNull(token);
			var (principal, validatedToken) = tokenValidator.Validate(token!);
			Assert.Equal("JwtLoginServiceUnitTest", validatedToken.Issuer);
			Assert.Equal("42", Assert.Single(principal.Claims, c => c.Type.Equals("userid", StringComparison.OrdinalIgnoreCase)).Value);
		}
	}
}
