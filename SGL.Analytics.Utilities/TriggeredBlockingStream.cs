using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
	public class TriggeredBlockingStream : Stream {
		private Stream innerStream;
		private object lockObject = new object();
		private TaskCompletionSource readTCS = new TaskCompletionSource();
		private TaskCompletionSource writeTCS = new TaskCompletionSource();

		public TriggeredBlockingStream(Stream innerStream) {
			this.innerStream = innerStream;
		}

		public override bool CanRead => innerStream.CanRead;

		public override bool CanSeek => innerStream.CanSeek;

		public override bool CanWrite => innerStream.CanWrite;

		public override long Length => innerStream.Length;

		public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

		public override void Flush() {
			innerStream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count) {
			Task trigger;
			lock (lockObject) {
				trigger = readTCS.Task;
			}
			trigger.Wait();
			return innerStream.Read(buffer, offset, count);
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			Task trigger;
			lock (lockObject) {
				trigger = readTCS.Task;
			}
			await trigger;
			return await base.ReadAsync(buffer, offset, count, cancellationToken);
		}

		public override long Seek(long offset, SeekOrigin origin) {
			return innerStream.Seek(offset, origin);
		}

		public override void SetLength(long value) {
			innerStream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count) {
			Task trigger;
			lock (lockObject) {
				trigger = writeTCS.Task;
			}
			trigger.Wait();
			innerStream.Write(buffer, offset, count);
		}

		public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			Task trigger;
			lock (lockObject) {
				trigger = writeTCS.Task;
			}
			await trigger;
			await base.WriteAsync(buffer, offset, count, cancellationToken);
		}

		public void TriggerReadReady() {
			lock (lockObject) {
				readTCS.SetResult();
			}
		}

		public void TriggerWriteReady() {
			lock (lockObject) {
				writeTCS.SetResult();
			}
		}

		public void RearmRead() {
			lock (lockObject) {
				readTCS = new TaskCompletionSource();
			}
		}
		public void RearmWrite() {
			lock (lockObject) {
				writeTCS = new TaskCompletionSource();
			}
		}
		public void TriggerReadError(Exception ex) {
			lock (lockObject) {
				readTCS.SetException(ex);
			}
		}

		public void TriggerWriteError(Exception ex) {
			lock (lockObject) {
				writeTCS.SetException(ex);
			}
		}
	}
}
