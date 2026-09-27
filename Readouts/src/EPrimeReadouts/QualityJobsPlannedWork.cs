using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Map-indexed structure of QJA's authoritative job snapshot: which bills
    /// and construction targets QJA manages. The material those jobs still
    /// owe is live game state (deliveries, bill ingredient filters) that QJA
    /// does not republish, so it is read on every count pass instead
    /// (PlannedWorkCounts), never frozen into this projection.
    internal static class QualityJobsPlannedWork
    {
        private static readonly Func<QualityJobsBridge.ManagedJobsSnapshot,
            QualityJobsPlannedWorkSnapshot> build =
            PlannedWorkCounts.BuildQualityJobsSnapshot;

        // Cache contract:
        // Owner: the active world/store lifecycle.
        // Key: EPrime's immutable projection of the QJA snapshot, by reference.
        // Value: immutable per-map managed bills, targets and job handles.
        // Dependencies: all QJA fields consumed by QualityJobsBridge. Live
        //               material state is deliberately not a dependency: the
        //               count pass reads it through the job handles.
        // Refresh policy: immediate when QJA publishes a changed snapshot;
        //                 unchanged source references are allocation-free.
        // Equality policy: equal rebuilt jobs preserve projection identity.
        // Teardown: Reset on map removal and world teardown releases all QJA,
        //           map, bill and target references.
        private static readonly ReferenceProjectionCache<
            QualityJobsBridge.ManagedJobsSnapshot,
            QualityJobsPlannedWorkSnapshot> cache =
            new ReferenceProjectionCache<
                QualityJobsBridge.ManagedJobsSnapshot,
                QualityJobsPlannedWorkSnapshot>(build);

        internal static QualityJobsPlannedWorkSnapshot Current()
            => cache.Get(QualityJobsBridge.GetManagedJobs());

        internal static void Reset()
        {
            cache.Clear();
            QualityJobsBridge.Reset();
        }
    }

    internal sealed class QualityJobsPlannedWorkSnapshot
        : IEquatable<QualityJobsPlannedWorkSnapshot>
    {
        internal static readonly QualityJobsPlannedWorkSnapshot Empty =
            new QualityJobsPlannedWorkSnapshot(
                Array.Empty<QualityJobsMapWorkSnapshot>());

        private readonly Dictionary<Map, QualityJobsMapWorkSnapshot> byMap;

        internal QualityJobsPlannedWorkSnapshot(
            QualityJobsMapWorkSnapshot[] maps)
        {
            Maps = maps;
            byMap = new Dictionary<Map, QualityJobsMapWorkSnapshot>(
                maps.Length, IdentityComparer<Map>.Instance);
            for (int i = 0; i < maps.Length; i++)
                byMap.Add(maps[i].Map, maps[i]);
        }

        internal readonly QualityJobsMapWorkSnapshot[] Maps;

        internal QualityJobsMapWorkSnapshot? For(Map map)
        {
            byMap.TryGetValue(map, out QualityJobsMapWorkSnapshot? found);
            return found;
        }

        public bool Equals(QualityJobsPlannedWorkSnapshot? other)
        {
            if (other == null || Maps.Length != other.Maps.Length) return false;
            for (int i = 0; i < Maps.Length; i++)
                if (!Maps[i].Equals(other.Maps[i])) return false;
            return true;
        }

        public override bool Equals(object obj)
            => Equals(obj as QualityJobsPlannedWorkSnapshot);

        public override int GetHashCode() => Maps.Length;
    }

    internal sealed class QualityJobsMapWorkSnapshot
        : IEquatable<QualityJobsMapWorkSnapshot>
    {
        private readonly HashSet<Bill_Production> managedBills;
        private readonly HashSet<Thing> managedTargets;

        internal QualityJobsMapWorkSnapshot(
            Map map,
            QualityJobsBridge.ManagedBillJob[] billJobs,
            QualityJobsBridge.ManagedConstructionJob[] constructionJobs)
        {
            Map = map;
            BillJobs = billJobs;
            ConstructionJobs = constructionJobs;
            managedBills = new HashSet<Bill_Production>(
                IdentityComparer<Bill_Production>.Instance);
            for (int i = 0; i < billJobs.Length; i++)
                managedBills.Add(billJobs[i].Bill);
            managedTargets = new HashSet<Thing>(
                IdentityComparer<Thing>.Instance);
            for (int i = 0; i < constructionJobs.Length; i++)
            {
                Thing[] targets = constructionJobs[i].Targets;
                for (int target = 0; target < targets.Length; target++)
                    managedTargets.Add(targets[target]);
            }
        }

        internal readonly Map Map;
        internal readonly QualityJobsBridge.ManagedBillJob[] BillJobs;
        internal readonly QualityJobsBridge.ManagedConstructionJob[] ConstructionJobs;

        internal bool Contains(Bill_Production bill)
            => managedBills.Contains(bill);

        internal bool Contains(Thing target)
            => managedTargets.Contains(target);

        public bool Equals(QualityJobsMapWorkSnapshot? other)
        {
            if (other == null
                || !ReferenceEquals(Map, other.Map)
                || BillJobs.Length != other.BillJobs.Length
                || ConstructionJobs.Length != other.ConstructionJobs.Length)
                return false;
            for (int i = 0; i < BillJobs.Length; i++)
                if (!BillJobs[i].Equals(other.BillJobs[i])) return false;
            for (int i = 0; i < ConstructionJobs.Length; i++)
                if (!ConstructionJobs[i].Equals(other.ConstructionJobs[i]))
                    return false;
            return true;
        }

        public override bool Equals(object obj)
            => Equals(obj as QualityJobsMapWorkSnapshot);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(Map);
    }

    internal sealed class IdentityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        internal static readonly IdentityComparer<T> Instance =
            new IdentityComparer<T>();

        private IdentityComparer()
        {
        }

        public bool Equals(T left, T right) => ReferenceEquals(left, right);

        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }
}
