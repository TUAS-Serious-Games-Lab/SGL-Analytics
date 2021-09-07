using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Infrastructure.Services {
	public class DbApplicationRepository : IApplicationRepository {
		private UsersContext context;

		public DbApplicationRepository(UsersContext context) {
			this.context = context;
		}

		public async Task<ApplicationWithUserProperties> AddApplicationAsync(ApplicationWithUserProperties app) {
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
					throw new EntityUniquenessConflictException("Application", "Name", ex);
				}
				else if (context.Applications.Count(a => a.Id == app.Id) > 0) {
					throw new EntityUniquenessConflictException("Application", "Id", ex);
				}
				else throw;
			}
			return app;
		}

		public async Task<ApplicationWithUserProperties?> GetApplicationByNameAsync(string appName) {
			return await context.Applications.Include(a => a.UserProperties).Where(a => a.Name == appName).SingleOrDefaultAsync();
		}
	}
}
