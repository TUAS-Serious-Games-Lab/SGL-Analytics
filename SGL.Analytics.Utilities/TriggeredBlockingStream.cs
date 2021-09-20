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
		private List<TaskCompletionSource<int>> readTCSs = new List<TaskCompletionSource<int>> { new TaskCompletionSource<int>() };
		private List<TaskCompletionSource<bool>> writeTCSs = new List<TaskCompletionSource<bool>> { new TaskCompletionSource<bool>() };

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
			Task<int> trigger;
			lock (lockObject) {
				trigger = readTCSs[0].Task;
			}
			int triggerCount = trigger.Result;
			if (triggerCount >= 0) {
				lock (lockObject) {
					readTCSs.RemoveAt(0);
				}
			}
			return innerStream.Read(buffer, offset, triggerCount > 0 ? triggerCount : count);
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			Task<int> trigger;
			lock (lockObject) {
				trigger = readTCSs[0].Task;
			}
			int triggerCount = await trigger;
			if (triggerCount >= 0) {
				lock (lockObject) {
					readTCSs.RemoveAt(0);
				}
			}
			return await base.ReadAsync(buffer, offset, triggerCount > 0 ? triggerCount : count, cancellationToken);
		}

		public override long Seek(long offset, SeekOrigin origin) {
			return innerStream.Seek(offset, origin);
		}

		public override void SetLength(long value) {
			innerStream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count) {
			Task<bool> trigger;
			lock (lockObject) {
				trigger = writeTCSs[0].Task;
			}
			bool once = trigger.Result;
			if (once) {
				lock (lockObject) {
					writeTCSs.RemoveAt(0);
				}
			}
			innerStream.Write(buffer, offset, count);
		}

		public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			Task<bool> trigger;
			lock (lockObject) {
				trigger = writeTCSs[0].Task;
			}
			bool once = await trigger;
			if (once) {
				lock (lockObject) {
					writeTCSs.RemoveAt(0);
				}
			}
			await base.WriteAsync(buffer, offset, count, cancellationToken);
		}

		public void TriggerReadReady(int count = -1) {
			lock (lockObject) {
				var tcs = readTCSs.Last();
				readTCSs.Add(new TaskCompletionSource<int>());
				tcs.SetResult(count);
			}
		}

		public void TriggerWriteReady(bool once = false) {
			lock (lockObject) {
				var tcs = writeTCSs.Last();
				writeTCSs.Add(new TaskCompletionSource<bool>());
				tcs.SetResult(once);
			}
		}

		public void TriggerReadError(Exception ex) {
			lock (lockObject) {
				var tcs = readTCSs.Last();
				readTCSs.Add(new TaskCompletionSource<int>());
				tcs.SetException(ex);
			}
		}

		public void TriggerWriteError(Exception ex) {
			lock (lockObject) {
				var tcs = writeTCSs.Last();
				writeTCSs.Add(new TaskCompletionSource<bool>());
				tcs.SetException(ex);
			}
		}
	}
}
