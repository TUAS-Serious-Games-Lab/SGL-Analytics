using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Utilities {
	/// <summary>
	/// Provides extension methods for working with date and time values in Entity Framework Core.
	/// </summary>
	public static class DbDateTimeExtensions {
		private static DateTime entityToDb(DateTime ev) {
			return ev.Kind switch { DateTimeKind.Utc => ev, DateTimeKind.Local => ev.ToUniversalTime(), _ => DateTime.SpecifyKind(ev, DateTimeKind.Utc) };
		}
		private static DateTime dbToEntity(DateTime db) {
			return db.Kind switch { DateTimeKind.Unspecified => DateTime.SpecifyKind(db, DateTimeKind.Utc), _ => db.ToUniversalTime() };
		}

		/// <summary>
		/// Causes the property with a <see cref="DateTime"/> value to be stored in UTC representation in the database, irrespective of the representation in the mapped object.
		/// When persisting, values with <see cref="DateTimeKind.Utc"/> are kept as-is, values with <see cref="DateTimeKind.Local"/> are converted using <see cref="DateTime.ToUniversalTime"/>,
		/// and values with <see cref="DateTimeKind.Unspecified"/> are assumed to already be in UTC (using <see cref="DateTime.SpecifyKind(DateTime, DateTimeKind)"/>) for lack of a better option.
		/// </summary>
		/// <param name="property">The builder for the property to manipulate.</param>
		/// <returns>A reference to <paramref name="property"/> for chaining.</returns>
		public static PropertyBuilder<DateTime> IsStoredInUtc(this PropertyBuilder<DateTime> property) {
			property.HasConversion(ev => entityToDb(ev), db => dbToEntity(db));
			return property;
		}
	}
}
