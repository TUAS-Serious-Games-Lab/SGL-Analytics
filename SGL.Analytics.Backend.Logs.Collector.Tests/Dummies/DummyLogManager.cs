using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Model;
using SGL.Analytics.DTO;
using SGL.Utilities.Backend.Applications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector.Tests {
	internal class DummyLogManager : ILogManager, IDisposable {
		public class IngestOperation {
			public LogMetadataDTO LogMetaDTO { get; set; }
			public LogMetadata LogMetadata { get; set; }
			public MemoryStream LogContent { get; set; }

			public IngestOperation(LogMetadataDTO logMetaDTO, LogMetadata logMetadata, MemoryStream logContent) {
				LogMetaDTO = logMetaDTO;
				LogMetadata = logMetadata;
				LogContent = logContent;
			}
		}

		private IApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions> appRepo;
		public List<IngestOperation> Ingests { get; } = new();

		public DummyLogManager(IApplicationRepository<Domain.Entity.Application, ApplicationQueryOptions> appRepo) {
			this.appRepo = appRepo;
		}

		public async Task<LogFile> IngestLogAsync(Guid userId, string appName, string appApiToken, LogMetadataDTO logMetaDTO, Stream logContent, CancellationToken ct = default) {
			long size = 0;
			var app = await appRepo.GetApplicationByNameAsync(appName);
			if (app == null) {
				throw new ApplicationDoesNotExistException(appName);
			}
			else if (app.ApiToken != appApiToken) {
				throw new ApplicationApiTokenMismatchException(appName, appApiToken);
			}
			var content = new MemoryStream();
			await logContent.CopyToAsync(content, ct);
			content.Position = 0;
			var logMd = new LogMetadata(logMetaDTO.LogFileId, app.Id, userId, logMetaDTO.LogFileId,
				logMetaDTO.CreationTime.ToUniversalTime(), logMetaDTO.EndTime.ToUniversalTime(), DateTime.Now.ToUniversalTime(),
				logMetaDTO.NameSuffix, logMetaDTO.LogContentEncoding, size, true);
			logMd.App = app;
			ct.ThrowIfCancellationRequested();
			Ingests.Add(new IngestOperation(logMetaDTO, logMd, content));
			return new LogFile(logMd, new SingleLogFileRepository(app.Name, userId, logMd.Id, logMd.FilenameSuffix, content));
		}

		public void Dispose() {
			Ingests.ForEach(i => i.LogContent.Dispose());
		}

		class SingleLogFileRepository : ILogFileRepository, IDisposable {
			private string appName;
			private Guid userId;
			private Guid logId;
			private string suffix;
			private MemoryStream content;

			public SingleLogFileRepository(string appName, Guid userId, Guid logId, string suffix, MemoryStream content) {
				this.appName = appName;
				this.userId = userId;
				this.logId = logId;
				this.suffix = suffix;
				this.content = content;
			}

			public Task CheckHealthAsync(CancellationToken ct = default) => Task.CompletedTask;

			public async Task CopyLogIntoAsync(string appName, Guid userId, Guid logId, string suffix, Stream contentDestination, CancellationToken ct = default) {
				if ((appName, userId, logId, suffix) != (this.appName, this.userId, this.logId, this.suffix)) {
					throw new LogFileNotAvailableException(new LogPath { AppName = appName, UserId = userId, LogId = logId, Suffix = suffix });
				}
				content.Position = 0;
				await content.CopyToAsync(contentDestination, ct);
			}

			public Task DeleteLogAsync(string appName, Guid userId, Guid logId, string suffix, CancellationToken ct = default) {
				throw new NotImplementedException();
			}

			public void Dispose() => content.Dispose();

			public IEnumerable<LogPath> EnumerateLogs(string appName, Guid userId) {
				throw new NotImplementedException();
			}

			public IEnumerable<LogPath> EnumerateLogs(string appName) {
				throw new NotImplementedException();
			}

			public IEnumerable<LogPath> EnumerateLogs() {
				throw new NotImplementedException();
			}

			public async Task<Stream> ReadLogAsync(string appName, Guid userId, Guid logId, string suffix, CancellationToken ct = default) {
				if ((appName, userId, logId, suffix) != (this.appName, this.userId, this.logId, this.suffix)) {
					throw new LogFileNotAvailableException(new LogPath { AppName = appName, UserId = userId, LogId = logId, Suffix = suffix });
				}
				await Task.CompletedTask;
				ct.ThrowIfCancellationRequested();
				return new StreamWrapper(content);
			}

			public Task<long> StoreLogAsync(string appName, Guid userId, Guid logId, string suffix, Stream content, CancellationToken ct = default) {
				throw new NotImplementedException();
			}

			private class StreamWrapper : Stream {
				private Stream inner;

				public StreamWrapper(Stream inner) {
					this.inner = inner;
					this.inner.Position = 0;
				}

				public override bool CanRead => inner.CanRead;
				public override bool CanSeek => inner.CanSeek;
				public override bool CanWrite => inner.CanWrite;
				public override long Length => inner.Length;
				public override long Position { get => inner.Position; set => inner.Position = value; }
				public override void Flush() => inner.Flush();
				public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
				public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
				public override void SetLength(long value) => inner.SetLength(value);
				public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
				public override void Close() { }
			}
		}
	}
}