using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {
	public class DbApplicationRepository : IApplicationRepository {
		private LogsContext context;

		public DbApplicationRepository(LogsContext context) {
			this.context = context;
		}

		public Task<Domain.Entity.Application?> GetApplicationByNameAsync(string appName) {
			return context.Applications.Where(a => a.Name == appName).SingleOrDefaultAsync<Domain.Entity.Application?>();
		}

		public async Task<Domain.Entity.Application> AddApplicationAsync(Domain.Entity.Application app) {
			context.Applications.Add(app);
			try {
				await context.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException ex) {
				throw new ConcurrencyConflictException(ex);
			}
			catch (DbUpdateException ex) {
				// Should happen rarely and unfortunately, at the time of writing, there is no portable way (between databases) of further classifying the error.
				// To check if ex is a unique constraint violation, we would need to inspect its inner exception and switch over exception types for all supported providers and their internal error classifications.
				// To avoid this coupling, rather pay the perf cost of querrying again in this rare case.
				if (context.Applications.Count(a => a.Name == app.Name) > 0) {
					throw new EntityUniquenessConflictException("Application", "Name", app.Name, ex);
				}
				else if (context.Applications.Count(a => a.Id == app.Id) > 0) {
					throw new EntityUniquenessConflictException("Application", "Id", app.Id, ex);
				}
				else throw;
			}
			return app;
		}
	}
}
