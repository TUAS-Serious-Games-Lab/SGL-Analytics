using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {

	public delegate object PlaceholderValueGetter<T>(T obj);

	public interface INamedPlaceholderFormatterFactoryBuilder<T> {
		void AddPlaceholder(string name, PlaceholderValueGetter<T> getter);
	}

	public class NamedPlaceholderFormatException : Exception {
		public NamedPlaceholderFormatException(string message) : base($"Format string error: {message}") { }
	}

	public class NamedPlaceholderFormatterFactory<T> {
		private Dictionary<string, PlaceholderValueGetter<T>> definedPlaceholders = new();

		private static void pushLiteral(List<IFormattingComponent<T>> comps, string literal) {
			if (comps.LastOrDefault() is LiteralComponent<T> comp) {
				comp.LiteralText += literal;
			}
			else {
				comps.Add(new LiteralComponent<T>(literal));
			}
		}

		private void pushPlaceholder(List<IFormattingComponent<T>> comps, string name, string formatting = "") {
			if (definedPlaceholders.TryGetValue(name, out var valueGetter)) {
				comps.Add(new PlaceholderComponent<T>("{0" + formatting + "}", name, valueGetter));
			}
			else {
				throw new NamedPlaceholderFormatException($"Undefined placeholder '{name}'.");
			}
		}

		private List<IFormattingComponent<T>> parseFormatString(string format) {
			var components = new List<IFormattingComponent<T>>();
			int start = 0;
			for (int cur = 0; cur < format.Length; ++cur) {
				switch (format[cur]) {
					case '{' when (cur + 1 < format.Length && format[cur + 1] == '{'):
						++cur;
						pushLiteral(components, format.Substring(start, cur - start));
						start = cur + 1;
						break;
					case '{':
						// Found a placeholder, push literal since last placeholder or start:
						if (start != cur) pushLiteral(components, format.Substring(start, cur - start));
						// Remember where placeholder content starts (char after the '{'):
						start = cur + 1;
						// Then find name:
						for (; cur < format.Length && format[cur] is not (',' or ':' or '}'); ++cur) ;
						if (cur == format.Length) throw new InvalidOperationException("Unexpected end inside placeholder.");
						if (format[cur] == '}') {
							pushPlaceholder(components, format.Substring(start, cur - start));
						}
						else {
							int separator = cur;
							for (; cur < format.Length && format[cur] != '}'; ++cur) ;
							if (cur == format.Length) throw new InvalidOperationException("Unexpected end inside placeholder.");
							pushPlaceholder(components, format.Substring(start, separator - start), format.Substring(separator, cur - (separator)));
						}
						start = cur + 1;
						break;
					case '}' when (cur + 1 < format.Length && format[cur + 1] == '}'):
						++cur;
						pushLiteral(components, format.Substring(start, cur - start));
						start = cur + 1;
						break;
					case '}':
						throw new InvalidOperationException($"Unmatched closing brace at position {cur}.");
					default:
						break;
				}
			}
			if (start != format.Length) pushLiteral(components, format.Substring(start));
			return components;
		}

		private class Builder : INamedPlaceholderFormatterFactoryBuilder<T> {
			private NamedPlaceholderFormatterFactory<T> buildee;

			public Builder(NamedPlaceholderFormatterFactory<T> buildee) {
				this.buildee = buildee;
			}

			public void AddPlaceholder(string name, PlaceholderValueGetter<T> getter) {
				buildee.definedPlaceholders[name] = getter;
			}
		}

		public NamedPlaceholderFormatterFactory(Action<INamedPlaceholderFormatterFactoryBuilder<T>> buildFactory) {
			buildFactory(new Builder(this));
		}

		public NamedPlaceholderFormatter<T> Create(string formatString) {
			return new NamedPlaceholderFormatter<T>(parseFormatString(formatString));
		}
	}

	internal interface IFormattingComponent<T> {
		StringBuilder AppendFormattedTo(StringBuilder sb, T objectToFormat);
	}

	internal class LiteralComponent<T> : IFormattingComponent<T> {
		internal string LiteralText { get; set; }

		public LiteralComponent(string literalText) {
			LiteralText = literalText;
		}

		public StringBuilder AppendFormattedTo(StringBuilder sb, T objectToFormat) {
			sb.Append(LiteralText);
			return sb;
		}
	}

	internal class PlaceholderComponent<T> : IFormattingComponent<T> {
		internal string FormatString { get; }
		internal string PlaceholderName { get; }
		internal PlaceholderValueGetter<T> ValueGetter { get; }

		public PlaceholderComponent(string formatString, string placeholderName, PlaceholderValueGetter<T> valueGetter) {
			FormatString = formatString;
			PlaceholderName = placeholderName;
			ValueGetter = valueGetter;
		}

		public StringBuilder AppendFormattedTo(StringBuilder sb, T objectToFormat) {
			sb.AppendFormat(FormatString, ValueGetter(objectToFormat));
			return sb;
		}
	}

	public class NamedPlaceholderFormatter<T> {
		private List<IFormattingComponent<T>> components;

		internal NamedPlaceholderFormatter(List<IFormattingComponent<T>> components) {
			this.components = components;
		}

		public StringBuilder AppendFormattedTo(StringBuilder sb, T objectToFormat) {
			foreach (var comp in components) {
				comp.AppendFormattedTo(sb, objectToFormat);
			}
			return sb;
		}

		public bool UsesPlaceholder(string name) => components.OfType<PlaceholderComponent<T>>().Any(c => c.PlaceholderName == name);
	}
}
