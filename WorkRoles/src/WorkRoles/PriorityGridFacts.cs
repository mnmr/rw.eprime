using System.Collections.Generic;
using RimShared.Common;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// <summary>
    /// Narrow event-driven revision source for mutable pawn facts projected only
    /// by the priority grid: skill levels/passions, disabled work types, and
    /// unmanaged vanilla work priorities.
    /// </summary>
    internal static class PriorityGridFacts
    {
        // Owner: current world, partitioned by pawn identity. Key: pawn reference.
        // Value: per-pawn invalidation revision (no render data). Dependencies:
        // displayed skill level/passion, disabled-work capability, and unmanaged
        // vanilla priority mutations. Refresh: immediate at authoritative mutation
        // boundaries. Equality: no-op mutations do not advance a revision.
        // Teardown: pawn destruction releases its entry; world teardown clears all.
        internal static readonly OwnerInvalidationRevisions<Pawn> Revisions =
            new OwnerInvalidationRevisions<Pawn>(
                ReferenceIdentityComparer<Pawn>.Instance);

        // Owner: all open priority-grid dialogs. Key: fixed pawn reference.
        // Value: dialog watch count. Dependencies: dialog open/close lifecycle.
        // Refresh: immediate acquire/release. Equality: duplicate acquisition
        // changes only the count. Teardown: pawn destruction or world teardown.
        private static readonly Dictionary<Pawn, int> watchers =
            new Dictionary<Pawn, int>(ReferenceIdentityComparer<Pawn>.Instance);

        internal static bool IsRelevant(Pawn pawn) => pawn != null
            && (pawn.IsColonist || pawn.IsSlaveOfColony
                || RoleStore.Current?.IsManaged(pawn) == true
                || watchers.ContainsKey(pawn));

        internal static void Acquire(Pawn pawn)
        {
            if (pawn == null) return;
            watchers.TryGetValue(pawn, out int count);
            watchers[pawn] = count + 1;
        }

        internal static void ReleaseWatch(Pawn pawn)
        {
            if (pawn == null || !watchers.TryGetValue(pawn, out int count))
                return;
            if (count <= 1) watchers.Remove(pawn);
            else watchers[pawn] = count - 1;
        }

        internal static void Invalidate(Pawn pawn)
        {
            if (IsRelevant(pawn)) Revisions.Invalidate(pawn);
        }

        internal static void Release(Pawn pawn)
        {
            if (pawn == null) return;
            watchers.Remove(pawn);
            Revisions.Release(pawn);
        }

        internal static void ReleaseForTeardown()
        {
            watchers.Clear();
            Revisions.InvalidateAll();
        }
    }
}
