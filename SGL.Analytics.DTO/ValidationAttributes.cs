using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Specifies that the property carrying this attribute must be a string, must not contain special characters outside of letters, digits, brackets (round, square, curly), '-', '.', or '_', and must not contain multiple adjacent '.'s.
	/// </summary>
	public class PlainNameAttribute : ValidationAttribute {
		/// <summary>
		/// Constructs the attribute object using the default error message.
		/// </summary>
		public PlainNameAttribute() : base("{0} must be a string, must not contain special characters outside of letters, digits, brackets (round, square, curly), '-', '.', or '_', and must not contain multiple adjacent '.'s.") { }
		/// <summary>
		/// Constructs the attribute object using the given custom error message.
		/// </summary>
		/// <param name="errorMessage">A custom error message template, <c>{0}</c> is formated as the <see cref="ValidationContext.DisplayName"/>.</param>
		public PlainNameAttribute(string errorMessage) : base(errorMessage) { }

		/// <inheritdoc/>
		protected override ValidationResult IsValid(object? value, ValidationContext validationContext) {
			if (value == null) return ValidationResult.Success!;
			if (!(value is string strVal)) return FailValidation(validationContext);
			var hasInvalidChars = strVal.Any(c => c switch {
				(>= 'A') and (<= 'Z') => false,
				(>= 'a') and (<= 'z') => false,
				(>= '0') and (<= '9') => false,
				'{' or '}' or '[' or ']' or '(' or ')' => false,
				'-' or '.' or '_' => false,
				_ => true
			});
			if (hasInvalidChars) return FailValidation(validationContext);
			if (strVal.Contains("..")) return FailValidation(validationContext);
			return ValidationResult.Success!;
		}

		private ValidationResult FailValidation(ValidationContext validationContext) {
			return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
		}
	}
}
