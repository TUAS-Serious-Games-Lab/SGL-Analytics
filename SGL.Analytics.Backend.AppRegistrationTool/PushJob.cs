using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Analytics.Backend.Utilities;
using SGL.Analytics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.AppRegistrationTool {
	public class PushJob : CommandService {
		private Program.PushOptions opts;
		private ILogger<PushJob> logger;
		private LogsContext logsContext;
		private UsersContext usersContext;
		private AppRegistrationManager appRegMgr;
		private DbContext[] contexts;

		public PushJob(IHost host, ServiceResultWrapper<PushJob, int> exitCodeWrapper, Program.PushOptions opts, ILogger<PushJob> logger,
			LogsContext logsContext, UsersContext usersContext, AppRegistrationManager appRegMgr) : base(host, exitCodeWrapper) {
			this.opts = opts;
			this.logger = logger;
			this.logsContext = logsContext;
			this.usersContext = usersContext;
			this.appRegMgr = appRegMgr;
			this.contexts = new DbContext[] { logsContext, usersContext };
		}

		protected override async Task<int> RunAsync(CancellationToken ct) {
			try {
				var files = Directory.EnumerateFiles(opts.AppDefinitionsDirectory).Where(fn => !Path.GetFileName(fn).StartsWith("appsettings.", StringComparison.OrdinalIgnoreCase));
				var definitions = await files.BatchAsync(64, batch => batch.Select(file => readDefinitionAsync(file, ct)), ct).ToListAsync(ct);

				await host.WaitForDbsReadyAsync<LogsContext, UsersContext>(pollingInterval: TimeSpan.FromMilliseconds(100), ct);
				using var transactions = (await Task.WhenAll(contexts.Select(ctx => ctx.Database.BeginTransactionAsync(ct)))).ToDisposableEnumerable<IDbContextTransaction>();

				var results = new List<AppRegistrationManager.PushResult>();

				// We can't run push operations concurrently, because it is not supported by DbContext. Therefore, we need to await each push operation separately.
				foreach (var appDef in definitions) {
					if (appDef == null) continue;
					results.AddRange(await appRegMgr.PushApplicationAsync(appDef, ct));
				}

				if (results.Any(res => res == AppRegistrationManager.PushResult.Error)) {
					logger.LogInformation("Due to the above errors, the transactions for the updates are being rolled back now...");
					await Task.WhenAll(transactions.Select(t => t.RollbackAsync(ct)));
					return 2;
				}
				else {
					await Task.WhenAll(transactions.Select(t => t.CommitAsync(ct)));
					logger.LogInformation("The transactions were successfully committed.");
					return 0;
				}
			}
			catch (OperationCanceledException) {
				await Console.Out.WriteLineAsync();
				logger.LogInformation("The push command was cancelled.");
				return 4;
			}
			catch (Exception ex) {
				logger.LogError(ex, "Encountered an unexpected error.");
				return 3;
			}

		}

		private class EnumNamingPolicy : JsonNamingPolicy {
			public override string ConvertName(string name) => name;
		}

		private readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions() {
			WriteIndented = true,
			Converters = { new JsonStringEnumConverter(new EnumNamingPolicy()) }
		};

		private Task<ApplicationWithUserProperties?> readDefinitionAsync(string filename, CancellationToken ct = default) {
			return Task.Run(async () => {
				logger.LogDebug("Loading application definition file '{file}' ...", Path.GetFileName(filename));
				using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
				var def = await JsonSerializer.DeserializeAsync<ApplicationWithUserProperties>(stream, jsonSerializerOptions, ct).AsTask();
				if (def == null) logger.LogWarning("Couldn't load application definition from file '{filename}'. It will be skipped.", Path.GetFileName(filename));
				else logger.LogDebug("Successfully loaded application definition file '{file}'.", Path.GetFileName(filename));
				return def;
			}, ct);
		}
	}
}
