using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Utilities.Backend;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector {

	public class ApplicationMetricsService : ApplicationMetricsServiceBase {
		private ILogMetadataRepository repo;
		private static readonly Gauge logsCollected = Metrics.CreateGauge("sgla_logs_collected",
			"Number of log files already collected by SGL Analytics Log Collector service.",
			"app");

		public ApplicationMetricsService(IOptions<ApplicationMetricsServiceOptions> options, ILogger<ApplicationMetricsService> logger, ILogMetadataRepository repo) : base(options, logger) {
			this.repo= repo;
		}

		protected override async Task UpdateMetrics(CancellationToken ct) {
			var stats = await repo.GetLogsCountPerAppAsync(ct);
			foreach (var entry in stats) {
				logsCollected.WithLabels(entry.Key).Set(entry.Value);
			}
		}
	}
}
