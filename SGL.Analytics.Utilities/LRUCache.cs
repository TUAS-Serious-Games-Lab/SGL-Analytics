using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.Analytics.Utilities {
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
				recentList.RemoveFirst();
				entryMap.Remove(node.Value.Key);
			}
		}

		public LRUCache(int capacity, Action<TValue> removingAction) {
			this.capacity = capacity;
			RemovingAction = removingAction;
		}

		public LRUCache(int capacity, bool autoDisposeOnDisplace = true) {
			this.capacity = capacity;
			if (autoDisposeOnDisplace) {
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

		public Action<TValue> RemovingAction { get; set; }

		public int Capacity {
			get => capacity;
			set {
				capacity = value;
				if (entryMap.Count > capacity) removeLeastRecent(entryMap.Count - capacity);
			}
		}

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

		public ICollection<TKey> Keys => entryMap.Keys;

		public class ValueCollection : ICollection<TValue> {
			private LRUCache<TKey, TValue> cache;

			public ValueCollection(LRUCache<TKey, TValue> cache) {
				this.cache = cache;
			}

			public int Count => cache.Count;

			public bool IsReadOnly => true;

			public void Add(TValue item) => throw new NotImplementedException();

			public void Clear() => throw new NotImplementedException();

			public bool Contains(TValue item) => cache.entryMap.Values.Any(v => object.Equals(item, v.Value.Value));

			public void CopyTo(TValue[] array, int arrayIndex) {
				foreach (var node in cache.entryMap.Values) {
					array[arrayIndex++] = node.Value.Value;
				}
			}

			public IEnumerator<TValue> GetEnumerator() {
				return cache.entryMap.Values.Select(node => node.Value.Value).GetEnumerator();
			}

			public bool Remove(TValue item) => throw new NotImplementedException();

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		public ICollection<TValue> Values {
			get {
				return new ValueCollection(this);
			}
		}

		public int Count => entryMap.Count;

		public bool IsReadOnly => false;

		public void Add(TKey key, TValue value) => add(key, value);

		public void Add(KeyValuePair<TKey, TValue> item) => add(item.Key, item.Value);

		public void Clear() {
			foreach (var entry in entryMap) {
				RemovingAction(entry.Value.Value.Value);
			}
			entryMap.Clear();
			recentList.Clear();
		}

		public bool Contains(KeyValuePair<TKey, TValue> item) => entryMap.TryGetValue(item.Key, out var node) && object.Equals(node.Value.Value, item.Value);

		public bool ContainsKey(TKey key) => entryMap.ContainsKey(key);

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
			foreach (var entry in this) {
				array[arrayIndex++] = entry;
			}
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => entryMap.Select(kn => new KeyValuePair<TKey, TValue>(kn.Key, kn.Value.Value.Value)).GetEnumerator();

		public bool Remove(TKey key) {
			if (entryMap.TryGetValue(key, out var node)) {
				recentList.Remove(node);
				return entryMap.Remove(key);
			}
			else {
				return false;
			}
		}

		public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

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
