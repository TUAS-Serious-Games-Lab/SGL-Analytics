using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Tests.Dummies {
	public class DummyApplicationRepository : IApplicationRepository {
		private readonly Dictionary<string, Domain.Entity.Application> apps = new();

		public async Task<Domain.Entity.Application> AddApplicationAsync(Domain.Entity.Application app) {
			await Task.CompletedTask;
			if (apps.ContainsKey(app.Name)) throw new EntityUniquenessConflictException("Application", "Name");
			if (app.Id == Guid.Empty) app.Id = Guid.NewGuid();
			if (apps.Values.Any(a => a.Id == app.Id)) throw new EntityUniquenessConflictException("Application", "Id");
			apps.Add(app.Name, app);
			return app;
		}

		public async Task<Domain.Entity.Application?> GetApplicationByNameAsync(string appName) {
			await Task.CompletedTask;
			if (apps.TryGetValue(appName, out var app)) {
				return app;
			}
			else {
				return null;
			}
		}
	}
}
