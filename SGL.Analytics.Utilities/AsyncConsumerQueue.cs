using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
	public class AsyncConsumerQueue<T> {
		private Channel<T> channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false });
		public void Enqueue(T item) {
			if (!channel.Writer.TryWrite(item)) {
				throw new InvalidOperationException("Can't enqueue to this queue object because it is already finished.");
			}
		}
		public IAsyncEnumerable<T> DequeueAllAsync() {
			return channel.Reader.ReadAllAsync();
		}
		public void Finish() {
			channel.Writer.Complete();
		}
	}
}
