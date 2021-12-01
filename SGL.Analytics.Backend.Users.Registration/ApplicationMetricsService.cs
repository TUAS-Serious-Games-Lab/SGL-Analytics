using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using SGL.Analytics.Backend.Users.Application.Interfaces;
using SGL.Utilities.Backend;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Users.Registration {
	/// <summary>
	/// A background service that periodically updates application-level metrics.
	/// </summary>
	public class ApplicationMetricsService : ApplicationMetricsServiceBase {
		private IUserRepository repo;
		private static readonly Gauge registeredUsers = Metrics.CreateGauge("sgla_registered_users",
			"Number of users registered with SGL Analytics User Registration service.",
			"app");

		/// <summary>
		/// Instantiates the service, injecting the given dependencies.
		/// </summary>
		public ApplicationMetricsService(IOptions<ApplicationMetricsServiceOptions> options, ILogger<ApplicationMetricsService> logger, IUserRepository repo) : base(options, logger) {
			this.repo = repo;
		}

		/// <summary>
		/// Asynchronously obtains the current metrics values and updates them in Prometheus-net.
		/// </summary>
		protected async override Task UpdateMetrics(CancellationToken ct) {
			var stats = await repo.GetUsersCountPerAppAsync(ct);
			foreach (var entry in stats) {
				registeredUsers.WithLabels(entry.Key).Set(entry.Value);
			}
			var disappeared_app_labels = registeredUsers.GetAllLabelValues().Select(e => e.Single()).Where(lbl => !stats.ContainsKey(lbl));
			foreach(var app_label in disappeared_app_labels) {
				registeredUsers.WithLabels(app_label).Remove();
			}
		}
	}
}
