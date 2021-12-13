using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SGL.Analytics.DTO {
	public class PlainNameAttribute : ValidationAttribute {
		public PlainNameAttribute() : base("{0} must be a string, must not contain special characters outside of letters, digits, brackets (round, square, curly), '-', '.', or '_', and must not contain multiple adjacent '.'s.") { }
		public PlainNameAttribute(string errorMessage) : base(errorMessage) { }

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
