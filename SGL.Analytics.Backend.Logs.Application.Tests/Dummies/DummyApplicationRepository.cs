using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Tests.Dummies {
	public class DummyApplicationRepository : IApplicationRepository {
		private readonly Dictionary<string, Domain.Entity.Application> apps = new();

		public async Task<Domain.Entity.Application> AddApplicationAsync(Domain.Entity.Application app, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (apps.ContainsKey(app.Name)) throw new EntityUniquenessConflictException("Application", "Name", app.Name);
			if (app.Id == Guid.Empty) app.Id = Guid.NewGuid();
			if (apps.Values.Any(a => a.Id == app.Id)) throw new EntityUniquenessConflictException("Application", "Id", app.Id);
			ct.ThrowIfCancellationRequested();
			apps.Add(app.Name, app);
			return app;
		}

		public async Task<Domain.Entity.Application?> GetApplicationByNameAsync(string appName, CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			if (apps.TryGetValue(appName, out var app)) {
				return app;
			}
			else {
				return null;
			}
		}

		public async Task<IList<Domain.Entity.Application>> ListApplicationsAsync(CancellationToken ct = default) {
			await Task.CompletedTask;
			ct.ThrowIfCancellationRequested();
			return apps.Values.ToList();
		}

		public async Task<Domain.Entity.Application> UpdateApplicationAsync(Domain.Entity.Application app, CancellationToken ct = default) {
			await Task.CompletedTask;
			Debug.Assert(apps.ContainsKey(app.Name));
			ct.ThrowIfCancellationRequested();
			apps[app.Name] = app;
			return app;
		}
	}
}
