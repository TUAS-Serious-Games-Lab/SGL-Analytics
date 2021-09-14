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
		[Fact]
		public void IncorrectSecretFailsValidationAgainstHash() {
			var secret = SecretGenerator.Instance.GenerateSecret(16);
			var hashedSecret = SecretHashing.CreateHashedSecret(secret);
			var modifiedSecret = new string(secret.Select(c => char.IsLetter(c) ? (char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c)) : c).ToArray());
			var (success, rehashed) = SecretHashing.VerifyHashedSecret(ref hashedSecret, modifiedSecret);
			Assert.False(success);
			Assert.False(rehashed);
		}
	}
}
