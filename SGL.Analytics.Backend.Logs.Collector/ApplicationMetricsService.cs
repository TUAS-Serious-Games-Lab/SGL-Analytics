using Microsoft.Extensions.Logging;
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
		private readonly ILogMetadataRepository logRepo;
		private readonly IApplicationRepository appRepo;
		private readonly IMetricsManager metrics;

		/// <summary>
		/// Instantiates the service, injecting the given dependencies.
		/// </summary>
		public ApplicationMetricsService(IOptions<ApplicationMetricsServiceOptions> options, ILogger<ApplicationMetricsService> logger,
			ILogMetadataRepository logRepo, IApplicationRepository appRepo, IMetricsManager metrics) : base(options, logger) {
			this.logRepo = logRepo;
			this.appRepo = appRepo;
			this.metrics = metrics;
		}

		/// <summary>
		/// Asynchronously obtains the current metrics values and updates them in the injected metrics manager.
		/// It also calls <see cref="IMetricsManager.EnsureMetricsExist(string)"/> for all registered apps.
		/// </summary>
		protected override async Task UpdateMetrics(CancellationToken ct) {
			var logsCounts = await logRepo.GetLogsCountPerAppAsync(ct);
			metrics.UpdateCollectedLogs(logsCounts);
			var avgLogSizes = await logRepo.GetLogSizeAvgPerAppAsync(ct);
			metrics.UpdateAvgLogSize(avgLogSizes);
			var apps = await appRepo.ListApplicationsAsync(ct);
			foreach (var app in apps) {
				metrics.EnsureMetricsExist(app.Name);
			}
		}
	}
}
