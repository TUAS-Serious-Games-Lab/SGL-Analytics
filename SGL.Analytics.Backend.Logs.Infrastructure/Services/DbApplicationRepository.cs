using Microsoft.EntityFrameworkCore;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {
	public class DbApplicationRepository : IApplicationRepository, IDisposable, IAsyncDisposable {
		private LogsContext context;

		public DbApplicationRepository(LogsContext context) {
			this.context = context;
		}

		public void Dispose() => context.Dispose();
		public ValueTask DisposeAsync() => context.DisposeAsync();

		public Task<Domain.Entity.Application?> GetApplicationByNameAsync(string appName) {
			return context.Applications.Where(a => a.Name == appName).SingleOrDefaultAsync<Domain.Entity.Application?>();
		}

		public async Task<Domain.Entity.Application> AddApplicationAsync(Domain.Entity.Application app) {
			context.Applications.Add(app);
			await context.SaveChangesAsync();
			return app;
		}
	}
}
