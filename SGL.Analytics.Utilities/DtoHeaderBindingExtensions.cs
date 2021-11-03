using System;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;

namespace SGL.Analytics.Utilities {

	/// <summary>
	/// Provides the <see cref="MapDtoProperties(HttpHeaders, object)"/> extension method.
	/// </summary>
	public static class DtoHeaderBindingExtensions {
		private static Func<object, string?>? getTypeMapping(PropertyInfo prop) {
			var type = prop.PropertyType;
			switch (type) {
				case Type _ when type == typeof(DateTime):
					return source => (prop.GetValue(source) as DateTime?)?.ToString("O");
				case Type _ when type == typeof(string):
				case Type _ when type == typeof(Guid):
				case { IsEnum: true }:
					return source => prop.GetValue(source)?.ToString();
				case { IsPrimitive: true }:
					return source => String.Format(CultureInfo.InvariantCulture, "{0}", prop.GetValue(source));
				default:
					return null;
			}
		}
		private static string getNameMapping(PropertyInfo prop) {
			// TODO check for attributes that can override the name
			return prop.Name;
		}
		/// <summary>
		/// Adds HTTP headers to the given <c>headers</c> object that represent the public non-static properties of the given <c>source</c> data transfer object.
		/// </summary>
		/// <param name="headers">The header collection to which the entries are to be added.</param>
		/// <param name="source">The DTO to take the header names and values from.</param>
		/// <remarks>
		/// This mapping is usefull to pass parameters (contained in an object) to a web API where the paramters don't fit into route or query parameters semantically and the request body can't be used because it is already otherwise occupied, e.g. for a file upload.
		/// </remarks>
		public static void MapDtoProperties(this HttpHeaders headers, object source) {
			var props = source.GetType().GetProperties();
			var mappings = (from prop in props
							select new { NameMapping = getNameMapping(prop), TypeMapping = getTypeMapping(prop), OriginalProperty = prop }).ToList();
			var unmapped = mappings.Where(m => m.TypeMapping is null).ToList();
			if (unmapped.Count > 0) {
				throw new ArgumentException("Not all properties of the given object could be mapped. The following properties are of types that could not be mapped: " + String.Join(", ", unmapped.Select(um => $"{um.OriginalProperty.Name} of type {um.OriginalProperty.PropertyType.FullName}")), nameof(source));
			}
			foreach (var mapping in mappings) {
				headers.Add(mapping.NameMapping, mapping.TypeMapping(source));
			}
		}
	}
}
