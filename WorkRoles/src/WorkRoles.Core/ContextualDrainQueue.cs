using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Deduplicated pending owners drained only by their active sim context.
    public sealed class ContextualDrainQueue<TOwner> where TOwner : class
    {
        private readonly HashSet<TOwner> pending;

        public ContextualDrainQueue(IEqualityComparer<TOwner> comparer = null)
        {
            pending = new HashSet<TOwner>(
                comparer ?? EqualityComparer<TOwner>.Default);
        }

        public int Count => pending.Count;

        public void Enqueue(TOwner owner)
        {
            if (owner != null) pending.Add(owner);
        }

        public List<TOwner> Drain(Func<TOwner, bool> belongsToContext)
        {
            if (belongsToContext == null)
                throw new ArgumentNullException(nameof(belongsToContext));
            var result = new List<TOwner>();
            foreach (TOwner owner in pending)
                if (belongsToContext(owner)) result.Add(owner);
            for (int i = 0; i < result.Count; i++) pending.Remove(result[i]);
            return result;
        }

        public void Clear() => pending.Clear();
    }
}
