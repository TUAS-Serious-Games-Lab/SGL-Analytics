using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Domain.Entity {
	/// <summary>
	/// Models an instance of a per-user property for an application, containing the value of the property for a specific user.
	/// </summary>
	public class ApplicationUserPropertyInstance {
		private static JsonSerializerOptions jsonOptions = new JsonSerializerOptions { Converters = { new ObjectDictionaryValueJsonConverter() } };

		/// <summary>
		/// The unique database id of the  property definition.
		/// </summary>
		[Key]
		public int Id { get; set; }
		/// <summary>
		/// The id of the propery definition that this instance instantiates.
		/// </summary>
		public int DefinitionId { get; set; }
		/// <summary>
		/// The id of the user to which this instance belongs.
		/// </summary>
		public Guid UserId { get; set; }
		/// <summary>
		/// The propery definition that this instance instantiates.
		/// </summary>
		public ApplicationUserPropertyDefinition Definition { get; set; } = null!;
		/// <summary>
		/// The user to which this instance belongs.
		/// </summary>
		public UserRegistration User { get; set; } = null!;
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains the value of the property instance if it is <see cref="UserPropertyType.Integer"/>-typed.
		/// </summary>
		public int? IntegerValue { get; set; }
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains the value of the property instance if it is <see cref="UserPropertyType.FloatingPoint"/>-typed.
		/// </summary>
		public double? FloatingPointValue { get; set; }
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains the value of the property instance if it is <see cref="UserPropertyType.String"/>-typed.
		/// </summary>
		public string? StringValue { get; set; }
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains the value of the property instance if it is <see cref="UserPropertyType.DateTime"/>-typed.
		/// </summary>
		public DateTime? DateTimeValue { get; set; }
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains the value of the property instance if it is <see cref="UserPropertyType.Guid"/>-typed.
		/// </summary>
		public Guid? GuidValue { get; set; }
		/// <summary>
		/// This property is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Value"/> instead.
		/// It contains a string representation of the value of the property instance if it is <see cref="UserPropertyType.Json"/>-typed.
		/// </summary>
		public string? JsonValue { get; set; }

		/// <summary>
		/// Indicates whether the value represents an empty state.
		/// </summary>
		public bool IsNull() => Definition.Type switch {
			UserPropertyType.Integer => IntegerValue is null,
			UserPropertyType.FloatingPoint => FloatingPointValue is null,
			UserPropertyType.String => StringValue is null,
			UserPropertyType.DateTime => DateTimeValue is null,
			UserPropertyType.Guid => GuidValue is null,
			UserPropertyType.Json => string.IsNullOrWhiteSpace(JsonValue) || JsonValue == "null",
			_ => throw new PropertyWithUnknownTypeException(Definition.Name, Definition.Type.ToString())
		};

		/// <summary>
		/// Provides access to the typed value of the property.
		/// Get access for a primitively typed property returns the value from the ...<c>Value</c> property associated with the type indicated by <see cref="ApplicationUserPropertyDefinition.Type"/> of <see cref="Definition"/>.
		/// Get access for a JSON-typed property deserializes the JSON string representation (stored in <see cref="JsonValue"/>) and returns the deserialized object.
		/// Set access for a primitively typed property sets the value of the ...<c>Value</c> property associated with the type indicated by <see cref="ApplicationUserPropertyDefinition.Type"/> of <see cref="Definition"/>.
		/// Set access for a JSON-typed property serializes the value into a JSON string representation and stores it in <see cref="JsonValue"/>.
		/// JSON (de-)serialization is done using <see cref="ObjectDictionaryValueJsonConverter"/>.
		/// </summary>
		/// <exception cref="RequiredPropertyNullException">A <see langword="null"/> value was encountered for a property instance of a property that is defined as <see cref="ApplicationUserPropertyDefinition.Required"/>.</exception>
		/// <exception cref="PropertyTypeDoesntMatchDefinitionException">The type of the value given to a set operation doesn't match the data type specified in the property definition.</exception>
		/// <exception cref="PropertyWithUnknownTypeException">An unknown property type was encountered.</exception>
		[NotMapped]
		public object? Value {
			get => Definition.Type switch {
				UserPropertyType.Integer => IntegerValue,
				UserPropertyType.FloatingPoint => FloatingPointValue,
				UserPropertyType.String => StringValue,
				UserPropertyType.DateTime => DateTimeValue,
				UserPropertyType.Guid => GuidValue,
				UserPropertyType.Json => JsonSerializer.Deserialize<object?>(JsonValue ?? "null", jsonOptions),
				_ => throw new PropertyWithUnknownTypeException(Definition.Name, Definition.Type.ToString())
			} ?? (Definition.Required ? throw new RequiredPropertyNullException(Definition.Name) : null);
			set {
				switch (value) {
					case null when Definition.Required:
						throw new RequiredPropertyNullException(Definition.Name);
					case null:
						IntegerValue = null;
						FloatingPointValue = null;
						StringValue = null;
						DateTimeValue = null;
						GuidValue = null;
						JsonValue = null;
						break;
					case int intVal when Definition.Type == UserPropertyType.Integer:
						IntegerValue = intVal;
						break;
					case double fpVal when Definition.Type == UserPropertyType.FloatingPoint:
						FloatingPointValue = fpVal;
						break;
					case string strVal when Definition.Type == UserPropertyType.String:
						StringValue = strVal;
						break;
					case DateTime dtVal when Definition.Type == UserPropertyType.DateTime:
						DateTimeValue = dtVal;
						break;
					case Guid guidVal when Definition.Type == UserPropertyType.Guid:
						GuidValue = guidVal;
						break;
					case object when Definition.Type == UserPropertyType.Json:
						JsonValue = JsonSerializer.Serialize<object?>(value, jsonOptions);
						break;
					default:
						throw new PropertyTypeDoesntMatchDefinitionException(Definition.Name, value.GetType(), Definition.Type);
				}
			}
		}

		/// <summary>
		/// This constructor is public to be accessible for OR mapper and should not be used directly otherwise,
		/// use <see cref="Create(ApplicationUserPropertyDefinition, UserRegistration)"/> or <see cref="Create(ApplicationUserPropertyDefinition, UserRegistration, object?)"/> instead.
		/// It instantiates an object with the given underlying data values from the OR mapper.
		/// </summary>
		public ApplicationUserPropertyInstance(int id, int definitionId, Guid userId,
			int? integerValue = null, double? floatingPointValue = null,
			string? stringValue = null, DateTime? dateTimeValue = null, Guid? guidValue = null) {
			Id = id;
			DefinitionId = definitionId;
			UserId = userId;
			IntegerValue = integerValue;
			FloatingPointValue = floatingPointValue;
			StringValue = stringValue;
			DateTimeValue = dateTimeValue;
			GuidValue = guidValue;
		}

		/// <summary>
		/// Creates a property instance for the given property definition and the given user.
		/// The value is initialized as empty and if the property is required, the value needs to be set using <see cref="Value"/> before the object is persisted.
		/// </summary>
		/// <param name="def">The property definition to instantiate.</param>
		/// <param name="user">The user for which it is instantiated.</param>
		/// <returns>The created property instance object.</returns>
		public static ApplicationUserPropertyInstance Create(ApplicationUserPropertyDefinition def, UserRegistration user) {
			var pi = new ApplicationUserPropertyInstance(0, def.Id, user.Id);
			pi.Definition = def;
			pi.User = user;
			return pi;
		}
		/// <summary>
		/// Creates a property instance for the given property definition and the given user with the given value.
		/// </summary>
		/// <param name="def">The property definition to instantiate.</param>
		/// <param name="user">The user for which it is instantiated.</param>
		/// <param name="value">The value of the property for the user. It is processed as if by setting it in <see cref="Value"/>.</param>
		/// <returns>The created property instance object.</returns>
		public static ApplicationUserPropertyInstance Create(ApplicationUserPropertyDefinition def, UserRegistration user, object? value) {
			var pi = Create(def, user);
			pi.Value = value;
			return pi;
		}
	}
}
