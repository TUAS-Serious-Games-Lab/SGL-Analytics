using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {
	/// <summary>
	/// Provides a persistent implementation of <see cref="IApplicationRepository"/> using Entity Framework Core to map the objects into a relational database.
	/// </summary>
	public class DbApplicationRepository : IApplicationRepository {
		private LogsContext context;

		/// <summary>
		/// Creates a repository object using the given database context object for data access.
		/// </summary>
		/// <param name="context">The <see cref="DbContext"/> implementation for the database.</param>
		public DbApplicationRepository(LogsContext context) {
			this.context = context;
		}

		/// <inheritdoc/>
		public Task<Domain.Entity.Application?> GetApplicationByNameAsync(string appName, CancellationToken ct = default) {
			return context.Applications.Where(a => a.Name == appName).SingleOrDefaultAsync<Domain.Entity.Application?>(ct);
		}

		/// <inheritdoc/>
		public async Task<Domain.Entity.Application> AddApplicationAsync(Domain.Entity.Application app, CancellationToken ct = default) {
			context.Applications.Add(app);
			try {
				await context.SaveChangesAsync(ct);
			}
			catch (DbUpdateConcurrencyException ex) {
				throw new ConcurrencyConflictException(ex);
			}
			catch (DbUpdateException ex) {
				// Should happen rarely and unfortunately, at the time of writing, there is no portable way (between databases) of further classifying the error.
				// To check if ex is a unique constraint violation, we would need to inspect its inner exception and switch over exception types for all supported providers and their internal error classifications.
				// To avoid this coupling, rather pay the perf cost of querrying again in this rare case.
				if (await context.Applications.CountAsync(a => a.Name == app.Name, ct) > 0) {
					throw new EntityUniquenessConflictException("Application", "Name", app.Name, ex);
				}
				else if (await context.Applications.CountAsync(a => a.Id == app.Id, ct) > 0) {
					throw new EntityUniquenessConflictException("Application", "Id", app.Id, ex);
				}
				else throw;
			}
			return app;
		}

		/// <inheritdoc/>
		public async Task<Domain.Entity.Application> UpdateApplicationAsync(Domain.Entity.Application app, CancellationToken ct = default) {
			Debug.Assert(context.Entry(app).State is EntityState.Modified or EntityState.Unchanged);
			await context.SaveChangesAsync(ct);
			return app;
		}
	}
}
