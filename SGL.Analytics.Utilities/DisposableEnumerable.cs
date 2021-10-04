using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
	public class DisposableEnumerable<T> : IDisposable, IEnumerable<T> where T : IDisposable {
		private IEnumerable<T> enumerable;

		public DisposableEnumerable(IEnumerable<T> enumerable) {
			this.enumerable = enumerable;
		}

		public void Dispose() {
			foreach (var obj in enumerable) {
				obj.Dispose();
			}
		}

		public IEnumerator<T> GetEnumerator() {
			return enumerable.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return ((IEnumerable)enumerable).GetEnumerator();
		}
	}

	public static class DisposableEnumerableExtensions {
		public static DisposableEnumerable<T> ToDisposableEnumerable<T>(this IEnumerable<T> source) where T : IDisposable {
			return new DisposableEnumerable<T>(source);
		}
	}
}
