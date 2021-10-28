using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Security {

	/// <summary>
	/// An exception base class for exceptions representing errors with authentication claims.
	/// </summary>
	public class ClaimException : Exception {
		/// <summary>
		/// The claim type (i.e. key) for which an error happened.
		/// </summary>
		public string ClaimType { get; set; }

		/// <summary>
		/// Instantiates the exception base class with the given <see cref="ClaimType"/> and error message.
		/// </summary>
		/// <param name="claimType"></param>
		/// <param name="message"></param>
		protected ClaimException(string claimType, string message) : base(message) {
			ClaimType = claimType;
		}
	}

	/// <summary>
	/// This exception type is thrown if no claim was found for a type that was expected to be present.
	/// </summary>
	public class ClaimNotFoundException : ClaimException {
		/// <summary>
		/// Instantiates an exception object for the given <see cref="ClaimException.ClaimType"/>.
		/// </summary>
		/// <param name="claimType">The claim type for which no claim was found.</param>
		public ClaimNotFoundException(string claimType) :
			base(claimType, $"No claim of type {claimType} was present.") { }
	}
	/// <summary>
	/// This exception type is thrown if the value of a claim doesn't have the expected format and thus can't be parsed as the .Net type that is needed.
	/// </summary>
	public class InvalidClaimFormatException : ClaimException {
		/// <summary>
		/// Instantiates an exception object for the given <see cref="ClaimException.ClaimType"/> and target .Net type.
		/// </summary>
		/// <param name="claimType">The claim type with the invalid value.</param>
		/// <param name="type">The type as which it was unsuccessfully attempted to parse.</param>
		public InvalidClaimFormatException(string claimType, Type type) :
			base(claimType, $"The claim of type {claimType} has an invalid format and could not be parsed as the target type {type.FullName}.") { }
	}

	/// <summary>
	/// Provides extension methods to simplify working with authentication <see cref="Claim"/>s in <see cref="ClaimsPrincipal"/>s.
	/// </summary>
	public static class ClaimsExtensions {
		/// <summary>
		/// The delegate type used to attempt to parse claim values.
		/// It is designed to be compatible with the usual signature of <c>TryParse</c> methods on types that are used for the values.
		/// </summary>
		/// <typeparam name="T">The type as which the claim is </typeparam>
		/// <param name="claimValue">The raw string value of the claim.</param>
		/// <param name="parsedValue">The parsed value is assigned to this argument.</param>
		/// <returns><see langword="true"/> if the value was parsed successfully, <see langword="false"/> otherwise.</returns>
		public delegate bool TryParser<T>(string claimValue, out T parsedValue);

		/// <summary>
		/// Looks for a claim with the given type / name in the claims sequence and returns the value of the first one as a string.
		/// </summary>
		/// <param name="claims">A sequence of claims to search.</param>
		/// <param name="claimType">The claim type to look for.</param>
		/// <returns>The value of the first claim of the given type.</returns>
		/// <exception cref="ClaimNotFoundException">When no such claim is present.</exception>
		public static string GetClaim(this IEnumerable<Claim> claims, string claimType) {
			var claim = claims.FirstOrDefault(c => c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase));
			if (claim is null) {
				throw new ClaimNotFoundException(claimType);
			}
			else {
				return claim.Value;
			}
		}
		/// <summary>
		/// Looks for a claim with the given type / name in the claims sequence, and returns its value parsed as <c>T</c>.
		/// </summary>
		/// <typeparam name="T">The type as which the value is parsed.</typeparam>
		/// <param name="claims">A sequence of claims to search.</param>
		/// <param name="claimType">The claim type to look for.</param>
		/// <param name="tryParser">The delegate to be used for attempting to parse the value.</param>
		/// <returns>The parsed value of the first claim of the given type.</returns>
		/// <exception cref="ClaimNotFoundException">When no such claim is present.</exception>
		/// <exception cref="InvalidClaimFormatException">When the claim value could not be parsed as the given type.</exception>
		public static T GetClaim<T>(this IEnumerable<Claim> claims, string claimType, TryParser<T> tryParser) {
			var value = claims.GetClaim(claimType);
			if (tryParser(value, out var parsedValue)) {
				return parsedValue;
			}
			else {
				throw new InvalidClaimFormatException(claimType, typeof(T));
			}
		}
		/// <summary>
		/// A convenience method to apply <see cref="GetClaim(IEnumerable{Claim}, string)"/> to the claims of a <see cref="ClaimsPrincipal"/>.
		/// </summary>
		public static string GetClaim(this ClaimsPrincipal principal, string claimType) {
			return principal.Claims.GetClaim(claimType);
		}
		/// <summary>
		/// A convenience method to apply <see cref="GetClaim{T}(IEnumerable{Claim}, string, TryParser{T})"/> to the claims of a <see cref="ClaimsPrincipal"/>.
		/// </summary>
		public static T GetClaim<T>(this ClaimsPrincipal principal, string claimType, TryParser<T> tryParser) {
			return principal.Claims.GetClaim<T>(claimType, tryParser);
		}
	}
}
