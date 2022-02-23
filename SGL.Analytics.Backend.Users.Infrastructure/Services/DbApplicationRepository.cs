﻿using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Infrastructure.Services {
	/// <summary>
	/// Provides a persistent implementation of <see cref="IApplicationRepository"/> using Entity Framework Core to map the objects into a relational database.
	/// </summary>
	public class DbApplicationRepository : IApplicationRepository {
		private UsersContext context;

		/// <summary>
		/// Creates a repository object using the given database context object for data access.
		/// </summary>
		/// <param name="context">The <see cref="DbContext"/> implementation for the database.</param>
		public DbApplicationRepository(UsersContext context) {
			this.context = context;
		}

		/// <inheritdoc/>
		public async Task<ApplicationWithUserProperties> AddApplicationAsync(ApplicationWithUserProperties app, CancellationToken ct = default) {
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
				if (await context.Applications.CountAsync(a => a.Name == app.Name) > 0) {
					throw new EntityUniquenessConflictException("Application", "Name", app.Name, ex);
				}
				else if (await context.Applications.CountAsync(a => a.Id == app.Id) > 0) {
					throw new EntityUniquenessConflictException("Application", "Id", app.Id, ex);
				}
				else throw;
			}
			return app;
		}

		/// <inheritdoc/>
		public async Task<ApplicationWithUserProperties?> GetApplicationByNameAsync(string appName, bool fetchRecipients = false, CancellationToken ct = default) {
			IQueryable<ApplicationWithUserProperties> query = context.Applications.Include(a => a.UserProperties).Where(a => a.Name == appName);
			if (fetchRecipients) {
				query = query.Include(a => a.DataRecipients);
			}
			return await query.SingleOrDefaultAsync(ct);
		}

		/// <inheritdoc/>
		public async Task<IList<ApplicationWithUserProperties>> ListApplicationsAsync(bool fetchRecipients = false, CancellationToken ct = default) {
			IQueryable<ApplicationWithUserProperties> query = context.Applications.Include(a => a.UserProperties);
			if (fetchRecipients) {
				query = query.Include(a => a.DataRecipients);
			}
			return await query.ToListAsync(ct);
		}

		/// <inheritdoc/>
		public async Task<ApplicationWithUserProperties> UpdateApplicationAsync(ApplicationWithUserProperties app, CancellationToken ct = default) {
			Debug.Assert(context.Entry(app).State is EntityState.Modified or EntityState.Unchanged);
			await context.SaveChangesAsync(ct);
			return app;
		}
	}
}
