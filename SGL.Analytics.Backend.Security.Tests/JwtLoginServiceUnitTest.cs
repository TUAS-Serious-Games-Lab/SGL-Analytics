using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGL.Analytics.TestUtilities;
using System;
using System.Collections.Generic;
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
		private JwtLoginService loginService;
		private JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

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
					FailureDelay = TimeSpan.FromMilliseconds(400)
				}
			};
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
			var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters() {
				ValidateIssuer = true,
				ValidateAudience = true,
				ValidateLifetime = true,
				ValidateIssuerSigningKey = true,
				ValidAudience = options.Audience,
				ValidIssuer = options.Issuer,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SymmetricKey ?? throw new InvalidOperationException("Missing key.")))
			}, out var validatedToken);
			Assert.Equal("JwtLoginServiceUnitTest", validatedToken.Issuer);
			Assert.Equal("42", Assert.Single(principal.Claims, c => c.Type.Equals("userid", StringComparison.OrdinalIgnoreCase)).Value);
		}

		[Fact]
		public async Task JwtLoginServiceIssuesNoTokenForNonExistentUserAndTakesAtLeastFailureDelay() {
			var start = DateTime.Now;
			var token = await loginService.LoginAsync(42, "UserSecret", async id => {
				await Task.CompletedTask;
				Assert.Equal(42, id);
				return (User?)null;
			}, u => {
				throw new XunitException("The login service should not call this because it got no user object.");
			}, (u, s) => {
				throw new XunitException("The login service should not need to rehash in this test case.");
			});
			var end = DateTime.Now;
			Assert.Null(token);
			// Ensure that the login operation only fails after the minimum delay for failures.
			// This increases security by
			// - preventing timing attacks to differentiate between nonexistent users and incorrect secrets (the delay should be longer than the longest failure path takes)
			// - slowing down brute force attacks
			Assert.InRange(end - start, options.LoginService.FailureDelay, TimeSpan.MaxValue);
		}

		[Fact]
		public async Task JwtLoginServiceIssuesNoTokenForIncorrectUserSecretAndTakesAtLeastFailureDelay() {
			var user = new User();
			var start = DateTime.Now;
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
			var end = DateTime.Now;
			Assert.Null(token);
			// Ensure that the login operation only fails after the minimum delay for failures.
			// This increases security by
			// - preventing timing attacks to differentiate between nonexistent users and incorrect secrets (the delay should be longer than the longest failure path takes)
			// - slowing down brute force attacks
			Assert.InRange(end - start, options.LoginService.FailureDelay, TimeSpan.MaxValue);
		}
	}
}
