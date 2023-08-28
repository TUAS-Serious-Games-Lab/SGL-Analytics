namespace SGL.Analytics.ExporterClient {
	/// <summary>
	/// Specifies the builder pattern interface for queries on property values to select the property type and
	/// to select the appropriate detailed interface for queries on that property type.
	/// </summary>
	public interface IUserRegistrationPropertyQuery {
		/// <summary>
		/// Requires that the value is of integer type and returns a query interface for integers.
		/// </summary>
		/// <returns>A query interface that allows value comparisons on integer values.</returns>
		IUserRegistrationPropertyComparisonQuery<long> IsInteger();
		/// <summary>
		/// Requires that the value is of floating point type and returns a query interface for floating point values.
		/// </summary>
		/// <returns>A query interface that allows value comparisons on floating point values.</returns>
		IUserRegistrationPropertyComparisonQuery<double> IsFloatingPoint();
		/// <summary>
		/// Requires that the value is of string type and returns a query interface for strings.
		/// </summary>
		/// <returns>A query interface that allows lexicographic value comparisons, string-specific conditions on string values .</returns>
		IUserRegistrationPropertyStringQuery IsString();
		/// <summary>
		/// Requires that the value is of date and time type and returns a query interface for date and time values.
		/// </summary>
		/// <returns>A query interface that allows value comparisons on date and time values.</returns>
		IUserRegistrationPropertyComparisonQuery<DateTime> IsDateTime();
		/// <summary>
		/// Requires that the value is of GUID type and returns a query interface for GUID values.
		/// </summary>
		/// <returns>A query interface that allows value equality checks on GUID values.</returns>
		IUserRegistrationPropertyEqualityQuery<Guid> IsGuid();
	}
	/// <summary>
	/// Specifies the builder pattern interface for queries on property values that require equality comparison to a given value.
	/// </summary>
	public interface IUserRegistrationPropertyEqualityQuery<T> : IUserRegistrationPropertyQuery {
		/// <summary>
		/// Requires that the value of the user property is equal to the given <paramref name="value"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationPropertyEqualityQuery<T> IsEqualTo(T value);
	}
	/// <summary>
	/// Specifies the builder pattern interface for queries on property values that require relational comparison to a given value.
	/// Extends <see cref="IUserRegistrationPropertyEqualityQuery{T}"/> to also support equality comparison.
	/// </summary>
	public interface IUserRegistrationPropertyComparisonQuery<T> : IUserRegistrationPropertyEqualityQuery<T> {
		/// <summary>
		/// Requires that the value of the user property is less than the given <paramref name="value"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationPropertyComparisonQuery<T> IsLessThan(T value);
		/// <summary>
		/// Requires that the value of the user property is less than or equal to the given <paramref name="value"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationPropertyComparisonQuery<T> IsLessOrEqualTo(T value);
		/// <summary>
		/// Requires that the value of the user property is greater than the given <paramref name="value"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationPropertyComparisonQuery<T> IsGreaterThan(T value);
		/// <summary>
		/// Requires that the value of the user property is greater than or equal to the given <paramref name="value"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationPropertyComparisonQuery<T> IsGreaterOrEqualTo(T value);
	}
	/// <summary>
	/// Specifies the builder pattern interface for queries on property values that require string matching operations to a given value.
	/// Extends <see cref="IUserRegistrationPropertyComparisonQuery{String}"/> to include lexicographic comparison.
	/// </summary>
	public interface IUserRegistrationPropertyStringQuery : IUserRegistrationPropertyComparisonQuery<string> {
		/// <summary>
		/// Requires that the string value of the user property contains the given <paramref name="value"/> as a substring.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationPropertyStringQuery Contains(string value, StringComparison comp);
		/// <summary>
		/// Requires that the string value of the user property starts with the given <paramref name="value"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationPropertyStringQuery StartsWith(string value, StringComparison comp);
		/// <summary>
		/// Requires that the string value of the user property ends with the given <paramref name="value"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationPropertyStringQuery EndsWith(string value, StringComparison comp);
		/// <summary>
		/// Requires that the string value of the user property is equal to the given <paramref name="value"/> according to the comparison mode given in <paramref name="comp"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationPropertyStringQuery IsEqualTo(string value, StringComparison comp);
	}

	/// <summary>
	/// Specifies the builder pattern interface for combining query criteria for user registrations
	/// in <see cref="SglAnalyticsExporter.GetDecryptedUserRegistrationsAsync(Func{IUserRegistrationQuery, IUserRegistrationQuery}, CancellationToken)"/> and
	/// <see cref="SglAnalyticsExporter.GetDecryptedUserRegistrationsAsync(IUserRegistrationSink, Func{IUserRegistrationQuery, IUserRegistrationQuery}, CancellationToken)"/>.
	/// </summary>
	public interface IUserRegistrationQuery {
		/// <summary>
		/// Only return user registrations that have the property named by <paramref name="key"/> present and for which the value
		/// of that property satisfies the conditions built by <paramref name="conditions"/>.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationQuery HasUnencryptedProperty(string key, Func<IUserRegistrationPropertyQuery, IUserRegistrationPropertyQuery> conditions);
		/// <summary>
		/// Only return user registrations that don't have the property named in <paramref name="key"/> present.
		/// </summary>
		/// <returns>A reference to the this object for chaining.</returns>
		IUserRegistrationQuery DoesntHaveUnencryptedProperty(string key);
	}
}
