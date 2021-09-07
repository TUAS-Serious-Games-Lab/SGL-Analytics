using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Domain.Entity;
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
			await context.SaveChangesAsync();
			return app;
		}

		public async Task<ApplicationWithUserProperties?> GetApplicationByNameAsync(string appName) {
			return await context.Applications.Include(a => a.UserProperties).Where(a => a.Name == appName).SingleOrDefaultAsync();
		}
	}
}
