using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Security {

	public class ClaimException : Exception {
		public string ClaimType { get; set; }

		protected ClaimException(string claimType, string message) : base(message) {
			ClaimType = claimType;
		}
	}

	public class ClaimNotFoundException : ClaimException {
		public ClaimNotFoundException(string claimType) :
			base(claimType, $"No claim of type {claimType} was present.") { }
	}
	public class InvalidClaimFormatException : ClaimException {
		public InvalidClaimFormatException(string claimType, Type type) :
			base(claimType, $"The claim of type {claimType} has an invalid format and could not be parsed as the target type {type.FullName}.") { }
	}

	public static class ClaimsExtensions {
		public delegate bool TryParser<T>(string claimValue, out T parsedValue);
		public static string GetClaim(this IEnumerable<Claim> claims, string claimType) {
			var claim = claims.FirstOrDefault(c => c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase));
			if (claim is null) {
				throw new ClaimNotFoundException(claimType);
			}
			else {
				return claim.Value;
			}
		}
		public static T GetClaim<T>(this IEnumerable<Claim> claims, string claimType, TryParser<T> tryParser) {
			var value = claims.GetClaim(claimType);
			if (tryParser(value, out var parsedValue)) {
				return parsedValue;
			}
			else {
				throw new InvalidClaimFormatException(claimType, typeof(T));
			}
		}
		public static string GetClaim(this ClaimsPrincipal principal, string claimType) {
			return principal.Claims.GetClaim(claimType);
		}
		public static T GetClaim<T>(this ClaimsPrincipal principal, string claimType, TryParser<T> tryParser) {
			return principal.Claims.GetClaim<T>(claimType, tryParser);
		}
	}
}
