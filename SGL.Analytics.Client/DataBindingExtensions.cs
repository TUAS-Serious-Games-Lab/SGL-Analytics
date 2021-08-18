using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;

namespace SGL.Analytics.Client {
	public static class DataBindingExtensions {
		private static Func<object, string?>? getTypeMapping(PropertyInfo prop) {
			var type = prop.PropertyType;
			switch (type) {
				case Type _ when type == typeof(DateTime):
					return source => (prop.GetValue(source) as DateTime?)?.ToString("O");
				case Type _ when type == typeof(string):
				case Type _ when type == typeof(Guid):
				case { IsEnum: true }:
				case { IsPrimitive: true }:
					return source => prop.GetValue(source)?.ToString();
				default:
					return null;
			}
		}
		private static string getNameMapping(PropertyInfo prop) {
			// TODO check for attributes that can override the name
			return prop.Name;
		}
		public static void MapObjectProperties(this HttpHeaders headers, object source) {
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
