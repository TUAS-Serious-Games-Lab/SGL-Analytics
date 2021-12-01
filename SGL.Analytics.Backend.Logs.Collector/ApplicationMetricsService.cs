using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Utilities.Backend;
using SGL.Utilities.PrometheusNet;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector {

	/// <summary>
	/// A background service that periodically updates application-level metrics.
	/// </summary>
	public class ApplicationMetricsService : ApplicationMetricsServiceBase {
		private ILogMetadataRepository repo;
		private static readonly Gauge logsCollected = Metrics.CreateGauge("sgla_logs_collected",
			"Number of log files already collected by SGL Analytics Log Collector service.",
			"app");

		/// <summary>
		/// Instantiates the service, injecting the given dependencies.
		/// </summary>
		public ApplicationMetricsService(IOptions<ApplicationMetricsServiceOptions> options, ILogger<ApplicationMetricsService> logger, ILogMetadataRepository repo) : base(options, logger) {
			this.repo= repo;
		}

		/// <summary>
		/// Asynchronously obtains the current metrics values and updates them in Prometheus-net.
		/// </summary>
		protected override async Task UpdateMetrics(CancellationToken ct) {
			var stats = await repo.GetLogsCountPerAppAsync(ct);
			logsCollected.UpdateLabeledValues(stats);
		}
	}
}
