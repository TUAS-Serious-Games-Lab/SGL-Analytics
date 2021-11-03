using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
	/// <summary>
	/// Implements a caching data structure using the least-recently-used strategy, providing access through the <see cref="IDictionary{TKey, TValue}"/> interface.
	/// </summary>
	/// <typeparam name="TKey">The type for the keys, used to lookup entries.</typeparam>
	/// <typeparam name="TValue">The type for the actual values stored in the cache.</typeparam>
	public class LRUCache<TKey, TValue> : IDictionary<TKey, TValue> where TKey : notnull {
		private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> entryMap = new();
		private readonly LinkedList<(TKey Key, TValue Value)> recentList = new();
		private int capacity;

		private void use(LinkedListNode<(TKey Key, TValue Value)> entryNode) {
			recentList.Remove(entryNode);
			recentList.AddLast(entryNode);
		}

		private void add(TKey key, TValue value) {
			if (Count >= Capacity) {
				removeLeastRecent();
			}
			var node = new LinkedListNode<(TKey Key, TValue Value)>((key, value));
			entryMap.Add(key, node);
			recentList.AddLast(node);
		}

		private void removeLeastRecent(int count = 1) {
			for (int i = 0; i < count; ++i) {
				var node = recentList.First;
				if (node == null) throw new InvalidOperationException("Couldn't remove the required number of elements from the LRUCache, because it ran out of elements to remove and is now empty.");
				RemovingAction(node.Value.Value);
				recentList.RemoveFirst();
				entryMap.Remove(node.Value.Key);
			}
		}
		/// <summary>
		/// Creates a cache object with the given <c>capacity</c> and using the given action that is performed on a value when it is removed.
		/// </summary>
		/// <param name="capacity">The maximum number of elements allowed in the cache before displacing the least recently used one.</param>
		/// <param name="removingAction">A cleanup action that is invoked on values when they are removed.</param>
		public LRUCache(int capacity, Action<TValue> removingAction) {
			this.capacity = capacity;
			RemovingAction = removingAction;
		}

		/// <summary>
		/// Creates a cache object with the given <c>capacity</c> that automatically disposes removed objects if <c>autoDisposeOnRemove</c> is set to <see langword="true"/> and the objects are <see cref="IDisposable"/>, otherwise no action is performed on removed objects.
		/// </summary>
		/// <param name="capacity">The maximum number of elements allowed in the cache before displacing the least recently used one.</param>
		/// <param name="autoDisposeOnRemove"><see langword="true"/> if removed disposable objects should be disposed, <see langword="false"/> otherwise.</param>
		public LRUCache(int capacity, bool autoDisposeOnRemove = true) {
			this.capacity = capacity;
			if (autoDisposeOnRemove) {
				RemovingAction = v => {
					if (v is IDisposable d) {
						d.Dispose();
					}
				};
			}
			else {
				RemovingAction = v => { };
			}
		}

		/// <summary>
		/// The action that is performed on removed objects.
		/// </summary>
		public Action<TValue> RemovingAction { get; set; }

		/// <summary>
		/// The maximum number of elements allowed in the cache before displacing the least recently used one.
		/// </summary>
		public int Capacity {
			get => capacity;
			set {
				capacity = value;
				if (entryMap.Count > capacity) removeLeastRecent(entryMap.Count - capacity);
			}
		}

		/// <summary>
		/// Provides access to elements by key.
		/// For <see langword="get"/> access using a not present key, that is not present, a <see cref="KeyNotFoundException"/> is thrown.
		/// For <see langword="set"/> access using a not present key, the corresponding entry is created, possibly displacing the least recently used element to free capacity for it.
		/// The access performed using this member consitutes a 'use' in the sense of the least-recently-used strategy.
		/// </summary>
		/// <param name="key">The key to lookup the associated value.</param>
		/// <returns>The value associated with the given key.</returns>
		public TValue this[TKey key] {
			get {
				var node = entryMap[key];
				use(node);
				return node.Value.Value;
			}

			set {
				if (entryMap.TryGetValue(key, out var node)) {
					use(node);
					node.ValueRef.Value = value;
				}
				else {
					add(key, value);
				}
			}
		}

		/// <inheritdoc/>
		public ICollection<TKey> Keys => entryMap.Keys;

		/// <summary>
		/// Helper class, implementing a collection over all values in the cache.
		/// </summary>
		public class ValueCollection : ICollection<TValue> {
			private LRUCache<TKey, TValue> cache;

			/// <summary>
			/// Instantiates a collection working on the state of the given cache obejct.
			/// </summary>
			/// <param name="cache">The cache object to work on.</param>
			public ValueCollection(LRUCache<TKey, TValue> cache) {
				this.cache = cache;
			}

			/// <inheritdoc/>
			public int Count => cache.Count;

			/// <inheritdoc/>
			public bool IsReadOnly => true;

			/// <summary>
			/// Not implemented.
			/// </summary>
			public void Add(TValue item) => throw new NotImplementedException();

			/// <summary>
			/// Not implemented.
			/// </summary>
			public void Clear() => throw new NotImplementedException();

			/// <inheritdoc/>
			public bool Contains(TValue item) => cache.entryMap.Values.Any(v => object.Equals(item, v.Value.Value));

			/// <inheritdoc/>
			public void CopyTo(TValue[] array, int arrayIndex) {
				foreach (var node in cache.entryMap.Values) {
					array[arrayIndex++] = node.Value.Value;
				}
			}

			/// <inheritdoc/>
			public IEnumerator<TValue> GetEnumerator() {
				return cache.entryMap.Values.Select(node => node.Value.Value).GetEnumerator();
			}

			/// <summary>
			/// Not implemented.
			/// </summary>
			public bool Remove(TValue item) => throw new NotImplementedException();

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		/// <inheritdoc/>
		public ICollection<TValue> Values {
			get {
				return new ValueCollection(this);
			}
		}

		/// <inheritdoc/>
		public int Count => entryMap.Count;

		/// <summary>
		/// Returns false, because the cache supports writing.
		/// </summary>
		public bool IsReadOnly => false;

		/// <summary>
		/// Adds the given key and value to the cache, possibly displacing another entry using the least-recently-used strategy.
		/// </summary>
		/// <param name="key">The key for the given value.</param>
		/// <param name="value">The value to add.</param>
		/// <exception cref="ArgumentException">When an element with the given key is already present.</exception>
		public void Add(TKey key, TValue value) => add(key, value);

		/// <summary>
		/// Adds the given key-value-pair to the cache, possibly displacing another entry using the least-recently-used strategy.
		/// </summary>
		/// <param name="item">The key and value to add, bundled together.</param>
		/// <exception cref="ArgumentException">When an element with the given key is already present.</exception>
		public void Add(KeyValuePair<TKey, TValue> item) => add(item.Key, item.Value);

		/// <summary>
		/// Removes all current entries from the cache, making it empty.
		/// </summary>
		public void Clear() {
			foreach (var entry in entryMap) {
				RemovingAction(entry.Value.Value.Value);
			}
			entryMap.Clear();
			recentList.Clear();
		}

		/// <inheritdoc/>
		public bool Contains(KeyValuePair<TKey, TValue> item) => entryMap.TryGetValue(item.Key, out var node) && object.Equals(node.Value.Value, item.Value);

		/// <inheritdoc/>
		public bool ContainsKey(TKey key) => entryMap.ContainsKey(key);

		/// <inheritdoc/>
		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
			foreach (var entry in this) {
				array[arrayIndex++] = entry;
			}
		}

		/// <inheritdoc/>
		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => entryMap.Select(kn => new KeyValuePair<TKey, TValue>(kn.Key, kn.Value.Value.Value)).GetEnumerator();

		/// <summary>
		/// Removes the entry with the given key from the cache.
		/// </summary>
		/// <param name="key">The key to remove.</param>
		/// <returns><see langword="true"/> if the entry existed and was removed, <see langword="false"/> if it was not present.</returns>
		public bool Remove(TKey key) {
			if (entryMap.TryGetValue(key, out var node)) {
				RemovingAction(node.Value.Value);
				recentList.Remove(node);
				return entryMap.Remove(key);
			}
			else {
				return false;
			}
		}

		/// <summary>
		/// Removes the entry with the given key contained in the given key-value-pair from the cache.
		/// </summary>
		/// <returns><see langword="true"/> if the entry existed and was removed, <see langword="false"/> if it was not present.</returns>
		public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

		/// <summary>
		/// Lookup the given key in the cache and set <c>value</c> to the associated cached value if it is present.
		/// Otherwise <c>value</c> is set to <c>default(TValue)</c>.
		/// The access performed using this member consitutes a 'use' in the sense of the least-recently-used strategy.
		/// </summary>
		/// <param name="key">The key to lookup.</param>
		/// <param name="value">The variable into which the value should be read if it is present.</param>
		/// <returns><see langword="true"/> if the entry exists and its associated value was read into <c>value</c>, <see langword="false"/> if it doesn't.</returns>
		public bool TryGetValue(TKey key, out TValue value) {
			if (entryMap.TryGetValue(key, out var node)) {
				use(node);
				value = node.Value.Value;
				return true;
			}
			else {
				value = default!;
				return false;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
