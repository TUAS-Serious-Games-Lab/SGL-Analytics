namespace SGL.Analytics.ExporterClient {
	public interface IUserRegistrationPropertyQuery {
		IUserRegistrationPropertyComparisonQuery<long> IsInteger();
		IUserRegistrationPropertyComparisonQuery<double> IsFloatingPoint();
		IUserRegistrationPropertyStringQuery IsString();
		IUserRegistrationPropertyComparisonQuery<DateTime> IsDateTime();
		IUserRegistrationPropertyEqualityQuery<Guid> IsGuid();
	}
	public interface IUserRegistrationPropertyEqualityQuery<T> : IUserRegistrationPropertyQuery {
		IUserRegistrationPropertyEqualityQuery<T> IsEqualTo(T value);
	}
	public interface IUserRegistrationPropertyComparisonQuery<T> : IUserRegistrationPropertyEqualityQuery<T> {
		IUserRegistrationPropertyComparisonQuery<T> IsLessThan(T value);
		IUserRegistrationPropertyComparisonQuery<T> IsLessOrEqualTo(T value);
		IUserRegistrationPropertyComparisonQuery<T> IsGreaterThan(T value);
		IUserRegistrationPropertyComparisonQuery<T> IsGreaterOrEqualTo(T value);
	}
	public interface IUserRegistrationPropertyStringQuery : IUserRegistrationPropertyComparisonQuery<string> {
		IUserRegistrationPropertyStringQuery Contains(string value, StringComparison comp);
		IUserRegistrationPropertyStringQuery StartsWith(string value, StringComparison comp);
		IUserRegistrationPropertyStringQuery EndsWith(string value, StringComparison comp);
		IUserRegistrationPropertyStringQuery IsEqualTo(string value, StringComparison comp);
	}

	public interface IUserRegistrationQuery {
		IUserRegistrationQuery HasUnencryptedProperty(string key, Func<IUserRegistrationPropertyQuery, IUserRegistrationPropertyQuery> conditions);
		IUserRegistrationQuery DoesntHaveUnencryptedProperty(string key);
	}
}
