using System;
using System.Collections.Generic;

namespace QualityJobs.Core
{
    /// <summary>
    /// Window-local target identities that follow deterministic game-object
    /// replacement while preserving the original primary/secondary ordering.
    /// </summary>
    public sealed class TrackedTargetIds
    {
        private readonly int[] ids;

        public TrackedTargetIds(IReadOnlyList<int> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Count == 0)
                throw new ArgumentException("At least one target is required.", nameof(source));

            ids = new int[source.Count];
            for (int i = 0; i < source.Count; i++) ids[i] = source[i];
        }

        public int Count => ids.Length;

        public int Primary => ids[0];

        public int this[int index] => ids[index];

        public bool Retarget(int previousId, int replacementId)
        {
            bool changed = false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] != previousId) continue;
                ids[i] = replacementId;
                changed = true;
            }
            return changed;
        }
    }
}
