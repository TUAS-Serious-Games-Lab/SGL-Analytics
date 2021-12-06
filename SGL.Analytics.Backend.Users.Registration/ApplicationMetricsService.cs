﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Utilities.Backend;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Registration {
	/// <summary>
	/// A background service that periodically updates application-level metrics.
	/// </summary>
	public class ApplicationMetricsService : ApplicationMetricsServiceBase {
		private readonly IUserRepository userRepo;
		private readonly IApplicationRepository appRepo;
		private readonly IMetricsManager metrics;

		/// <summary>
		/// Instantiates the service, injecting the given dependencies.
		/// </summary>
		public ApplicationMetricsService(IOptions<ApplicationMetricsServiceOptions> options, ILogger<ApplicationMetricsService> logger, IUserRepository userRepo,
			IApplicationRepository appRepo, IMetricsManager metrics) : base(options, logger) {
			this.userRepo = userRepo;
			this.appRepo = appRepo;
			this.metrics = metrics;
		}

		/// <summary>
		/// Asynchronously obtains the current metrics values and updates them in the injected metrics manager.
		/// It also calls <see cref="IMetricsManager.EnsureMetricsExist(string)"/> for all registered apps.
		/// </summary>
		protected async override Task UpdateMetrics(CancellationToken ct) {
			var stats = await userRepo.GetUsersCountPerAppAsync(ct);
			metrics.UpdateRegisteredUsers(stats);
			var apps = await appRepo.ListApplicationsAsync(ct);
			foreach (var app in apps) {
				metrics.EnsureMetricsExist(app.Name);
			}
		}
	}
}
