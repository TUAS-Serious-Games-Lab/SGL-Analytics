using SGL.Analytics.DTO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Client.Tests {
	public class InMemoryLogStorage : ILogStorage {
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

			public Guid ID { get; private set; }

			public DateTime CreationTime { get; } = DateTime.Now;

			public DateTime EndTime { get; set; } = DateTime.Now;

			public bool WriteClosed { get; set; } = false;

			public bool Deleted { get; set; } = false;

			public string Suffix => Finished ? ".log" : ".temp";

			public bool Finished { get; set; } = false;

			public LogContentEncoding Encoding => LogContentEncoding.Plain;

			public byte[] Content { get; internal set; } = Array.Empty<byte>();

			internal LogFile(InMemoryLogStorage storage, Guid id) {
				this.storage = storage;
				ID = id;
			}

			public bool Equals(ILogStorage.ILogFile? other) => other is LogFile lfo ? (ID == other.ID && storage == lfo.storage) : false;

			public Stream OpenReadContent() => new MemoryStream(Content, writable: false);
			public Stream OpenReadEncoded() => new MemoryStream(Content, writable: false);

			public void Remove() {
				Deleted = true;
			}

			public Task FinishAsync(CancellationToken ct = default) {
				if (Finished) {
					throw new InvalidOperationException("Already finished.");
				}
				Finished = true;
				return Task.CompletedTask;
			}
		}

		private List<LogFile> logs = new();

		public Stream CreateLogFile(out ILogStorage.ILogFile logFileMetadata) {
			var log = new LogFile(this, Guid.NewGuid());
			logs.Add(log);
			logFileMetadata = log;
			var memStream = new MemoryStream();
			return new WriteStreamWrapper(memStream, () => {
				log.EndTime = DateTime.Now;
				log.Content = memStream.ToArray();
				memStream.Dispose();
				log.WriteClosed = true;
			});
		}

		public IList<ILogStorage.ILogFile> ListAllLogFiles() => logs.Where(log => !log.Deleted)
			.Cast<ILogStorage.ILogFile>().ToList();
		public IList<ILogStorage.ILogFile> ListLogFiles() => logs.Where(log => !log.Deleted && log.WriteClosed && log.Finished)
			.Cast<ILogStorage.ILogFile>().ToList();

		public IList<ILogStorage.ILogFile> ListUnfinishedLogFilesForRecovery() =>
			logs.Where(log => !log.Deleted && log.WriteClosed && !log.Finished).Cast<ILogStorage.ILogFile>().ToList();
	}
}
