using Microsoft.Extensions.Diagnostics.HealthChecks;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {
	public class LogFileRepositoryHealthCheck : IHealthCheck {
		private ILogFileRepository logRepo;

		public LogFileRepositoryHealthCheck(ILogFileRepository logRepo) {
			this.logRepo = logRepo;
		}

		public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
			try {
				await logRepo.CheckHealthAsync(cancellationToken);
				return HealthCheckResult.Healthy("The log file repository is fully operational.");

			}catch(Exception ex) {
				return HealthCheckResult.Unhealthy("The log file repository is not operational.",ex);
			}
		}
	}
}
