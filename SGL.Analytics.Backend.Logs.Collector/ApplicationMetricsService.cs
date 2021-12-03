﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Utilities.Backend;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector {

	/// <summary>
	/// A background service that periodically updates application-level metrics.
	/// </summary>
	public class ApplicationMetricsService : ApplicationMetricsServiceBase {
		private readonly ILogMetadataRepository repo;
		private readonly IMetricsManager metrics;

		/// <summary>
		/// Instantiates the service, injecting the given dependencies.
		/// </summary>
		public ApplicationMetricsService(IOptions<ApplicationMetricsServiceOptions> options, ILogger<ApplicationMetricsService> logger, ILogMetadataRepository repo, IMetricsManager metrics) : base(options, logger) {
			this.repo = repo;
			this.metrics = metrics;
		}

		/// <summary>
		/// Asynchronously obtains the current metrics values and updates them in Prometheus-net.
		/// </summary>
		protected override async Task UpdateMetrics(CancellationToken ct) {
			var stats = await repo.GetLogsCountPerAppAsync(ct);
			metrics.UpdateCollectedLogs(stats);
		}
	}
}
