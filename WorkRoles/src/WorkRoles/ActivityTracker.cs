using System.Collections.Generic;
using Verse;

namespace WorkRoles
{
    /// Per-pawn job-transition revisions, bumped by the Pawn_JobTracker patches.
    /// UI-side only: consumers compare revisions to re-resolve cached activity.
    /// Gated to an open WorkRoles window so a closed window costs nothing and
    /// retains no pawn references.
    internal static class ActivityTracker
    {
        private static readonly Dictionary<Pawn, int> revisions = new Dictionary<Pawn, int>();
        private static bool enabled;

        internal static void Enable() => enabled = true;

        internal static void Disable()
        {
            enabled = false;
            revisions.Clear();
        }

        internal static void ReleaseForTeardown() => Disable();

        internal static void NotifyJobChanged(Pawn pawn)
        {
            if (!enabled || pawn == null) return;
            if (!pawn.IsColonist && !pawn.IsSlaveOfColony) return;
            revisions.TryGetValue(pawn, out int revision);
            revisions[pawn] = revision + 1;
        }

        internal static int RevisionOf(Pawn pawn) =>
            pawn != null && revisions.TryGetValue(pawn, out int revision) ? revision : 0;
    }
}
