using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
	/// <summary>
	/// A stream wrapper that allows to artificially delay the completion of I/O operations of the wrapped stream or to inject errors into the operations.
	/// This can be useful to simulate waiting times and I/O errors for testing.
	/// </summary>
	public class TriggeredBlockingStream : Stream {
		private Stream innerStream;
		private object lockObject = new object();
		private List<TaskCompletionSource<int>> readTCSs = new List<TaskCompletionSource<int>> { new TaskCompletionSource<int>() };
		private List<TaskCompletionSource<bool>> writeTCSs = new List<TaskCompletionSource<bool>> { new TaskCompletionSource<bool>() };

		/// <summary>
		/// Creates a <see cref="TriggeredBlockingStream"/> wrapping the given inner <see cref="Stream"/>.
		/// </summary>
		/// <param name="innerStream">The stream to wrap.</param>
		public TriggeredBlockingStream(Stream innerStream) {
			this.innerStream = innerStream;
		}

		/// <summary>
		/// Forwarded to the wrapped stream.
		/// </summary>
		public override bool CanRead => innerStream.CanRead;

		/// <summary>
		/// Forwarded to the wrapped stream.
		/// </summary>
		public override bool CanSeek => innerStream.CanSeek;

		/// <summary>
		/// Forwarded to the wrapped stream.
		/// </summary>
		public override bool CanWrite => innerStream.CanWrite;

		/// <summary>
		/// Forwarded to the wrapped stream.
		/// </summary>
		public override long Length => innerStream.Length;

		/// <summary>
		/// Forwarded to the wrapped stream.
		/// </summary>
		public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

		/// <summary>
		/// Forwarded to the wrapped stream.
		/// </summary>
		public override void Flush() {
			innerStream.Flush();
		}

		/// <summary>
		/// Wait for the read readiness to be triggered and then either read from the wrapped stream normally (if 0 or -1 was passed to <see cref="TriggerReadReady(int)"/>),
		/// read just the amount of bytes passed to the call to <see cref="TriggerReadReady(int)"/>, or
		/// throw the exception passed to <see cref="TriggerReadError(Exception)"/>.
		/// </summary>
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

		/// <summary>
		/// Asynchronously wait for the read readiness to be triggered and then either read from the wrapped stream normally (if 0 or -1 was passed to <see cref="TriggerReadReady(int)"/>),
		/// read just the amount of bytes passed to the call to <see cref="TriggerReadReady(int)"/>, or
		/// throw the exception passed to <see cref="TriggerReadError(Exception)"/>.
		/// </summary>
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

		/// <summary>
		/// Forwarded to the wrapped stream.
		/// </summary>
		public override long Seek(long offset, SeekOrigin origin) {
			return innerStream.Seek(offset, origin);
		}

		/// <summary>
		/// Forwarded to the wrapped stream.
		/// </summary>
		public override void SetLength(long value) {
			innerStream.SetLength(value);
		}

		/// <summary>
		/// Wait for the write readiness to be triggered and then write to the wrapped stream.
		/// </summary>
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

		/// <summary>
		/// Asynchronously wait for the write readiness to be triggered and then write to the wrapped stream.
		/// </summary>
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

		/// <summary>
		/// Triggers read readiness.
		/// If <c><![CDATA[count<0]]></c>, the stream becomes permanently read-ready.
		/// If <c>count==0</c>, the next (or currently pending) read operation becomes ready and reads the amount of byte requested in the read operation.
		/// If <c><![CDATA[count>0]]></c>, the next (or currently pending) read operation becomes ready and reads the given number of bytes.
		/// </summary>
		public void TriggerReadReady(int count = -1) {
			lock (lockObject) {
				var tcs = readTCSs.Last();
				readTCSs.Add(new TaskCompletionSource<int>());
				tcs.SetResult(count);
			}
		}

		/// <summary>
		/// Triggers write readiness.
		/// If <c>once==false</c>, the stream becomes permanently write-ready.
		/// If <c>once==true</c>, only the next (or currently pending) write operation becomes ready.
		/// </summary>
		public void TriggerWriteReady(bool once = false) {
			lock (lockObject) {
				var tcs = writeTCSs.Last();
				writeTCSs.Add(new TaskCompletionSource<bool>());
				tcs.SetResult(once);
			}
		}

		/// <summary>
		/// Triggers an error state for currently pending and subsequent read operations.
		/// </summary>
		/// <param name="ex">The exception to be thrown from the read operations.</param>
		public void TriggerReadError(Exception ex) {
			lock (lockObject) {
				var tcs = readTCSs.Last();
				readTCSs.Add(new TaskCompletionSource<int>());
				tcs.SetException(ex);
			}
		}

		/// <summary>
		/// Triggers an error state for currently pending and subsequent write operations.
		/// </summary>
		/// <param name="ex">The exception to be thrown from the write operations.</param>
		public void TriggerWriteError(Exception ex) {
			lock (lockObject) {
				var tcs = writeTCSs.Last();
				writeTCSs.Add(new TaskCompletionSource<bool>());
				tcs.SetException(ex);
			}
		}
	}
}
