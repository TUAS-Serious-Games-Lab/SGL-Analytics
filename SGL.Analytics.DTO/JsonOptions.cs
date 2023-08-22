using Microsoft.Extensions.Options;
using SGL.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.Analytics.DTO {
	/// <summary>
	/// Encapsulates the JSON serialization options to be used when (de)serializing data in SGL.Analytics.
	/// </summary>
	public static class JsonOptions {
		/// <summary>
		/// The JSON options to use for the REST interface.
		/// </summary>
		public static readonly JsonSerializerOptions RestOptions;
		/// <summary>
		/// The JSON options to use for the log entries.
		/// </summary>
		public static readonly JsonSerializerOptions LogEntryOptions;
		/// <summary>
		/// The JSON options to use for user registration properties.
		/// </summary>
		public static readonly JsonSerializerOptions UserPropertiesOptions;
		/// <summary>
		/// The JSON options to use for nested object values in user registration properties.
		/// </summary>
		public static readonly JsonSerializerOptions UserPropertyValuesOptions;
		/// <summary>
		/// The JSON options to use for app registration description files.
		/// </summary>
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
