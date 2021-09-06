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

		public bool IsNull() => Definition.Type switch {
			UserPropertyType.Integer => IntegerValue is null,
			UserPropertyType.FloatingPoint => FloatingPointValue is null,
			UserPropertyType.String => StringValue is null,
			UserPropertyType.DateTime => DateTimeValue is null,
			UserPropertyType.Guid => GuidValue is null,
			UserPropertyType.Json => string.IsNullOrWhiteSpace(JsonValue) || JsonValue == "null",
			_ => throw new PropertyWithUnknownTypeException(Definition.Name, Definition.Type.ToString())
		};

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

		public static ApplicationUserPropertyInstance Create(ApplicationUserPropertyDefinition def, UserRegistration user) {
			var pi = new ApplicationUserPropertyInstance(0, def.Id, user.Id);
			pi.Definition = def;
			pi.User = user;
			return pi;
		}
		public static ApplicationUserPropertyInstance Create(ApplicationUserPropertyDefinition def, UserRegistration user, object? value) {
			var pi = Create(def, user);
			pi.Value = value;
			return pi;
		}
	}
}
