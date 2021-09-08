using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Tests.Dummies {
	public class DummyApplicationRepository : IApplicationRepository {
		private readonly Dictionary<string, ApplicationWithUserProperties> apps = new();
		private int nextPropertyDefinitionId = 1;

		public async Task<ApplicationWithUserProperties> AddApplicationAsync(ApplicationWithUserProperties app) {
			await Task.CompletedTask;
			if (apps.ContainsKey(app.Name)) throw new EntityUniquenessConflictException("Application", "Name");
			if (app.Id == Guid.Empty) app.Id = Guid.NewGuid();
			if (apps.Values.Any(a => a.Id == app.Id)) throw new EntityUniquenessConflictException("Application", "Id");
			assignPropertyDefinitionIds(app);
			apps.Add(app.Name, app);
			return app;
		}

		public async Task<ApplicationWithUserProperties?> GetApplicationByNameAsync(string appName) {
			await Task.CompletedTask;
			if (apps.TryGetValue(appName, out var app)) {
				return app;
			}
			else {
				return null;
			}
		}

		private void assignPropertyDefinitionIds(ApplicationWithUserProperties app) {
			foreach (var propDef in app.UserProperties) {
				if (propDef.Id == 0) propDef.Id = nextPropertyDefinitionId++;
			}
		}
	}
}
