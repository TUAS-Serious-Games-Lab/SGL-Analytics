using SGL.Analytics.Backend.Logs.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Backend.Logs.Application.Tests.Dummies {
	public class DummyLogFileRepository : ILogFileRepository, IDisposable {
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

		private Dictionary<LogPath, MemoryStream> files = new();

		public async Task CopyLogIntoAsync(string appName, Guid userId, Guid logId, string suffix, Stream contentDestination, CancellationToken ct = default) {
			await using (var stream = await ReadLogAsync(appName, userId, logId, suffix, ct)) {
				await stream.CopyToAsync(contentDestination, ct);
			}
		}

		public async Task DeleteLogAsync(string appName, Guid userId, Guid logId, string suffix, CancellationToken ct = default) {
			var key = new LogPath() { AppName = appName, UserId = userId, LogId = logId, Suffix = suffix };
			ct.ThrowIfCancellationRequested();
			files.Remove(key);
			await Task.CompletedTask;
		}

		public void Dispose() {
			foreach (var kvp in files) {
				kvp.Value.Dispose();
			}
		}

		public IEnumerable<LogPath> EnumerateLogs(string appName, Guid userId) {
			return files.Keys.Where(f => f.AppName == appName && f.UserId == userId);
		}

		public IEnumerable<LogPath> EnumerateLogs(string appName) {
			return files.Keys.Where(f => f.AppName == appName);
		}

		public IEnumerable<LogPath> EnumerateLogs() {
			return files.Keys;
		}

		public async Task<Stream> ReadLogAsync(string appName, Guid userId, Guid logId, string suffix, CancellationToken ct = default) {
			await Task.CompletedTask;
			var key = new LogPath() { AppName = appName, UserId = userId, LogId = logId, Suffix = suffix };
			ct.ThrowIfCancellationRequested();
			if (files.TryGetValue(key, out var content)) {
				return new StreamWrapper(content);
			}
			else {
				throw new LogFileNotAvailableException(key);
			}
		}

		public async Task<long> StoreLogAsync(string appName, Guid userId, Guid logId, string suffix, Stream content, CancellationToken ct = default) {
			var stream = new MemoryStream();
			files[new LogPath() { AppName = appName, UserId = userId, LogId = logId, Suffix = suffix }] = stream;
			await content.CopyToAsync(stream, ct);
			return stream.Length;
		}

		public Task CheckHealthAsync(CancellationToken ct = default) => Task.CompletedTask;
	}
}
