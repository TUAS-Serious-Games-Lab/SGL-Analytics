using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Utilities.Backend;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector {
	public class ApplicationMetricsServiceOptions {
		public const string OptionsName = "ApplicationMetrics";
		public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMinutes(1);
	}
	public static class ApplicationMetricsServiceExtensions {
		public static IServiceCollection UseApplicationMetricsService(this IServiceCollection services, IConfiguration config) {
			services.Configure<ApplicationMetricsServiceOptions>(config.GetSection(ApplicationMetricsServiceOptions.OptionsName));
			services.AddScopedBackgroundService<ApplicationMetricsService>();
			return services;
		}
	}
	public class ApplicationMetricsService : IScopedBackgroundService {
		private ApplicationMetricsServiceOptions options;
		private ILogMetadataRepository repo;
		private ILogger<ApplicationMetricsService> logger;
		private static readonly Gauge logsCollected = Metrics.CreateGauge("sgla_logs_collected", 
			"Number of log files already collected by SGL Analytics Log Collector service.",
			"app");

		public ApplicationMetricsService(IOptions<ApplicationMetricsServiceOptions> options, ILogMetadataRepository repo, ILogger<ApplicationMetricsService> logger) {
			this.options = options.Value;
			this.repo = repo;
			this.logger = logger;
		}

		public async Task ExecuteAsync(CancellationToken stoppingToken) {
			logger.LogDebug("Starting ApplicationMetricsService ...");
			try {
				while (!stoppingToken.IsCancellationRequested) {
					try {
						await UpdateMetrics(stoppingToken);
					}
					catch (OperationCanceledException) {
						break;
					}
					catch (Exception ex) {
						logger.LogWarning(ex, "Caught error during metrics update.");
					}
					await Task.Delay(options.UpdateInterval, stoppingToken);
				}
			}
			finally {
				logger.LogDebug("ApplicationMetricsService is shutting down.");
			}
		}

		public async Task UpdateMetrics(CancellationToken ct) {
			var stats = await repo.GetLogsCountPerAppAsync(ct);
			foreach (var entry in stats) {
				logsCollected.WithLabels(entry.Key).Set(entry.Value);
			}
		}
	}
}
