using System.Collections.Generic;
using Verse;
using Verse.AI;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Rank of each managed pawn's current work job at the moment it was
    /// issued. Reconciliation compares against this to interrupt jobs whose
    /// work type lost standing after a role-state change. Entries are keyed
    /// to the job instance, so a stale entry is inert rather than wrong;
    /// jobs resumed from a save have no baseline (revoked-only handling).
    internal static class JobRankBaseline
    {
        private struct Baseline
        {
            public Job Job;
            public int Rank;
        }

        private static readonly Dictionary<Pawn, Baseline> baselines =
            new Dictionary<Pawn, Baseline>(ReferenceIdentityComparer<Pawn>.Instance);

        /// Runs from the StartJob postfix, inside the synced simulation.
        internal static void NotifyJobStarted(Pawn pawn)
        {
            if (pawn == null) return;
            var job = pawn.jobs?.curJob;
            var workType = job?.workGiverDef?.workType;
            if (workType == null || RoleStore.Current?.IsManaged(pawn) != true)
            {
                baselines.Remove(pawn);
                return;
            }
            baselines[pawn] = new Baseline
            {
                Job = job,
                Rank = CompiledJobOrders.PriorityFor(pawn, workType),
            };
        }

        internal static bool TryGetRank(Pawn pawn, Job job, out int rank)
        {
            rank = 0;
            if (pawn == null || job == null
                || !baselines.TryGetValue(pawn, out var baseline)
                || baseline.Job != job)
                return false;
            rank = baseline.Rank;
            return true;
        }

        internal static void NotifyDestroyed(Pawn pawn)
        {
            if (pawn != null) baselines.Remove(pawn);
        }

        internal static void ReleaseForTeardown() => baselines.Clear();
    }
}
