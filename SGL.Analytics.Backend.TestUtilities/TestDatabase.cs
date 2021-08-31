using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.TestUtilities {
	public class TestDatabase<TContext> : IDisposable where TContext : DbContext {
		public DbConnection Connection { get; init; }
		public DbContextOptions<TContext> ContextOptions { get; init; }

		public TestDatabase() {
			Connection = new SqliteConnection("Filename=:memory:");
			Connection.Open();
			ContextOptions = new DbContextOptionsBuilder<TContext>().UseSqlite(Connection).Options;
			using (var context = Activator.CreateInstance(typeof(TContext), ContextOptions) as TContext ?? throw new InvalidOperationException()) {
				context.Database.EnsureCreated();
			}
		}

		public void Dispose() => Connection.Dispose();
	}
}
