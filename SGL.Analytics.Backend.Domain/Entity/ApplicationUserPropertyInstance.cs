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
	public class ApplicationUserPropertyInstance {
		private static JsonSerializerOptions jsonOptions = new JsonSerializerOptions { Converters = { new ObjectDictionaryValueJsonConverter() } };

		[Key]
		public int Id { get; set; }
		public int DefinitionId { get; set; }
		public Guid UserId { get; set; }
		public ApplicationUserPropertyDefinition Definition { get; set; } = null!;
		public UserRegistration User { get; set; } = null!;

		public int? IntegerValue { get; set; }
		public double? FloatingPointValue { get; set; }
		public string? StringValue { get; set; }
		public DateTime? DateTimeValue { get; set; }
		public Guid? GuidValue { get; set; }
		public string? JsonValue { get; set; }

		[NotMapped]
		public object? Value {
			get => Definition.Type switch {
				UserPropertyType.Integer => IntegerValue,
				UserPropertyType.FloatingPoint => FloatingPointValue,
				UserPropertyType.String => StringValue,
				UserPropertyType.DateTime => DateTimeValue,
				UserPropertyType.Guid => GuidValue,
				UserPropertyType.Json => JsonSerializer.Deserialize<object?>(JsonValue ?? "null", jsonOptions),
				_ => throw new InvalidOperationException("The user property definition has an unknown type.")
			} ?? (Definition.Required ? throw new InvalidOperationException("The required property unexpectedly contains a null value.") : null);
			set {
				switch (value) {
					case null when Definition.Required:
						throw new ArgumentException("A null value is invalid for a required property.");
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
						throw new ArgumentException("The type of the given value doesn't match the type of the user property definition.");
				}
			}
		}

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
	}
}
