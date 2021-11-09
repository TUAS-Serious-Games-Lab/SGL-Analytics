using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class InMemoryLogStorage : ILogStorage, IDisposable, IAsyncDisposable {
		private class ReadStreamWrapper : Stream {
			private MemoryStream innerStream;

			public ReadStreamWrapper(MemoryStream stream) {
				this.innerStream = stream;
				this.innerStream.Position = 0;
			}

			public override bool CanRead => innerStream.CanRead;

			public override bool CanSeek => innerStream.CanSeek;

			public override bool CanWrite => innerStream.CanWrite;

			public override long Length => innerStream.Length;

			public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

			public override void Close() {
				// Intentionally do nothing, as the innerStream must be preserved for later operations.
			}

			public override ValueTask DisposeAsync() {
				// Intentionally do nothing, as the innerStream must be preserved for later operations.
				return default;
			}

			public override void Flush() {
				innerStream.Flush();
			}

			public override int Read(byte[] buffer, int offset, int count) {
				return innerStream.Read(buffer, offset, count);
			}

			public override long Seek(long offset, SeekOrigin origin) {
				return innerStream.Seek(offset, origin);
			}

			public override void SetLength(long value) {
				throw new NotSupportedException("Can't change length of read-only stream.");
			}

			public override void Write(byte[] buffer, int offset, int count) {
				throw new NotSupportedException("Can't write to read-only stream.");
			}

			protected override void Dispose(bool disposing) {
				base.Dispose(disposing);
			}
		}

		private class WriteStreamWrapper : Stream {
			private MemoryStream innerStream;
			private Action onClose;
			private bool onCloseCalled = false;

			public WriteStreamWrapper(MemoryStream innerStream, Action onClose) {
				this.innerStream = innerStream;
				this.innerStream.Position = 0;
				this.onClose = onClose;
			}

			public override bool CanRead => innerStream.CanRead;

			public override bool CanSeek => innerStream.CanSeek;

			public override bool CanWrite => innerStream.CanWrite;

			public override long Length => innerStream.Length;

			public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

			public override void Close() {
				invokeOnClose();
			}

			private void invokeOnClose() {
				if (!onCloseCalled) {
					onClose();
					onCloseCalled = true;
				}
				// Intentionally don't delegate to innerStream, as it needs to be preserved for later operations.
			}

			public override ValueTask DisposeAsync() {
				invokeOnClose();
				return ValueTask.CompletedTask;
			}

			public override void Flush() {
				innerStream.Flush();
			}

			public override int Read(byte[] buffer, int offset, int count) {
				return innerStream.Read(buffer, offset, count);
			}

			public override long Seek(long offset, SeekOrigin origin) {
				return innerStream.Seek(offset, origin);
			}

			public override void SetLength(long value) {
				innerStream.SetLength(value);
			}

			public override void Write(byte[] buffer, int offset, int count) {
				innerStream.Write(buffer, offset, count);
			}

			protected override void Dispose(bool disposing) {
				invokeOnClose();
			}
		}

		public class LogFile : ILogStorage.ILogFile {
			private InMemoryLogStorage storage;
			private MemoryStream content = new();

			public Guid ID { get; private set; }

			public DateTime CreationTime { get; } = DateTime.Now;

			public DateTime EndTime { get; set; } = DateTime.Now;

			public bool WriteClosed { get; set; } = false;

			public bool Deleted { get; set; } = false;

			public string Suffix => ".log";

			public LogContentEncoding Encoding => LogContentEncoding.Plain;

			public MemoryStream Content => content;

			internal LogFile(InMemoryLogStorage storage, Guid id) {
				this.storage = storage;
				ID = id;
			}

			public bool Equals(ILogStorage.ILogFile? other) => other is LogFile lfo ? (ID == other.ID && storage == lfo.storage) : false;

			public Stream OpenRead() => new ReadStreamWrapper(content);
			public Stream OpenReadRaw() => new ReadStreamWrapper(content);

			public void Remove() {
				Deleted = true;
			}
		}

		private List<LogFile> logs = new();

		public Stream CreateLogFile(out ILogStorage.ILogFile logFileMetadata) {
			var log = new LogFile(this, Guid.NewGuid());
			logs.Add(log);
			logFileMetadata = log;
			return new WriteStreamWrapper(log.Content, () => { log.EndTime = DateTime.Now; log.WriteClosed = true; });
		}

		public IEnumerable<ILogStorage.ILogFile> EnumerateLogs() => logs.Where(log => !log.Deleted);
		public IEnumerable<ILogStorage.ILogFile> EnumerateFinishedLogs() => logs.Where(log => !log.Deleted && log.WriteClosed);

		public void Dispose() {
			foreach (var log in logs) {
				log.Content.Dispose();
			}
		}

		public async ValueTask DisposeAsync() {
			foreach (var log in logs) {
				await log.Content.DisposeAsync();
			}
		}
	}
}
