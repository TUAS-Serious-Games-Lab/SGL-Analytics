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
			if (apps.ContainsKey(app.Name)) throw new InvalidOperationException($"An application with the given name '{app.Name}' is already present.");
			app.Id = Guid.NewGuid();
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
