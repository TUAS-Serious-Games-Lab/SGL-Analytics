﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Utilities.Backend;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Registration {
	public class ApplicationMetricsService : ApplicationMetricsServiceBase {
		private IUserRepository repo;
		private static readonly Gauge registeredUsers = Metrics.CreateGauge("sgla_registered_users",
			"Number of users registered with SGL Analytics User Registration service.",
			"app");

		public ApplicationMetricsService(IOptions<ApplicationMetricsServiceOptions> options, ILogger<ApplicationMetricsService> logger, IUserRepository repo) : base(options, logger) {
			this.repo = repo;
		}

		protected async override Task UpdateMetrics(CancellationToken ct) {
			var stats = await repo.GetUsersCountPerAppAsync(ct);
			foreach (var entry in stats) {
				registeredUsers.WithLabels(entry.Key).Set(entry.Value);
			}
		}
	}
}
