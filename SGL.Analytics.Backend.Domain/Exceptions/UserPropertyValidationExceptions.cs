using SGL.Analytics.Backend.Domain.Entity;
using System;

namespace SGL.Analytics.Backend.Domain.Exceptions {
	public abstract class UserPropertyValidationException : Exception {
		protected UserPropertyValidationException(string? message, Exception? innerException) : base(message, innerException) { }

		public abstract string InvalidPropertyName { get; }
	}

	public class RequiredPropertyNullException : UserPropertyValidationException {
		public string NullPropertyName { get; init; }

		public override string InvalidPropertyName => NullPropertyName;

		public RequiredPropertyNullException(string nullPropertyName, Exception? innerException = null) :
			base($"The property {nullPropertyName} is required but has a null value.", innerException) {
			NullPropertyName = nullPropertyName;
		}
	}

	public class RequiredPropertyMissingException : UserPropertyValidationException {
		public string MissingPropertyName { get; init; }

		public override string InvalidPropertyName => MissingPropertyName;

		public RequiredPropertyMissingException(string missingPropertyName, Exception? innerException = null) :
			base($"The required property {missingPropertyName} is not present.", innerException) {
			MissingPropertyName = missingPropertyName;
		}
	}

	public class UndefinedPropertyException : UserPropertyValidationException {
		public string UndefinedPropertyName { get; init; }

		public override string InvalidPropertyName => UndefinedPropertyName;

		public UndefinedPropertyException(string undefinedPropertyName, Exception? innerException = null) :
			base($"The property {undefinedPropertyName} is present but not defined for the associated application.", innerException) {
			UndefinedPropertyName = undefinedPropertyName;
		}
	}

	public class PropertyTypeDoesntMatchDefinitionException : UserPropertyValidationException {
		public override string InvalidPropertyName { get; }
		public Type ValueType { get; }
		public UserPropertyType DefinitionType { get; }

		public PropertyTypeDoesntMatchDefinitionException(string invalidPropertyName, Type valueType, UserPropertyType definitionType, Exception? innerException = null) :
			base($"The value of type {valueType.Name} that was given for the property {invalidPropertyName} is not compatible with the type {definitionType.ToString()} as which the property is defined.", innerException) {
			InvalidPropertyName = invalidPropertyName;
			ValueType = valueType;
			DefinitionType = definitionType;
		}
	}

	public class PropertyWithUnknownTypeException : UserPropertyValidationException {
		public override string InvalidPropertyName { get; }
		public string UnknownType { get; }
		public PropertyWithUnknownTypeException(string invalidPropertyName, string unknownType, Exception? innerException = null) :
			base($"The property {invalidPropertyName} the unknown property type {unknownType}.", innerException) {
			InvalidPropertyName = invalidPropertyName;
			UnknownType = unknownType;
		}
	}

	public class ConflictingPropertyNameException : UserPropertyValidationException {
		public string ConflictingPropertyName { get; }

		public override string InvalidPropertyName => ConflictingPropertyName;

		public ConflictingPropertyNameException(string conflictingPropertyName, Exception? innerException = null) :
			base($"The property name {conflictingPropertyName} is already in use by another property.", innerException) {
			ConflictingPropertyName = conflictingPropertyName;
		}
	}
}
