using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Utilities {

	public abstract class CommandService<TResult> : IScopedBackgroundService {
		protected IHost host;
		protected ServiceResultWrapper<TResult> resultWrapper;

		protected CommandService(IHost host, ServiceResultWrapper<TResult> resultWrapper) {
			this.host = host;
			this.resultWrapper = resultWrapper;
		}

		async Task IScopedBackgroundService.ExecuteAsync(CancellationToken stoppingToken) {
			await Task.Yield();
			try {
				var result = await RunAsync(stoppingToken);
				resultWrapper.Result = result;
			}
			catch (Exception ex) {
				resultWrapper.Result = ResultForUncaughtException(ex);
			}
			_ = host.StopAsync();
		}

		protected abstract Task<TResult> RunAsync(CancellationToken ct);

		protected abstract TResult ResultForUncaughtException(Exception ex);
	}
}
