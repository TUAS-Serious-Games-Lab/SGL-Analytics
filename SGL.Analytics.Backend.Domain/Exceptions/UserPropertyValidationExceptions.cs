using SGL.Analytics.Backend.Domain.Entity;
using System;

namespace SGL.Analytics.Backend.Domain.Exceptions {
	/// <summary>
	/// The base class for exceptions thrown when an invalid property is encountered by <see cref="UserRegistration"/>, <see cref="ApplicationWithUserProperties"/>, <see cref="ApplicationUserPropertyDefinition"/>, or <see cref="ApplicationUserPropertyInstance"/>.
	/// </summary>
	public abstract class UserPropertyValidationException : Exception {
		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		protected UserPropertyValidationException(string? message, Exception? innerException) : base(message, innerException) { }

		/// <summary>
		/// The name of the property that violates a validation condition.
		/// </summary>
		public abstract string InvalidPropertyName { get; }
	}

	/// <summary>
	/// The exception type thrown when an empty value is given for a required property of the user's application.
	/// </summary>
	public class RequiredPropertyNullException : UserPropertyValidationException {
		/// <summary>
		/// The name of the required property that has an empty value.
		/// </summary>
		public string NullPropertyName { get; init; }

		/// <summary>
		/// Returns <see cref="NullPropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => NullPropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public RequiredPropertyNullException(string nullPropertyName, Exception? innerException = null) :
			base($"The property {nullPropertyName} is required but has a null value.", innerException) {
			NullPropertyName = nullPropertyName;
		}
	}

	/// <summary>
	/// The exception type thrown when there is no property instance on the user registration for a required property definition of the user's application.
	/// </summary>
	public class RequiredPropertyMissingException : UserPropertyValidationException {
		/// <summary>
		/// The name of the propery definition for which the instance is missing.
		/// </summary>
		public string MissingPropertyName { get; init; }

		/// <summary>
		/// Returns <see cref="MissingPropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => MissingPropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public RequiredPropertyMissingException(string missingPropertyName, Exception? innerException = null) :
			base($"The required property {missingPropertyName} is not present.", innerException) {
			MissingPropertyName = missingPropertyName;
		}
	}

	/// <summary>
	/// The exception type thrown when trying to create a property instance for a property name where no corresponding property definition is present for the user's application.
	/// </summary>
	public class UndefinedPropertyException : UserPropertyValidationException {
		/// <summary>
		/// The name of the undefined property.
		/// </summary>
		public string UndefinedPropertyName { get; init; }

		/// <summary>
		/// Returns <see cref="UndefinedPropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => UndefinedPropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public UndefinedPropertyException(string undefinedPropertyName, Exception? innerException = null) :
			base($"The property {undefinedPropertyName} is present but not defined for the associated application.", innerException) {
			UndefinedPropertyName = undefinedPropertyName;
		}
	}

	/// <summary>
	/// The exception type thrown when a value of a type is given for a property that doesn't match the type specified in the property definition.
	/// </summary>
	public class PropertyTypeDoesntMatchDefinitionException : UserPropertyValidationException {
		/// <summary>
		/// The name of the property with for which a non-matching value was given.
		/// </summary>
		public override string InvalidPropertyName { get; }
		/// <summary>
		/// The type of the supplied value.
		/// </summary>
		public Type ValueType { get; }
		/// <summary>
		/// The type specified in the property definition.
		/// </summary>
		public UserPropertyType DefinitionType { get; }

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public PropertyTypeDoesntMatchDefinitionException(string invalidPropertyName, Type valueType, UserPropertyType definitionType, Exception? innerException = null) :
			base($"The value of type {valueType.Name} that was given for the property {invalidPropertyName} is not compatible with the type {definitionType.ToString()} as which the property is defined.", innerException) {
			InvalidPropertyName = invalidPropertyName;
			ValueType = valueType;
			DefinitionType = definitionType;
		}
	}

	/// <summary>
	/// The exception type thrown when a property definition with an unknown type is encountered in the data layer.
	/// </summary>
	public class PropertyWithUnknownTypeException : UserPropertyValidationException {
		/// <summary>
		/// The name of the property definition with the unknown type.
		/// </summary>
		public override string InvalidPropertyName { get; }
		/// <summary>
		/// The unknown type specification.
		/// </summary>
		public string UnknownType { get; }
		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public PropertyWithUnknownTypeException(string invalidPropertyName, string unknownType, Exception? innerException = null) :
			base($"The property {invalidPropertyName} the unknown property type {unknownType}.", innerException) {
			InvalidPropertyName = invalidPropertyName;
			UnknownType = unknownType;
		}
	}

	/// <summary>
	/// The exception type thrown when trying to add a property definition with a name that is already in use by another property definition on the same application.
	/// </summary>
	public class ConflictingPropertyNameException : UserPropertyValidationException {
		/// <summary>
		/// The name that conflicts between two definitions.
		/// </summary>
		public string ConflictingPropertyName { get; }

		/// <summary>
		/// Returns <see cref="ConflictingPropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => ConflictingPropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public ConflictingPropertyNameException(string conflictingPropertyName, Exception? innerException = null) :
			base($"The property name {conflictingPropertyName} is already in use by another property.", innerException) {
			ConflictingPropertyName = conflictingPropertyName;
		}
	}

	/// <summary>
	/// The exception type thrown when trying to lookup a property instance (by the name of its definition) that doesn't exist for the given user or application.
	/// </summary>
	public class PropertyNotFoundException : UserPropertyValidationException {
		/// <summary>
		/// The name of the property that wasn't found.
		/// </summary>
		public string MissingPropertyName { get; init; }

		/// <summary>
		/// Returns <see cref="MissingPropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => MissingPropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public PropertyNotFoundException(string missingPropertyName, Exception? innerException = null) :
			base($"The requested property {missingPropertyName} is not present.", innerException) {
			MissingPropertyName = missingPropertyName;
		}
	}

	/// <summary>
	/// The exception type thrown when trying to create multiple instances of the same property definition for the same user.
	/// </summary>
	public class ConflictingPropertyInstanceException : UserPropertyValidationException {
		/// <summary>
		/// The name of the duplicate property.
		/// </summary>
		public string PropertyName { get; }

		/// <summary>
		/// Returns <see cref="PropertyName"/>.
		/// </summary>
		public override string InvalidPropertyName => PropertyName;

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public ConflictingPropertyInstanceException(string propertyName, Exception? innerException = null) :
			base($"There is more than one instance of the property {propertyName} present for the same user registration.", innerException) {
			PropertyName = propertyName;
		}
	}

}
