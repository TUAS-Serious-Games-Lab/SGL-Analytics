using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.ExporterClient {
	internal class UserRegistrationQuery : IUserRegistrationQuery {
		private Func<IEnumerable<UserMetadataDTO>, IEnumerable<UserMetadataDTO>> queryApplicator;

		internal UserRegistrationQuery() : this(e => e) { }

		private UserRegistrationQuery(Func<IEnumerable<UserMetadataDTO>, IEnumerable<UserMetadataDTO>> queryApplicator) {
			this.queryApplicator = queryApplicator;
		}

		private IUserRegistrationQuery appendToQuery(Func<IEnumerable<UserMetadataDTO>, IEnumerable<UserMetadataDTO>> current) {
			var prev = queryApplicator;
			return new UserRegistrationQuery(queryApplicator: q => current(prev(q)));
		}

		public IUserRegistrationQuery DoesntHaveUnencryptedProperty(string key) => appendToQuery(q => q.Where(udto => !udto.StudySpecificProperties.ContainsKey(key)));

		public IUserRegistrationQuery HasUnencryptedProperty(string key, Func<IUserRegistrationPropertyQuery, IUserRegistrationPropertyQuery> conditions) {
			var propQuery = (IUserRegistrationPropertyPredicateSource)conditions(new UserRegistrationPropertyQuery());
			var predicate = propQuery.GetPredicate();
			return appendToQuery(q => q.Where(udto => udto.StudySpecificProperties.TryGetValue(key, out var value) && predicate(value)));
		}

		internal IEnumerable<UserMetadataDTO> ApplyTo(IEnumerable<UserMetadataDTO> udtos) {
			return queryApplicator(udtos);
		}
	}

	internal interface IUserRegistrationPropertyPredicateSource {
		Func<object?, bool> GetPredicate();
	}

	internal class UserRegistrationPropertyQuery : IUserRegistrationPropertyQuery, IUserRegistrationPropertyPredicateSource {
		private Func<object?, bool> query;

		internal UserRegistrationPropertyQuery() : this(_ => true) { }
		private UserRegistrationPropertyQuery(Func<object?, bool> query) {
			this.query = query;
		}

		private Func<object?, bool> appendTypeCheckToQuery<T>(Func<object?, bool> check) {
			var prev = query;
			return obj => prev(obj) && check(obj);
		}

		public IUserRegistrationPropertyComparisonQuery<long> IsInteger() => new UserRegistrationPropertyComparisonQuery<long>(appendTypeCheckToQuery<long>(obj => obj is int or long));
		public IUserRegistrationPropertyComparisonQuery<double> IsFloatingPoint() => new UserRegistrationPropertyComparisonQuery<double>(appendTypeCheckToQuery<double>(obj => obj is double));
		public IUserRegistrationPropertyComparisonQuery<DateTime> IsDateTime() => new UserRegistrationPropertyComparisonQuery<DateTime>(appendTypeCheckToQuery<DateTime>(obj => obj is DateTime));
		public IUserRegistrationPropertyStringQuery IsString() => new UserRegistrationPropertyStringQuery(appendTypeCheckToQuery<string>(obj => obj is string));
		public IUserRegistrationPropertyEqualityQuery<Guid> IsGuid() => new UserRegistrationPropertyEqualityQuery<Guid>(appendTypeCheckToQuery<Guid>(obj => obj is Guid));

		public Func<object?, bool> GetPredicate() => query;
	}

	internal class UserRegistrationPropertyEqualityQuery<T> : IUserRegistrationPropertyEqualityQuery<T>, IUserRegistrationPropertyPredicateSource where T : notnull {
		protected Func<object?, bool> query;

		internal UserRegistrationPropertyEqualityQuery(Func<object?, bool> query) {
			this.query = query;
		}

		private IUserRegistrationPropertyEqualityQuery<T> appendToQuery(Func<T, bool> current) {
			var prev = query;
			return new UserRegistrationPropertyEqualityQuery<T>(obj => prev(obj) && current((T)obj!));
		}
		private IUserRegistrationPropertyEqualityQuery<T2> typeCheckEq<T2>() where T2 : notnull {
			if (typeof(T) == typeof(T2)) {
				return new UserRegistrationPropertyEqualityQuery<T2>(query);
			}
			else {
				throw new InvalidOperationException("Query already requires a different Type");
			}
		}
		private IUserRegistrationPropertyComparisonQuery<T2> typeCheckComp<T2>() where T2 : IComparable<T2> {
			if (typeof(T) == typeof(T2)) {
				return new UserRegistrationPropertyComparisonQuery<T2>(query);
			}
			else {
				throw new InvalidOperationException("Query already requires a different Type");
			}
		}
		private IUserRegistrationPropertyStringQuery typeCheckStr() {
			if (typeof(T) == typeof(string)) {
				return new UserRegistrationPropertyStringQuery(query);
			}
			else {
				throw new InvalidOperationException("Query already requires a different Type");
			}
		}

		public IUserRegistrationPropertyComparisonQuery<long> IsInteger() => typeCheckComp<long>();
		public IUserRegistrationPropertyComparisonQuery<double> IsFloatingPoint() => typeCheckComp<double>();
		public IUserRegistrationPropertyComparisonQuery<DateTime> IsDateTime() => typeCheckComp<DateTime>();
		public IUserRegistrationPropertyEqualityQuery<Guid> IsGuid() => typeCheckEq<Guid>();
		public IUserRegistrationPropertyStringQuery IsString() => typeCheckStr();

		public IUserRegistrationPropertyEqualityQuery<T> IsEqualTo(T value) => appendToQuery(v => v.Equals(value));

		public Func<object?, bool> GetPredicate() => query;
	}

	internal class UserRegistrationPropertyComparisonQuery<T> : UserRegistrationPropertyEqualityQuery<T>, IUserRegistrationPropertyComparisonQuery<T> where T : notnull, IComparable<T> {
		internal UserRegistrationPropertyComparisonQuery(Func<object?, bool> query) : base(query) { }

		private IUserRegistrationPropertyComparisonQuery<T> appendToQuery(Func<T, bool> current) {
			var prev = query;
			return new UserRegistrationPropertyComparisonQuery<T>(obj => prev(obj) && current((T)obj!));
		}

		public IUserRegistrationPropertyComparisonQuery<T> IsLessThan(T value) => appendToQuery(v => v.CompareTo(value) < 0);
		public IUserRegistrationPropertyComparisonQuery<T> IsLessOrEqualTo(T value) => appendToQuery(v => v.CompareTo(value) <= 0);
		public IUserRegistrationPropertyComparisonQuery<T> IsGreaterThan(T value) => appendToQuery(v => v.CompareTo(value) > 0);
		public IUserRegistrationPropertyComparisonQuery<T> IsGreaterOrEqualTo(T value) => appendToQuery(v => v.CompareTo(value) >= 0);
	}
	internal class UserRegistrationPropertyStringQuery : UserRegistrationPropertyComparisonQuery<string>, IUserRegistrationPropertyStringQuery {
		internal UserRegistrationPropertyStringQuery(Func<object?, bool> query) : base(query) { }

		private IUserRegistrationPropertyStringQuery appendToQuery(Func<string, bool> current) {
			var prev = query;
			return new UserRegistrationPropertyStringQuery(obj => prev(obj) && current((string)obj!));
		}

		public IUserRegistrationPropertyStringQuery IsEqualTo(string value, StringComparison comp) => appendToQuery(s => s.Equals(value, comp));
		public IUserRegistrationPropertyStringQuery StartsWith(string value, StringComparison comp) => appendToQuery(s => s.StartsWith(value, comp));
		public IUserRegistrationPropertyStringQuery EndsWith(string value, StringComparison comp) => appendToQuery(s => s.EndsWith(value, comp));
		public IUserRegistrationPropertyStringQuery Contains(string value, StringComparison comp) => appendToQuery(s => s.Contains(value, comp));
	}
}
