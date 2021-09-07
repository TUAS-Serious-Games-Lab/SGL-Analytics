using System;

namespace SGL.Analytics.Backend.Domain.Exceptions {

	public class ConflictException : Exception {
		public ConflictException(string message, Exception? innerException = null) : base(message, innerException) { }
	}

	public class EntityUniquenessConflictException : ConflictException {
		public string EntityTypeName { get; set; }
		public string ConflictingPropertyName { get; set; }

		public EntityUniquenessConflictException(string entityTypeName, string conflictingPropertyName, Exception? innerException = null) :
			base($"A record of type {entityTypeName} with the given {conflictingPropertyName} already exists.", innerException) {
			EntityTypeName = entityTypeName;
			ConflictingPropertyName = conflictingPropertyName;
		}
	}

	public class ConcurrencyConflictException : ConflictException {
		public ConcurrencyConflictException(Exception? innerException = null) :
			base("The operation could not be completed due to a concurrent access from another operation.", innerException) { }
	}
}
