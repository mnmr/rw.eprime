using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>Creates one stable value per key until explicitly cleared.</summary>
    public sealed class MemoizedFactory<TKey, TValue>
    {
        private readonly Func<TKey, TValue> factory;
        private readonly Dictionary<TKey, TValue> values;

        public MemoizedFactory(Func<TKey, TValue> factory,
            IEqualityComparer<TKey> comparer = null)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            values = new Dictionary<TKey, TValue>(
                comparer ?? EqualityComparer<TKey>.Default);
        }

        public int Count => values.Count;

        public TValue For(TKey key)
        {
            if (!values.TryGetValue(key, out TValue value))
            {
                value = factory(key);
                values.Add(key, value);
            }
            return value;
        }

        public bool Remove(TKey key) => values.Remove(key);

        public void Clear() => values.Clear();
    }
}
