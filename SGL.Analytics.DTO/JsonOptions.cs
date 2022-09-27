using Microsoft.Extensions.Options;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	public static class JsonOptions {
		public static readonly JsonSerializerOptions RestOptions;
		public static readonly JsonSerializerOptions LogEntryOptions;
		public static readonly JsonSerializerOptions UserPropertiesOptions;
		public static readonly JsonSerializerOptions UserPropertyValuesOptions;
		public static readonly JsonSerializerOptions AppDefinitionOptions;

		static JsonOptions() {
			RestOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
				WriteIndented = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			UserPropertiesOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
				WriteIndented = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			UserPropertiesOptions.Converters.Add(new ObjectDictionaryJsonConverter());
			UserPropertyValuesOptions = new JsonSerializerOptions { Converters = { new ObjectDictionaryValueJsonConverter() } };
			LogEntryOptions = new JsonSerializerOptions(JsonSerializerDefaults.General) {
				WriteIndented = true
			};
			AppDefinitionOptions = new JsonSerializerOptions() {
				WriteIndented = true,
				Converters = { new JsonStringEnumConverter(new IdentityEnumNamingPolicy()) }
			};
		}
		private class IdentityEnumNamingPolicy : JsonNamingPolicy {
			public override string ConvertName(string name) => name;
		}
	}
}
