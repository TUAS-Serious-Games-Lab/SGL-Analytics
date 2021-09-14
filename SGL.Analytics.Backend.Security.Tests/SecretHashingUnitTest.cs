using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SGL.Analytics.Backend.Security.Tests {
	public class SecretHashingUnitTest {
		[Fact]
		public void CorrectSecretIsSuccessfullyValidatedAgainstHash() {
			var secret = SecretGenerator.Instance.GenerateSecret(16);
			var hashedSecret = SecretHashing.CreateHashedSecret(secret);
			var (success, rehashed) = SecretHashing.VerifyHashedSecret(ref hashedSecret, secret);
			Assert.True(success);
			Assert.False(rehashed);
		}
	}
}
