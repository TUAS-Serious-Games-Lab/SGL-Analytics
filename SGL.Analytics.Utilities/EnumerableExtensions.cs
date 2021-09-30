using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
	public static class EnumerableExtensions {

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
