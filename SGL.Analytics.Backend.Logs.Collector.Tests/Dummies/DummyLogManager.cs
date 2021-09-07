using SGL.Analytics.Backend.Domain.Entity;
using SGL.Analytics.Backend.Domain.Exceptions;
using SGL.Analytics.Backend.Logs.Application.Interfaces;
using SGL.Analytics.Backend.Logs.Application.Model;
using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Collector.Tests {
	internal class DummyLogManager : ILogManager, IDisposable, IApplicationRepository {
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

		public Dictionary<string, Domain.Entity.Application> Apps { get; } = new();

		public List<IngestOperation> Ingests { get; } = new();

		public async Task<LogFile> IngestLogAsync(LogMetadataDTO logMetaDTO, Stream logContent) {
			if (!Apps.TryGetValue(logMetaDTO.AppName, out var app)) {
				throw new ApplicationDoesNotExistException(logMetaDTO.AppName);
			}
			var content = new MemoryStream();
			await logContent.CopyToAsync(content);
			content.Position = 0;
			var logMd = new Domain.Entity.LogMetadata(logMetaDTO.LogFileId, app.Id, logMetaDTO.UserId, logMetaDTO.LogFileId,
				logMetaDTO.CreationTime.ToUniversalTime(), logMetaDTO.EndTime.ToUniversalTime(), DateTime.Now.ToUniversalTime(), ".log.gz", true);
			logMd.App = app;
			Ingests.Add(new IngestOperation(logMetaDTO, logMd, content));
			return new LogFile(logMd, new SingleLogFileRepository(app.Name, logMetaDTO.UserId, logMd.Id, ".log.gz", content));
		}

		public void Dispose() {
			Ingests.ForEach(i => i.LogContent.Dispose());
		}

		public async Task<Domain.Entity.Application?> GetApplicationByNameAsync(string appName) {
			await Task.CompletedTask;
			if (Apps.TryGetValue(appName, out var app)) {
				return app;
			}
			else {
				return null;
			}
		}

		public async Task<Domain.Entity.Application> AddApplicationAsync(Domain.Entity.Application app) {
			if (Apps.ContainsKey(app.Name)) throw new EntityUniquenessConflictException("Application", "Name");
			if (app.Id == Guid.Empty) app.Id = Guid.NewGuid();
			Apps.Add(app.Name, app);
			await Task.CompletedTask;
			return app;
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

			public async Task CopyLogIntoAsync(string appName, Guid userId, Guid logId, string suffix, Stream contentDestination) {
				if ((appName, userId, logId, suffix) != (this.appName, this.userId, this.logId, this.suffix)) {
					throw new LogFileNotAvailableException(new LogPath { AppName = appName, UserId = userId, LogId = logId, Suffix = suffix });
				}
				content.Position = 0;
				await content.CopyToAsync(contentDestination);
			}

			public Task DeleteLogAsync(string appName, Guid userId, Guid logId, string suffix) {
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

			public async Task<Stream> ReadLogAsync(string appName, Guid userId, Guid logId, string suffix) {
				if ((appName, userId, logId, suffix) != (this.appName, this.userId, this.logId, this.suffix)) {
					throw new LogFileNotAvailableException(new LogPath { AppName = appName, UserId = userId, LogId = logId, Suffix = suffix });
				}
				await Task.CompletedTask;
				return new StreamWrapper(content);
			}

			public Task StoreLogAsync(string appName, Guid userId, Guid logId, string suffix, Stream content) {
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