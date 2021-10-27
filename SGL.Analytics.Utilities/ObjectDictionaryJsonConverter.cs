using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> implementation that reads values statically typed as <c>object?</c> which are dynamically of a type that matches what type of value is present in the JSON input.
	/// I.e., booleans in JSON are read as <see langword="bool"/>s, numbers in JSON are read as the appropriate numeric type, strings containing a Guid are read as <see cref="Guid"/>s,
	/// strings containing date and/or time are read as <see cref="DateTime"/>s, other strings are read as <see cref="string"/>s, JSON arrays are read as <see cref="List{T}"/>s of <c>object?</c>, and
	/// JSON objects are read as <see cref="Dictionary{TKey, TValue}"/>s that map name <c>string</c>s to <c>object?</c> values.
	/// </summary>
	public class ObjectDictionaryValueJsonConverter : JsonConverter<object?> {
		private static ObjectDictionaryJsonConverter dictConverter = new ObjectDictionaryJsonConverter();
		/// <inheritdoc/>
		public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			switch (reader.TokenType) {
				case JsonTokenType.Null: return null;
				case JsonTokenType.True: return true;
				case JsonTokenType.False: return false;
				case JsonTokenType.Number when reader.TryGetInt32(out int i): return i;
				case JsonTokenType.Number when reader.TryGetInt64(out long l): return l;
				case JsonTokenType.Number: return reader.GetDouble();
				case JsonTokenType.String when reader.TryGetGuid(out Guid g): return g;
				case JsonTokenType.String when reader.TryGetDateTime(out DateTime dt): return dt;
				case JsonTokenType.String: return reader.GetString();
				case JsonTokenType.StartArray:
					var list = new List<object?>();
					while (reader.Read()) {
						if (reader.TokenType == JsonTokenType.EndArray) break;
						list.Add(this.Read(ref reader, typeof(object), options));
					}
					if (reader.TokenType == JsonTokenType.EndArray) {
						return list;
					}
					else {
						throw new JsonException("Unexpected end of JSON array.");
					}
				case JsonTokenType.StartObject:
					return dictConverter.Read(ref reader, typeof(Dictionary<string, object?>), options);
				default:
					throw new JsonException("Unexpected JSON token.");
			}
		}

		/// <inheritdoc/>
		public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options) =>
			JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), options);
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> implementation that reads a JSON object from the input as a <see cref="Dictionary{TKey, TValue}"/> that maps the key name <c>string</c>s to <c>object?</c> values,
	/// representing the contained JSON values as read by <see cref="ObjectDictionaryValueJsonConverter"/>.
	/// </summary>
	public class ObjectDictionaryJsonConverter : JsonConverter<Dictionary<string, object?>> {
		private static ObjectDictionaryValueJsonConverter valueConverter = new ObjectDictionaryValueJsonConverter();
		/// <inheritdoc/>
		public override Dictionary<string, object?>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Unexpected JSON token.");
			var dict = new Dictionary<string, object?>();
			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.EndObject) break;
				string key = reader.GetString() ?? throw new JsonException("Couldn't read JSON property name.");
				reader.Read();
				dict.Add(key, valueConverter.Read(ref reader, typeof(object), options));
			}
			if (reader.TokenType == JsonTokenType.EndObject) {
				return dict;
			}
			else {
				throw new JsonException("Unexpected end of JSON object.");
			}
		}

		/// <inheritdoc/>
		public override void Write(Utf8JsonWriter writer, Dictionary<string, object?> value, JsonSerializerOptions options) {
			writer.WriteStartObject();
			foreach (var elem in value) {
				writer.WritePropertyName(elem.Key);
				JsonSerializer.Serialize(writer, elem.Value, options);
			}
			writer.WriteEndObject();
		}
	}
}
