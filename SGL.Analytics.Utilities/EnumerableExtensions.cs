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
	}
}
