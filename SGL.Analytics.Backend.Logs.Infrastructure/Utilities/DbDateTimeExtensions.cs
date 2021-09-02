using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Utilities {
	public static class DbDateTimeExtensions {
		private static DateTime entityToDb(DateTime ev) {
			return ev.Kind switch { DateTimeKind.Utc => ev, DateTimeKind.Local => ev.ToUniversalTime(), _ => DateTime.SpecifyKind(ev, DateTimeKind.Utc) };
		}
		private static DateTime dbToEntity(DateTime db) {
			return db.Kind switch { DateTimeKind.Unspecified => DateTime.SpecifyKind(db, DateTimeKind.Utc), _ => db.ToUniversalTime() };
		}

		public static PropertyBuilder<DateTime> IsStoredInUtc(this PropertyBuilder<DateTime> property) {
			property.HasConversion(ev => entityToDb(ev), db => dbToEntity(db));
			return property;
		}
	}
}
