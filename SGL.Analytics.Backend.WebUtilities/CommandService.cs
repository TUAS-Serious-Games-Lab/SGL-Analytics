using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.WebUtilities {
	public abstract class CommandService : IScopedBackgroundService {
		protected IHost host;
		protected ServiceResultWrapper<int> exitCodeWrapper;

		protected CommandService(IHost host, ServiceResultWrapper<int> exitCodeWrapper) {
			this.host = host;
			this.exitCodeWrapper = exitCodeWrapper;
		}

		public async Task ExecuteAsync(CancellationToken stoppingToken) {
			await Task.Yield();
			var result = await RunAsync(stoppingToken);
			exitCodeWrapper.Result = result;
			_ = host.StopAsync();
		}

		protected abstract Task<int> RunAsync(CancellationToken ct);
	}
}
