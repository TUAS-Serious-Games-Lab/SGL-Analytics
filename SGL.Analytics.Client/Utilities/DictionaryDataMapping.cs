using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SGL.Analytics.Client.Utilities {
	public class DictionaryDataMapping {
		private readonly static MethodInfo dictToMappingDict =
			typeof(DictionaryDataMapping).GetMethod(nameof(dictToMappingDictionary), BindingFlags.NonPublic | BindingFlags.Static) ??
				throw new MissingMethodException(nameof(DictionaryDataMapping),nameof(dictToMappingDictionary));

		private static object dictToMappingDictionary<K, V>(IDictionary<K, V> dict) {
			return dict.ToDictionary(kvp => objectToDataMappingKey(kvp.Key) ?? "null", kvp => objectToDataMapping(kvp.Value));
		}
		private static bool isDict(Type type, out Type iface) {
			iface = type;
			var ifaces = type.GetInterfaces().Where(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>));
			if (ifaces.Count() == 0) return false;
			iface = ifaces.Single();
			return true;
		}

		private static string objectToDataMappingKey(object? obj) {
			switch (obj) {
				case null:
					return "null";
				case DateTime dt:
					return dt.ToString("O");
				default:
					return obj?.ToString() ?? "null";
			}
		}

		private static object? objectToDataMapping(object? obj) {
			Type type = obj?.GetType() ?? typeof(object);
			switch (obj) {
				case null:
				case string:
				case DateTime:
				case TimeSpan:
				case Guid:
				case Enum:
				case object when type.IsPrimitive:
					return obj;
				case Uri:
					return obj.ToString();
				case IDictionary<string, object?> dict:
					return dict;
				case IList<object?> list:
					return list;
				case object when isDict(type, out var iface):
					return dictToMappingDict.MakeGenericMethod(iface.GenericTypeArguments).Invoke(null, new object[] { obj }) ;
				case IEnumerable<object?> e:
					return e.Select(elem => objectToDataMapping(elem)).ToList();
				case object when type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy).Count() > 0:
					return ToDataMappingDictionary(obj);
				default:
					throw new InvalidOperationException($"Don't know how to map type {type.Name}.");
			}
		}
		public static Dictionary<string, object?> ToDataMappingDictionary(object obj) {
			var type = obj.GetType();
			var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
			return props.ToDictionary(pi => pi.Name, pi => objectToDataMapping(pi.GetValue(obj)));
		}
	}
}
