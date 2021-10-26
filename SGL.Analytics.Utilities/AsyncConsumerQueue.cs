using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {

	/// <summary>
	/// Provides a queue for elements that are produced by arbitrary threads and asynchronously consumed by a single consumer thread or task.
	/// It acts as a convenient wrapper around an appropriately parameterized <see cref="System.Threading.Channels.Channel{T}"/> object.
	/// </summary>
	/// <typeparam name="T">The type of the elements, the queue should hold.</typeparam>
	public class AsyncConsumerQueue<T> {
		private Channel<T> channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false });
		/// <summary>
		/// Adds the given item to the end of the queue. This may be called from any thread.
		/// </summary>
		/// <param name="item">The item to enqueue.</param>
		/// <exception cref="InvalidOperationException">When the queue is already closed because <see cref="Finish"/> was called on it.</exception>
		public void Enqueue(T item) {
			if (!channel.Writer.TryWrite(item)) {
				throw new InvalidOperationException("Can't enqueue to this queue object because it is already finished.");
			}
		}
		/// <summary>
		/// Returns an <see cref="IAsyncEnumerable{T}"/> that allows the consumer to read through the elements from the start of the queue asynchronously.
		/// The async iteration is suspended when all current elements are consumed and continues when new elements are <see cref="Enqueue(T)"/>ed.
		/// This must only be called once per queue and only by the consumer.
		/// </summary>
		/// <returns>An <see cref="IAsyncEnumerable{T}"/> iterating over the queue elements.</returns>
		public IAsyncEnumerable<T> DequeueAllAsync() {
			return channel.Reader.ReadAllAsync();
		}
		/// <summary>
		/// Closes the queue and ends the async iteration of the consumer after it has consumed the currently last element.
		/// No further elements can be enqueued afterwards.
		/// </summary>
		public void Finish() {
			channel.Writer.Complete();
		}
	}
}
