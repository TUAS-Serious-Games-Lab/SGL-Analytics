using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Application.Tests.Dummies {
	public class DummyApplicationRepository : IApplicationRepository {
		private readonly Dictionary<string, ApplicationWithUserProperties> apps = new();
		private int nextPropertyDefinitionId = 1;

		public async Task<ApplicationWithUserProperties> AddApplicationAsync(ApplicationWithUserProperties app, CancellationToken ct = default) {
			await Task.CompletedTask;
			if (apps.ContainsKey(app.Name)) throw new EntityUniquenessConflictException("Application", "Name", app.Name);
			if (app.Id == Guid.Empty) app.Id = Guid.NewGuid();
			if (apps.Values.Any(a => a.Id == app.Id)) throw new EntityUniquenessConflictException("Application", "Id", app.Id);
			assignPropertyDefinitionIds(app);
			ct.ThrowIfCancellationRequested();
			apps.Add(app.Name, app);
			return app;
		}

		public async Task<ApplicationWithUserProperties?> GetApplicationByNameAsync(string appName, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (apps.TryGetValue(appName, out var app)) {
				return app;
			}
			else {
				return null;
			}
		}

		public async Task<ApplicationWithUserProperties> UpdateApplicationAsync(ApplicationWithUserProperties app, CancellationToken ct = default) {
			await Task.CompletedTask;
			Debug.Assert(apps.ContainsKey(app.Name));
			assignPropertyDefinitionIds(app);
			ct.ThrowIfCancellationRequested();
			apps[app.Name] = app;
			return app;
		}

		private void assignPropertyDefinitionIds(ApplicationWithUserProperties app) {
			foreach (var propDef in app.UserProperties) {
				if (propDef.Id == 0) propDef.Id = nextPropertyDefinitionId++;
			}
		}
	}
}
