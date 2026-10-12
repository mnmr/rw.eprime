using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Owner: supplied store identity. Key: role id within that store.
    /// Value: immutable RoleWorkSpec. Dependencies: that role's work revision
    /// and the definition-index identity. Refresh: immediate on the next read.
    /// Equality: equal rebuilt contents preserve the previous spec identity.
    /// Teardown: Remove drops deleted roles; Reset/owner replacement clears all.
    public sealed class RoleWorkSpecCache
    {
        private sealed class Entry
        {
            internal int Revision;
            internal object Index = null!;
            internal RoleWorkSpec Spec = null!;
        }
        private object? owner;
        private readonly Dictionary<int, Entry> specs = new Dictionary<int, Entry>();
        public void Reset() { owner = null; specs.Clear(); }
        public void Remove(int roleId) => specs.Remove(roleId);
        public RoleWorkSpec For(int roleId, object currentOwner, int revision,
            object index, Func<RoleWorkSpec> build) =>
            For(roleId, currentOwner, revision, index, build, InvokeFactory);
        private static readonly Func<Func<RoleWorkSpec>, RoleWorkSpec> InvokeFactory = build => build();

        /// Passing a context plus a cached static builder avoids a capturing
        /// factory allocation on every cache hit. Recursive member builds work
        /// because publication happens only after the builder returns.
        public RoleWorkSpec For<T>(int roleId, object currentOwner, int revision,
            object index, T context, Func<T, RoleWorkSpec> build)
        {
            if (!ReferenceEquals(owner, currentOwner))
            { specs.Clear(); owner = currentOwner; }
            if (specs.TryGetValue(roleId, out Entry entry)
                && entry.Revision == revision && ReferenceEquals(entry.Index, index))
                return entry.Spec;
            RoleWorkSpec built = build(context) ?? RoleWorkSpec.Empty;
            if (entry != null && RoleWorkSpec.StructurallyEqual(entry.Spec, built)) built = entry.Spec;
            specs[roleId] = new Entry { Revision = revision, Index = index, Spec = built };
            return built;
        }
    }
}
