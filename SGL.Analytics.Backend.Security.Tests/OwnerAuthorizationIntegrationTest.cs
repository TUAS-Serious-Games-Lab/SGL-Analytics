using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Security.Tests.OwnerAuthorizationScenario;
using SGL.Analytics.Backend.TestUtilities;
using SGL.Analytics.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SGL.Analytics.Backend.Security.Tests {

	public class OwnerAuthorizationIntegrationTestFixture : WebApplicationFactory<Startup> {
		public JwtOptions JwtOptions { get; } = new JwtOptions() {
			Audience = "LogCollectorIntegrationTest",
			Issuer = "LogCollectorIntegrationTest",
			SymmetricKey = "TestingS3cr3tTestingS3cr3t"
		};
		public Dictionary<string, string> JwtConfig { get; }

		public ITestOutputHelper? Output { get; set; } = null;
		public JwtTokenGenerator TokenGenerator { get; }

		public OwnerAuthorizationIntegrationTestFixture() {
			JwtConfig = new() {
				["Jwt:Audience"] = JwtOptions.Audience,
				["Jwt:Issuer"] = JwtOptions.Issuer,
				["Jwt:SymmetricKey"] = JwtOptions.SymmetricKey,
			};
			TokenGenerator = new JwtTokenGenerator(JwtOptions.Issuer, JwtOptions.Audience, JwtOptions.SymmetricKey);
		}

		protected override IHostBuilder CreateHostBuilder() {
			return Host.CreateDefaultBuilder()
				.ConfigureWebHostDefaults(webBuilder => {
					webBuilder.UseStartup<Startup>();
				}).ConfigureAppConfiguration(config => config
					.AddInMemoryCollection(JwtConfig))
					.ConfigureLogging(logging => logging.AddXUnit(() => Output).SetMinimumLevel(LogLevel.Debug));
		}
	}

	public class OwnerAuthorizationIntegrationTest : IClassFixture<OwnerAuthorizationIntegrationTestFixture> {
		private ITestOutputHelper output;
		private OwnerAuthorizationIntegrationTestFixture fixture;

		public OwnerAuthorizationIntegrationTest(ITestOutputHelper output, OwnerAuthorizationIntegrationTestFixture fixture) {
			this.output = output;
			this.fixture = fixture;
			fixture.Output = output;
		}

		[Fact]
		public async Task OwnerAuthorizationDeniesAccessForUnauthenticatedUserAndChallenges() {
			var userId = Guid.NewGuid();
			using (var client = fixture.CreateClient()) {
				var request = new HttpRequestMessage(HttpMethod.Get, $"/api/OwnerAuthorizationTest/user1/{userId}");
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
				Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
			}
		}

		[Theory]
		[InlineData("user1")]
		[InlineData("user2")]
		[InlineData("user3")]
		[InlineData("user4")]
		[InlineData("owner1")]
		[InlineData("owner2")]
		[InlineData("owner3")]
		[InlineData("owner4")]
		public async Task OwnerAuthorizationGrantsAccessForOwnerBasedOnRouteParam(string subRoute) {
			var userId = Guid.NewGuid();
			var token = fixture.TokenGenerator.GenerateToken(userId, TimeSpan.FromMinutes(5));
			using (var client = fixture.CreateClient()) {
				var request = new HttpRequestMessage(HttpMethod.Get, $"/api/OwnerAuthorizationTest/{subRoute}/{userId}");
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
				var response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();
				await using (var stream = await response.Content.ReadAsStreamAsync()) {
					var readUserId = await JsonSerializer.DeserializeAsync<Guid>(stream);
					Assert.Equal(userId, readUserId);
				}
			}
		}

		[Theory]
		[InlineData("user1")]
		[InlineData("user2")]
		[InlineData("user3")]
		[InlineData("user4")]
		[InlineData("owner1")]
		[InlineData("owner2")]
		[InlineData("owner3")]
		[InlineData("owner4")]
		public async Task OwnerAuthorizationDeniesAccessForNonOwnerBasedOnRouteParam(string subRoute) {
			var ownerId = Guid.NewGuid();
			var token = fixture.TokenGenerator.GenerateToken(Guid.NewGuid(), TimeSpan.FromMinutes(5));
			using (var client = fixture.CreateClient()) {
				var request = new HttpRequestMessage(HttpMethod.Get, $"/api/OwnerAuthorizationTest/{subRoute}/{ownerId}");
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
				var response = await client.SendAsync(request);
				Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
			}
		}
	}
}
