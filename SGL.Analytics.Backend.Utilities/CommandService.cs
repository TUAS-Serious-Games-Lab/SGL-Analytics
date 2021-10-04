using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Utilities {
	public abstract class CommandService : IScopedBackgroundService {
		protected IHost host;
		protected ServiceResultWrapper<int> exitCodeWrapper;

		protected CommandService(IHost host, ServiceResultWrapper<int> exitCodeWrapper) {
			this.host = host;
			this.exitCodeWrapper = exitCodeWrapper;
		}

		public async Task ExecuteAsync(CancellationToken stoppingToken) {
			await Task.Yield();
			try {
				var result = await RunAsync(stoppingToken);
				exitCodeWrapper.Result = result;
			}
			catch (Exception) {
				exitCodeWrapper.Result = 255;
			}
			_ = host.StopAsync();
		}

		protected abstract Task<int> RunAsync(CancellationToken ct);
	}
}
