using System;

namespace SGL.Analytics.Backend.Domain.Exceptions {

	/// <summary>
	/// The base class for exceptions thrown if a conflict occurs in the data layer.
	/// </summary>
	public class ConflictException : Exception {
		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public ConflictException(string message, Exception? innerException = null) : base(message, innerException) { }
	}

	/// <summary>
	/// The exception type thrown if an entity violates defined uniqueness constraints.
	/// </summary>
	public class EntityUniquenessConflictException : ConflictException {
		/// <summary>
		/// The type name of the affected entity.
		/// </summary>
		public string EntityTypeName { get; set; }
		/// <summary>
		/// The name of the conflicting property.
		/// </summary>
		public string ConflictingPropertyName { get; set; }
		/// <summary>
		/// The value of the conflicting property.
		/// </summary>
		public object ConflictingPropertyValue { get; set; }

		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public EntityUniquenessConflictException(string entityTypeName, string conflictingPropertyName, object conflictingPropertyValue, Exception? innerException = null) :
			base($"A record of type {entityTypeName} with the given {conflictingPropertyName} already exists.", innerException) {
			EntityTypeName = entityTypeName;
			ConflictingPropertyName = conflictingPropertyName;
			ConflictingPropertyValue = conflictingPropertyValue;
		}
	}

	/// <summary>
	/// The exception type thrown if an update operation couldn't be completed due to a conflicting modification of an operation that happened concurrently.
	/// </summary>
	public class ConcurrencyConflictException : ConflictException {
		/// <summary>
		/// Creates an exception object with the given error information.
		/// </summary>
		public ConcurrencyConflictException(Exception? innerException = null) :
			base("The operation could not be completed due to a concurrent access from another operation.", innerException) { }
	}
}
