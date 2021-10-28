using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.TestUtilities {
	/// <summary>
	/// A test utility class that provides a connection and DB context options for an in-memory Sqlite database for a given DbContext type.
	/// </summary>
	/// <typeparam name="TContext">The DbContext type to use for the database schema.</typeparam>
	public class TestDatabase<TContext> : IDisposable where TContext : DbContext {
		/// <summary>
		/// The database connection to the in-memory database.
		/// It can be passed to <see cref="SqliteDbContextOptionsBuilderExtensions.UseSqlite{TContext}(DbContextOptionsBuilder{TContext}, DbConnection, Action{Microsoft.EntityFrameworkCore.Infrastructure.SqliteDbContextOptionsBuilder})"/> to use it.
		/// </summary>
		public DbConnection Connection { get; }
		/// <summary>
		/// DB context options containing <see cref="Connection"/>.
		/// Can be passed to the database context constructor to use it.
		/// </summary>
		public DbContextOptions<TContext> ContextOptions { get; }

		/// <summary>
		/// Creates an sqlite in-memory database and applies the schema specified by the database context type to it.
		/// </summary>
		public TestDatabase() {
			Connection = new SqliteConnection("Filename=:memory:");
			Connection.Open();
			ContextOptions = new DbContextOptionsBuilder<TContext>().UseSqlite(Connection).Options;
			using (var context = Activator.CreateInstance(typeof(TContext), ContextOptions) as TContext ?? throw new InvalidOperationException()) {
				context.Database.EnsureCreated();
			}
		}

		/// <summary>
		/// Disposes the connection object, also closing and destroying the in-memory database.
		/// </summary>
		public void Dispose() => Connection.Dispose();
	}
}
