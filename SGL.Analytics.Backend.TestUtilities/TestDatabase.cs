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
		private readonly DbConnection connection;
		public DbContextOptions<TContext> ContextOptions { get; set; }

		public TestDatabase() {
			connection = new SqliteConnection("Filename=:memory:");
			connection.Open();
			ContextOptions = new DbContextOptionsBuilder<TContext>().UseSqlite(connection).Options;
			using (var context = Activator.CreateInstance(typeof(TContext), ContextOptions) as TContext ?? throw new InvalidOperationException()) {
				context.Database.EnsureCreated();
			}
		}

		public void Dispose() => connection.Dispose();
	}
}
