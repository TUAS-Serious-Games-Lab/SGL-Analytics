using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {

	/// <summary>
	/// Provides utility extension methods for working with enumerables.
	/// </summary>
	public static class EnumerableExtensions {
		/// <summary>
		/// Processes the elements of <c>source</c> in batches of size <c>batchSize</c> by invoking <c>batchFunc</c> for each batch and enables asynchronous enumeration of the results.
		/// </summary>
		/// <typeparam name="TSource">The type of the inputs.</typeparam>
		/// <typeparam name="TResult">The type of the produced results.</typeparam>
		/// <param name="source">An enumerable containing the input elements.</param>
		/// <param name="batchSize">The maximum size of the batches passed to <c>batchFunc</c>.</param>
		/// <param name="batchFunc">A function object that is callable with an <c><![CDATA[IEnumerable<TSource>]]></c> and that returns an <c><![CDATA[IEnumerable<Task<TResult>>]]></c> representing the asynchronous processing results for the elements.</param>
		/// <param name="ct">A cancellation token to cancel the batching logic.
		/// Usually, this token should also be used in the lambda passed to <c>batchFunc</c> to also cancel the asynchronous operations initiated there.</param>
		/// <returns>An <c><![CDATA[IAsyncEnumerable<TResult>]]></c> allowing asynchronous enumeration of the awaited results of the operations started in the batches.</returns>
		/// <remarks>This method is useful to concurrently perform asynchronous operations on many elements while enforcing a maximum concurreny limit to prevent ressource exhaustion.</remarks>
		public static async IAsyncEnumerable<TResult> BatchAsync<TSource, TResult>(this IEnumerable<TSource> source, int batchSize,
			Func<IEnumerable<TSource>, IEnumerable<Task<TResult>>> batchFunc, [EnumeratorCancellation] CancellationToken ct = default) {
			static bool getBatch(ref IEnumerable<TSource> source, int batchSize, out IEnumerable<TSource> batch, CancellationToken ct = default) {
				batch = source.Take(batchSize);
				source = source.Skip(batchSize);
				ct.ThrowIfCancellationRequested();
				return batch.Any();
			}

			IEnumerable<TResult>? results = null;
			while (getBatch(ref source, batchSize, out var batch, ct)) {
				var batchTask = Task.WhenAll(batchFunc(batch));
				if (results != null) {
					foreach (var res in results) {
						ct.ThrowIfCancellationRequested();
						yield return res;
					}
				}
				ct.ThrowIfCancellationRequested();
				results = await batchTask;
			}
			if (results != null) {
				foreach (var res in results) {
					ct.ThrowIfCancellationRequested();
					yield return res;
				}
			}
		}
		/// <summary>
		/// Maps the elemets from <c>source</c> using an asynchronous operation by buffering pending tasks up to a given buffer size.
		/// When the buffer is full, pending tasks are awaited in their input order and their results are transferred to an output buffer to make room for the next operations.
		/// The returned object allows asynchronously enumerating the output buffer to consume completed results when they become available.
		/// </summary>
		/// <typeparam name="TSource">The type of the inputs</typeparam>
		/// <typeparam name="TResult">The type of the outputs</typeparam>
		/// <param name="source">An enumerable containing the input elements.</param>
		/// <param name="bufferSize">The size of the buffers, limiting the concurrency.</param>
		/// <param name="mapFunc">A function object implementing the asynchronous operation, taking an input element and returning a <see cref="Task{TResult}"/> for waiting for the result.</param>
		/// <param name="ct">A cancellation token to cancel the mapping logic.
		/// Usually, this token should also be used in the lambda passed to <c>mapFunc</c> to also cancel the asynchronous operations initiated there.</param>
		/// <returns>An <c><![CDATA[IAsyncEnumerable<TResult>]]></c> allowing asynchronous enumeration of the awaited results of the operations started in <c>mapFunc</c>.</returns>
		/// <remarks>This method is useful to concurrently perform asynchronous operations on many elements while enforcing a maximum concurreny limit to prevent ressource exhaustion.</remarks>
		public static async IAsyncEnumerable<TResult> MapBufferedAsync<TSource, TResult>(this IEnumerable<TSource> source, int bufferSize, Func<TSource, Task<TResult>> mapFunc, [EnumeratorCancellation] CancellationToken ct = default) {
			var mapBuffer = new Queue<Task<TResult>>();
			var yieldBuffer = new Queue<TResult>();
			var sourceEnumerator = source.GetEnumerator();
			var sourceAvailable = sourceEnumerator.MoveNext();
			// Iterate as long as we have anything that will eventually be yield returned.
			while (sourceAvailable || mapBuffer.Count > 0 || yieldBuffer.Count > 0) {
				ct.ThrowIfCancellationRequested();
				bool progress = false;
				// Map source elements if available and mapBuffer is not full.
				while (sourceAvailable && mapBuffer.Count < bufferSize) {
					progress = true;
					mapBuffer.Enqueue(mapFunc(sourceEnumerator.Current));
					sourceAvailable = sourceEnumerator.MoveNext();
				}
				// Transfer completed Tasks to yieldBuffer if it isn't full yet.
				while (yieldBuffer.Count < bufferSize && mapBuffer.TryPeek(out var peekTask) && peekTask.IsCompleted) {
					progress = true;
					yieldBuffer.Enqueue(await mapBuffer.Dequeue()); // Unwrap using await, doesn't suspend beacuse the task is ready.
				}
				// yield return element from yieldBuffer if available.
				// No loop, because we should re-evaluate the above loops after the suspension by yield return.
				// So we simply do another round of the outer loop to get to the next yield return.
				if (yieldBuffer.TryDequeue(out var result)) {
					progress = true;
					yield return result;
				}
				// if we made no progress at all in this iteration and the yieldBuffer is not full, await unfinished Task from mapBuffer if available.
				// This is the point where suspension for waiting for results happens.
				if (!progress && yieldBuffer.Count < bufferSize && mapBuffer.TryDequeue(out var dequeuedTask)) yieldBuffer.Enqueue(await dequeuedTask);
			}
		}
		/// <summary>
		/// Provides the asynchronous counterpart of <see cref="Enumerable.ToList{TSource}(IEnumerable{TSource})"/> by collecting all elements from the
		/// <c><![CDATA[IAsyncEnumerable<TResult> source]]></c> into a <see cref="List{T}"/> and finishing when all elements have been collected.
		/// </summary>
		/// <typeparam name="T">The type of the elements.</typeparam>
		/// <param name="source">The async enumerable to collect.</param>
		/// <param name="ct">A cancellation token to cancel the collection process.</param>
		/// <returns>A <see cref="Task{TResult}"/> representing the resulting list, allowing calling code to wait for the completion of the collection process.</returns>
		public static async Task<IList<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default) {
			var list = new List<T>();
			await foreach (var val in source.WithCancellation(ct)) {
				list.Add(val);
			}
			return list;
		}
		/// <summary>
		/// Behaves similarly to SingleOrDefault known from Linq, but does not throw an exception if source has more than one element, but instead returns the default value as well, like for the empty source case.
		/// </summary>
		/// <typeparam name="TSource">The type of the elements of the source.</typeparam>
		/// <param name="source">A sequence of values</param>
		/// <returns>The element of source if source has exactly one element, default(TSource) otherwise.</returns>
		public static TSource? SingleOrDefaultNoExcept<TSource>(this IEnumerable<TSource> source) {
			var enumerator = source.GetEnumerator();
			if (!enumerator.MoveNext()) return default(TSource);
			var value = enumerator.Current;
			if (enumerator.MoveNext()) return default(TSource);
			return value;
		}
	}
}
