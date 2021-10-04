using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
	public static class EnumerableExtensions {
		public static async IAsyncEnumerable<TResult> BatchAsync<TSource, TResult>(this IEnumerable<TSource> source, int batchSize, Func<IEnumerable<TSource>, IEnumerable<Task<TResult>>> batchFunc, [EnumeratorCancellation] CancellationToken ct = default) {
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

		public static async Task<IList<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			var list = new List<T>();
			await foreach (var val in source.WithCancellation(ct))
			{
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
