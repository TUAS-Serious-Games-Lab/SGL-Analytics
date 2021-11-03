using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
	/// <summary>
	/// Wraps an <see cref="IEnumerable{T}"/> with <see cref="IDisposable"/> <c>T</c>s in an <see cref="IDisposable"/> object that also <see cref="IEnumerable{T}"/> and 
	/// disposes each of the elements of the original <see cref="IEnumerable{T}"/> when it is disposed.
	/// </summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	public class DisposableEnumerable<T> : IDisposable, IEnumerable<T> where T : IDisposable {
		private IEnumerable<T> enumerable;

		/// <summary>
		/// Constructs a new wrapper object holding the given <c>enumerable</c>.
		/// </summary>
		/// <param name="enumerable">The object to wrap.</param>
		public DisposableEnumerable(IEnumerable<T> enumerable) {
			this.enumerable = enumerable;
		}

		/// <summary>
		/// Disposes all elements of the wrapped <see cref="IEnumerable{T}"/>.
		/// </summary>
		public void Dispose() {
			foreach (var obj in enumerable) {
				obj.Dispose();
			}
		}

		/// <inheritdoc/>
		public IEnumerator<T> GetEnumerator() {
			return enumerable.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return ((IEnumerable)enumerable).GetEnumerator();
		}
	}

	/// <summary>
	/// Provides the <see cref="ToDisposableEnumerable{T}(IEnumerable{T})"/> extension method.
	/// </summary>
	public static class DisposableEnumerableExtensions {
		/// <summary>
		/// Transforms the <c>source</c> <see cref="IEnumerable{T}"/> to make it disposable in a way that disposes all elements when the transformed enumerable is disposed.
		/// </summary>
		/// <typeparam name="T">The type of the elements, must implement <see cref="IDisposable"/>.</typeparam>
		/// <param name="source">The enumerable to transform.</param>
		/// <returns>A wrapper object around <c>source</c> that disposes all elements when itself is disposed.</returns>
		public static DisposableEnumerable<T> ToDisposableEnumerable<T>(this IEnumerable<T> source) where T : IDisposable {
			return new DisposableEnumerable<T>(source);
		}
	}
}
