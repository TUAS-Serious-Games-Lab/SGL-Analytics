using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Logs.Infrastructure.Data;
using SGL.Analytics.Backend.Users.Infrastructure.Data;
using SGL.Utilities;
using SGL.Utilities.Backend;
using SGL.Utilities.Crypto.Certificates;
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
	/// <summary>
	/// Implements the functionality of a <c>push</c> operation of the AppRegistrationTool.
	/// It runs as a scoped hosted service in the service container with dependency injection provided by <see cref="IHost"/>.
	/// </summary>
	public class PushJob : CommandService<int> {
		private IHost host;
		private Program.PushOptions opts;
		private ILogger<PushJob> logger;
		private LogsContext logsContext;
		private UsersContext usersContext;
		private AppRegistrationManager appRegMgr;
		private DbContext[] contexts;

		/// <summary>
		/// Instantiates the job object using the given injected dependency objects.
		/// </summary>
		public PushJob(IHost host, ServiceResultWrapper<PushJob, int> exitCodeWrapper, Program.PushOptions opts, ILogger<PushJob> logger,
			LogsContext logsContext, UsersContext usersContext, AppRegistrationManager appRegMgr) : base(host, exitCodeWrapper) {
			this.host = host;
			this.opts = opts;
			this.logger = logger;
			this.logsContext = logsContext;
			this.usersContext = usersContext;
			this.appRegMgr = appRegMgr;
			this.contexts = new DbContext[] { logsContext, usersContext };
		}

		/// <summary>
		/// Runs the push operation.
		/// </summary>
		/// <param name="ct">A cancellation token triggered when the command is interruped by host shutdown.</param>
		/// <returns>A task representing the operation, providing an exit code as a result upon termination.</returns>
		protected override async Task<int> RunAsync(CancellationToken ct) {
			try {
				var files = Directory.EnumerateFiles(opts.AppDefinitionsDirectory)
					.Where(fn => Path.GetFileName(fn).EndsWith(".json", StringComparison.OrdinalIgnoreCase))
					.Where(fn => !Path.GetFileName(fn).StartsWith("appsettings.", StringComparison.OrdinalIgnoreCase));
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

		private async Task<ApplicationWithUserProperties?> readDefinitionAsync(string filename, CancellationToken ct = default) {
			var definition = await Task.Run(async () => {
				logger.LogDebug("Loading application definition file '{file}' ...", Path.GetFileName(filename));
				using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
				var def = await JsonSerializer.DeserializeAsync<ApplicationWithUserProperties>(stream, jsonSerializerOptions, ct).AsTask();
				if (def == null) logger.LogWarning("Couldn't load application definition from file '{filename}'. It will be skipped.", Path.GetFileName(filename));
				else logger.LogDebug("Successfully loaded application definition file '{file}'.", Path.GetFileName(filename));
				return def;
			}, ct);
			if (definition != null) {
				definition.DataRecipients = (await loadCertificates(filename, definition, ct)).ToList();
				if (!definition.DataRecipients.Any()) {
					logger.LogWarning("No associated data recipient certificates found for app {app} from definition file '{definitionFile}'.", definition.Name, filename);
				}
			}
			return definition;
		}

		private async Task<IEnumerable<Recipient>> loadCertificates(string filename, ApplicationWithUserProperties? definition, CancellationToken ct) {
			string dir = Path.GetDirectoryName(filename) ?? throw new ArgumentException("Filename has no valid directory.");
			string fileBaseName = Path.GetFileNameWithoutExtension(filename);
			EnumerationOptions enumOpts = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
			var siblingPemFile = Directory.EnumerateFiles(dir, fileBaseName + ".pem", enumOpts);
			var pemDirName = Path.Combine(dir, fileBaseName);
			var dirPemFiles = Directory.Exists(pemDirName) ? Directory.EnumerateFiles(pemDirName, "*.pem", enumOpts) : Enumerable.Empty<string>();
			var siblingPemTask = siblingPemFile.Select(file => readPemCertFileAsync(file, ct));
			var dirPemTasks = dirPemFiles.Select(async file => (file: file, certs: await readPemCertFileAsync(file, ct)));
			var dirCerts = await Task.WhenAll(dirPemTasks);
			var siblingCerts = await Task.WhenAll(siblingPemTask);
			var siblingRecipients = siblingCerts.SelectMany(item => item).Select(cert => createRecipient(cert, cert.SubjectDN.ToString() ?? ""));
			var dirRecipients = dirCerts.SelectMany(item => item.certs.Select(c => (file: item.file, cert: c)))
				.Select(item => createRecipient(item.cert, (item.cert.SubjectDN.ToString() ?? "") + " # " + Path.GetFileNameWithoutExtension(item.file)));
			return dirRecipients.Concat(siblingRecipients);
		}

		private Task<IEnumerable<Certificate>> readPemCertFileAsync(string file, CancellationToken ct) {
			return Task.Run(() => {
				using var fileReader = File.OpenText(file);
				return Certificate.LoadAllFromPem(fileReader).ToList().AsEnumerable();
			});
		}

		private Recipient createRecipient(Certificate cert, string label) {
			using var strWriter = new StringWriter();
			cert.StoreToPem(strWriter);
			return new Recipient(Guid.Empty, cert.PublicKey.CalculateId(), label, strWriter.ToString());
		}

		/// <summary>
		/// Returns 5 as the exit code for unexpected exceptions.
		/// </summary>
		protected override int ResultForUncaughtException(Exception ex) => 5;
	}
}
