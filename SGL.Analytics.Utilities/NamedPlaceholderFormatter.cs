using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {

	/// <summary>
	/// A delegate, used to retrieve the value of the placeholder defined together with the delegate in <see cref="INamedPlaceholderFormatterFactoryBuilder{T}.AddPlaceholder(string, PlaceholderValueGetter{T})"/> for the given formatted object.
	/// </summary>
	/// <typeparam name="T">The type of the object that is being formatted.</typeparam>
	/// <param name="obj">The object that is being formatted.</param>
	/// <returns>The value object, to be used for the placeholder.</returns>
	public delegate object PlaceholderValueGetter<T>(T obj);

	/// <summary>
	/// A delegate, used as the fallback for undefined placeholder names. As it acts as a catch-all, it is additionally passed the name of the undefined placeholder (compared to <see cref="PlaceholderValueGetter{T}"/>) and can lookup a value for it somewhere else.
	/// </summary>
	/// <typeparam name="T">The type of the object that is being formatted.</typeparam>
	/// <param name="placeholderName">The name of the unknown placeholder to resolve.</param>
	/// <param name="obj">The object that is being formatted.</param>
	/// <returns>The value object, to be used for the placeholder.</returns>
	public delegate object FallbackValueGetter<T>(string placeholderName, T obj);

	/// <summary>
	/// A builder interface, used to initialize a <see cref="NamedPlaceholderFormatterFactory{T}"/>, following the builder pattern.
	/// </summary>
	/// <typeparam name="T">The type of the objects for which the factory should create formatters.</typeparam>
	public interface INamedPlaceholderFormatterFactoryBuilder<T> {
		/// <summary>
		/// Adds a placeholder definition with the given name and getter delegate for the formatters created by the factory.
		/// </summary>
		/// <param name="name">The placeholder name, that can be used in the format strings.</param>
		/// <param name="getter">The delegate used to obtain the value of this placeholder for a given object being formatted.</param>
		void AddPlaceholder(string name, PlaceholderValueGetter<T> getter);
		/// <summary>
		/// Sets the fallback getter, that is used to retrieve values for unknown placeholder names.
		/// If this is not set at build time, an exception is thrown for unknown placeholders encountered during the format string parsing.
		/// </summary>
		/// <param name="getter">The delegate to perform the lookup logic.</param>
		void SetFallbackValueGetter(FallbackValueGetter<T> getter);
	}

	/// <summary>
	/// Indicates an error when <see cref="NamedPlaceholderFormatterFactory{T}"/> parses a format string.
	/// </summary>
	public class NamedPlaceholderFormatException : Exception {
		/// <summary>
		/// Constructs an exception object with the given detail message.
		/// </summary>
		/// <param name="message">A message explaining the kind of format string error.</param>
		public NamedPlaceholderFormatException(string message) : base($"Format string error: {message}") { }
	}

	/// <summary>
	/// Creates <see cref="NamedPlaceholderFormatter{T}"/>s for formatting objects of a given type <c>T</c> using format strings with named placeholders (instead of using index-based placeholders like e.g. <see cref="String.Format(string, object?[])"/> does).
	/// The factory holds the definitions of the placeholders available for the specific type, parses format strings and produces a <see cref="NamedPlaceholderFormatter{T}"/> for each format string passed to <see cref="Create(string)"/>.
	/// The <see cref="NamedPlaceholderFormatter{T}"/>s can then be used to efficiently format objects without having to parse the format string each time.
	/// </summary>
	/// <typeparam name="T">The type of the objects for which the factory should create formatters.</typeparam>
	/// <remarks>
	/// The format strings used by this formatting system use named placeholders in braces, e.g. <c>{SomeProperty}</c>, the values of which are obtained by getter delegates, usually from the object of type <c>T</c> that is formatted.
	/// Optional format specifiers for the placeholder values are supported by passing them on to <see cref="StringBuilder.AppendFormat(string, object?)"/>,
	/// e.g. <c>{SomeDate:yyyy}</c> prints only the year of the <see cref="DateTime"/> value, or <c>{SomeNumber,16}</c> right-aligns the number using spaces to produce a 16 characters wide string.
	/// Static text between the placeholders is also supported.
	/// </remarks>
	/// <example>
	/// <code>
	/// <![CDATA[
	/// public class Person {
	/// 	public string FirstName { get; set; }
	/// 	public string LastName { get; set; }
	/// 	public DateTime DateOfBirth { get; }
	/// 
	/// 	public Person(string firstName, string lastName, DateTime dateOfBirth) {
	/// 		FirstName = firstName;
	/// 		LastName = lastName;
	/// 		DateOfBirth = dateOfBirth;
	/// 	}
	/// }
	/// 
	/// public class FormatterDemo {
	/// 	public static void Main(string[] args) {
	/// 		var factory = new NamedPlaceholderFormatterFactory<Person>(builder => {
	/// 			builder.AddPlaceholder("FirstName", p => p.FirstName);
	/// 			builder.AddPlaceholder("LastName", p => p.LastName);
	/// 			builder.AddPlaceholder("DateOfBirth", p => p.DateOfBirth);
	/// 			builder.AddPlaceholder("Age", p => (int)((DateTime.Now - p.DateOfBirth).TotalDays / 365.242199)); //Approximation for demo
	/// 		});
	/// 		var formatter1 = factory.Create("{LastName}, {FirstName}");
	/// 		var formatter2 = factory.Create("{FirstName} {LastName} Year of Birth: {DateOfBirth:yyyy}");
	/// 		var formatter3 = factory.Create("Hello, my name is {FirstName} and I'm {Age} years old.");
	/// 		var person = new Person("John", "Doe", new DateTime(1990, 3, 14));
	/// 		var sb = new StringBuilder();
	/// 		Console.Out.WriteLine(formatter1.AppendFormattedTo(sb, person));
	/// 		sb.Clear();
	/// 		Console.Out.WriteLine(formatter2.AppendFormattedTo(sb, person));
	/// 		sb.Clear();
	/// 		Console.Out.WriteLine(formatter3.AppendFormattedTo(sb, person));
	/// 	}
	/// }	
	/// ]]>
	/// </code>
	/// </example>
	public class NamedPlaceholderFormatterFactory<T> {
		private Dictionary<string, PlaceholderValueGetter<T>> definedPlaceholders = new();
		private FallbackValueGetter<T>? fallbackValueGetter = null;

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
			else if (fallbackValueGetter != null) {
				comps.Add(new PlaceholderComponent<T>("{0" + formatting + "}", name, o => fallbackValueGetter(name, o)));
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

			public void SetFallbackValueGetter(FallbackValueGetter<T> getter) {
				buildee.fallbackValueGetter = getter;
			}
		}

		/// <summary>
		/// Constructs a factory object and initializes it using the given builder delegate.
		/// </summary>
		/// <param name="buildFactory">A delegate to build the factory by defining the available placeholders.</param>
		public NamedPlaceholderFormatterFactory(Action<INamedPlaceholderFormatterFactoryBuilder<T>> buildFactory) {
			buildFactory(new Builder(this));
		}

		/// <summary>
		/// Parses the given format string into a <see cref="NamedPlaceholderFormatter{T}"/> that can be used to format objects of type <c>T</c>.
		/// </summary>
		/// <param name="formatString">The string specifying how the objects should be formatted.</param>
		/// <returns>A <see cref="NamedPlaceholderFormatter{T}"/> that formats <c>T</c> objects according to the given format string.</returns>
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

	/// <summary>
	/// Provides formatting for a given type of objects according to a format string specification that is baked in when the <see cref="NamedPlaceholderFormatter{T}"/> is created by <see cref="NamedPlaceholderFormatterFactory{T}.Create(string)"/>.
	/// </summary>
	/// <typeparam name="T">The type of the objects this formatter can format.</typeparam>
	public class NamedPlaceholderFormatter<T> {
		private List<IFormattingComponent<T>> components;

		internal NamedPlaceholderFormatter(List<IFormattingComponent<T>> components) {
			this.components = components;
		}

		/// <summary>
		/// Appends the formatting output for <c>objectToFormat</c> to the given <c>StringBuilder</c>.
		/// Instead of returning a string, a <c>StringBuilder</c> is used for improved efficiency.
		/// </summary>
		/// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
		/// <param name="objectToFormat">The object to format.</param>
		/// <returns>A reference to <c>sb</c> to allow chaining.</returns>
		public StringBuilder AppendFormattedTo(StringBuilder sb, T objectToFormat) {
			foreach (var comp in components) {
				comp.AppendFormattedTo(sb, objectToFormat);
			}
			return sb;
		}

		/// <summary>
		/// Indicates whether the format used by this formatter makes use of the given placeholder, i.e. if at least one placeholder with the given name is present in the corresponding format string.
		/// </summary>
		/// <param name="name">The placeholder name to check for.</param>
		/// <returns>true if the given placeholder is used, false otherwise.</returns>
		public bool UsesPlaceholder(string name) => components.OfType<PlaceholderComponent<T>>().Any(c => c.PlaceholderName == name);
	}
}
