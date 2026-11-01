using System.Collections.Generic;
using RimShared.Common;
using Verse;
using Verse.AI;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Rank of each managed pawn's current work job at the moment it was
    /// issued. Reconciliation compares against this to interrupt jobs whose
    /// work type lost standing after a role-state change. Exact giver authority
    /// comes from the job's native/preserved issue provenance. Entries are keyed
    /// to the job instance, so a stale entry is inert rather than wrong.
    /// Baselines are saved with the store: a loaded game, and a multiplayer
    /// joiner, must judge a resumed job against the same issue rank as the
    /// session that started it.
    internal static class JobRankBaseline
    {
        private struct Baseline
        {
            public Job Job;
            public int Rank;
        }

        // Owner: current world. Key: pawn identity, with exact job identity in
        // the value. Value: issue-time rank, never updated while that job runs.
        // Dependencies: StartJob and current compiled work-type rank only.
        // Refresh: synchronous StartJob notification, no ticking or polling.
        // Equality: an unchanged job/rank is equivalent; values have no identity.
        // Teardown: pawn release/destruction removes its entry; world clear
        // invokes ReleaseForTeardown. Loads restore saved ranks (RestoreAfterLoad).
        private static readonly Dictionary<Pawn, Baseline> baselines =
            new Dictionary<Pawn, Baseline>(ReferenceIdentityComparer<Pawn>.Instance);

        /// Save-time projection for the persisted pawns' running jobs, in
        /// thing-id order so equal state writes equal saves.
        internal static void CaptureForSave(ICollection<Pawn> persisted,
            List<Pawn> pawns, List<int> jobIds, List<int> ranks)
        {
            var captured = new List<Pawn>();
            foreach (var pair in baselines)
                if (persisted.Contains(pair.Key) && pair.Value.Job.loadID >= 0
                    && ReferenceEquals(pair.Key.jobs?.curJob, pair.Value.Job))
                    captured.Add(pair.Key);
            captured.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
            foreach (Pawn pawn in captured)
            {
                pawns.Add(pawn);
                jobIds.Add(baselines[pawn].Job.loadID);
                ranks.Add(baselines[pawn].Rank);
            }
        }

        /// Load-time restore inside FinalizeInit, before any tick. A saved rank
        /// applies only to the same job (by load id); legacy saves and jobs
        /// without a saved rank fall back to the current rank.
        internal static void RestoreAfterLoad(RoleStore store,
            List<Pawn>? pawns, List<int>? jobIds, List<int>? ranks)
        {
            int count = pawns == null || jobIds == null || ranks == null ? 0
                : System.Math.Min(pawns.Count, System.Math.Min(jobIds.Count, ranks.Count));
            foreach (var pawn in store.pawnSets.Keys)
            {
                NotifyJobStarted(pawn);
                var job = pawn.jobs?.curJob;
                if (job == null || !baselines.ContainsKey(pawn)) continue;
                for (int i = 0; i < count; i++)
                    if (ReferenceEquals(pawns![i], pawn) && jobIds![i] == job.loadID)
                    {
                        baselines[pawn] = new Baseline { Job = job, Rank = ranks![i] };
                        break;
                    }
            }
        }

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
                Job = job!, // non-null: workType came from job?.workGiverDef?.workType
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
