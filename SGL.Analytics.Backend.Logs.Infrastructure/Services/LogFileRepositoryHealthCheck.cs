using Microsoft.Extensions.Diagnostics.HealthChecks;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Services {
	/// <summary>
	/// Implements a health check for <see cref="ILogFileRepository"/> implementations to allow an ASP.NET Core application to include their health status in its reported health.
	/// </summary>
	public class LogFileRepositoryHealthCheck : IHealthCheck {
		private ILogFileRepository logRepo;

		/// <summary>
		/// Constructs a health check object, injecting the active configured <see cref="ILogFileRepository"/> implementation.
		/// </summary>
		public LogFileRepositoryHealthCheck(ILogFileRepository logRepo) {
			this.logRepo = logRepo;
		}

		/// <summary>
		/// Asynchronously performs the health check by calling <see cref="ILogFileRepository.CheckHealthAsync(CancellationToken)"/>, returning
		/// <see cref="HealthCheckResult.Healthy(string?, IReadOnlyDictionary{string, object}?)"/> if the call completes without exceptions and returning
		/// <see cref="HealthCheckResult.Unhealthy(string?, Exception?, IReadOnlyDictionary{string, object}?)"/> if an exception is thrown from the call.
		/// </summary>
		/// <param name="context">A context object, not used by this implementation.</param>
		/// <param name="cancellationToken">A cancellation token, allowing the cancellation of the health check operation.</param>
		/// <returns>A result object with the corresponding status.</returns>
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
