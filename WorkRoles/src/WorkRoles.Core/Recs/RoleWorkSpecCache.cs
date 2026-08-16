using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Identity-preserving per-role spec cache. The game-side catalog
    /// supplies the owner token (RoleStore), the narrow role-work revision,
    /// the job-profile index identity, and the builder; this class owns the
    /// contract mechanics so they are executable without the game runtime.
    ///
    /// - Owner: the caller-supplied owner token; an owner change drops every
    ///   previous entry (no identity crosses stores).
    /// - Key: stable role id within the current stamp.
    /// - Value: one immutable RoleWorkSpec per role.
    /// - Dependencies: the revision and the index identity; either change
    ///   invalidates the whole generation.
    /// - Refresh: immediate on the first read after a stamp change.
    /// - Equality: an equal rebuild republishes the previous instance
    ///   (RoleWorkSpec.StructurallyEqual).
    /// - Teardown: Reset clears entries and identity seeds; idempotent.
    public sealed class RoleWorkSpecCache
    {
        private object? owner;
        private int builtRevision = int.MinValue;
        private object? builtIndex;
        private Dictionary<int, RoleWorkSpec> specs =
            new Dictionary<int, RoleWorkSpec>();
        private Dictionary<int, RoleWorkSpec> previous =
            new Dictionary<int, RoleWorkSpec>();

        public void Reset()
        {
            owner = null;
            builtRevision = int.MinValue;
            builtIndex = null;
            specs.Clear();
            previous.Clear();
        }

        /// Builders may recurse into For for member roles: entries publish
        /// after the recursive build completes, within one stamp generation.
        public RoleWorkSpec For(
            int roleId,
            object currentOwner,
            int revision,
            object index,
            Func<RoleWorkSpec> build)
        {
            EnsureCurrent(currentOwner, revision, index);
            if (specs.TryGetValue(roleId, out RoleWorkSpec cached))
                return cached;
            RoleWorkSpec built = build?.Invoke() ?? RoleWorkSpec.Empty;
            if (previous.TryGetValue(roleId, out RoleWorkSpec prior)
                && RoleWorkSpec.StructurallyEqual(prior, built))
                built = prior;
            specs[roleId] = built;
            return built;
        }

        private void EnsureCurrent(
            object currentOwner, int revision, object index)
        {
            if (ReferenceEquals(owner, currentOwner)
                && builtRevision == revision
                && ReferenceEquals(builtIndex, index))
                return;
            // The previous owner's entries never seed identity preservation;
            // within one owner the last generation does.
            previous = ReferenceEquals(owner, currentOwner)
                ? specs
                : new Dictionary<int, RoleWorkSpec>();
            specs = new Dictionary<int, RoleWorkSpec>();
            owner = currentOwner;
            builtRevision = revision;
            builtIndex = index;
        }
    }
}
