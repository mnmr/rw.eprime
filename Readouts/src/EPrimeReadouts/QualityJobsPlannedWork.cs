using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Resource-keyed material projection of QJA's authoritative job snapshot.
    internal static class QualityJobsPlannedWork
    {
        private static readonly Func<QualityJobsBridge.ManagedJobsSnapshot,
            QualityJobsPlannedWorkSnapshot> build =
            PlannedWorkCounts.BuildQualityJobsSnapshot;

        // Cache contract:
        // Owner: the active world/store lifecycle.
        // Key: EPrime's immutable projection of the QJA snapshot, by reference.
        // Value: immutable map/resource-indexed planned-work data.
        // Dependencies: all QJA fields consumed by QualityJobsBridge plus the
        //               live bill/target material state QJA authoritatively
        //               exposes through that published snapshot.
        // Refresh policy: immediate when QJA publishes a changed snapshot;
        //                 unchanged source references are allocation-free.
        // Equality policy: equal rebuilt contents preserve projection identity.
        // Teardown: Reset on map removal and world teardown releases all QJA,
        //           map, bill, target, resource, and projected-row references.
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
            Bill_Production[] bills,
            Thing[] targets,
            QualityJobsResourceWork[] resources)
        {
            Map = map;
            Bills = bills;
            Targets = targets;
            Resources = resources;
            managedBills = new HashSet<Bill_Production>(
                IdentityComparer<Bill_Production>.Instance);
            for (int i = 0; i < bills.Length; i++)
                managedBills.Add(bills[i]);
            managedTargets = new HashSet<Thing>(
                IdentityComparer<Thing>.Instance);
            for (int i = 0; i < targets.Length; i++)
                managedTargets.Add(targets[i]);
        }

        internal readonly Map Map;
        internal readonly Bill_Production[] Bills;
        internal readonly Thing[] Targets;
        internal readonly QualityJobsResourceWork[] Resources;

        internal bool Contains(Bill_Production bill)
            => managedBills.Contains(bill);

        internal bool Contains(Thing target)
            => managedTargets.Contains(target);

        internal bool HasBills => Bills.Length != 0;

        internal bool HasBuildables => Targets.Length != 0;

        internal void Accumulate(
            CountAccumulator accumulator,
            bool reserveBills,
            bool reserveBuildables)
        {
            for (int resourceIndex = 0;
                 resourceIndex < Resources.Length;
                 resourceIndex++)
            {
                QualityJobsResourceWork resource = Resources[resourceIndex];
                QualityJobsWorkEntry[] entries = resource.Entries;
                for (int workIndex = 0; workIndex < entries.Length; workIndex++)
                {
                    QualityJobsWorkEntry entry = entries[workIndex];
                    if (entry.Kind == PlannedWorkKind.Bill)
                    {
                        if (!reserveBills) continue;
                        accumulator.AddBillWork(
                            resource.Resource.defName,
                            resource.Resource.shortHash,
                            entry.WorkDefName,
                            entry.Queued,
                            entry.UnitCost,
                            entry.Drain,
                            PlannedWorkSource.QualityJob);
                    }
                    else
                    {
                        if (!reserveBuildables) continue;
                        accumulator.AddBuildableWork(
                            resource.Resource.defName,
                            resource.Resource.shortHash,
                            entry.WorkDefName,
                            entry.StuffDefName,
                            entry.Queued,
                            entry.UnitCost,
                            entry.Drain,
                            PlannedWorkSource.QualityJob);
                    }
                }
            }
        }

        public bool Equals(QualityJobsMapWorkSnapshot? other)
        {
            if (other == null || !ReferenceEquals(Map, other.Map)) return false;
            return BillsEqual(other) && BuildablesEqual(other);
        }

        internal bool BillsEqual(QualityJobsMapWorkSnapshot? other)
        {
            if (other == null
                || !ReferenceEquals(Map, other.Map)
                || Bills.Length != other.Bills.Length)
                return false;
            for (int i = 0; i < Bills.Length; i++)
                if (!ReferenceEquals(Bills[i], other.Bills[i])) return false;
            return WorkEquals(other, PlannedWorkKind.Bill);
        }

        internal bool BuildablesEqual(QualityJobsMapWorkSnapshot? other)
        {
            if (other == null
                || !ReferenceEquals(Map, other.Map)
                || Targets.Length != other.Targets.Length)
                return false;
            for (int i = 0; i < Targets.Length; i++)
                if (!ReferenceEquals(Targets[i], other.Targets[i])) return false;
            return WorkEquals(other, PlannedWorkKind.Buildable);
        }

        private bool WorkEquals(
            QualityJobsMapWorkSnapshot other,
            PlannedWorkKind kind)
        {
            int left = NextResource(Resources, 0, kind);
            int right = NextResource(other.Resources, 0, kind);
            while (left < Resources.Length && right < other.Resources.Length)
            {
                QualityJobsResourceWork leftResource = Resources[left];
                QualityJobsResourceWork rightResource = other.Resources[right];
                if (!ReferenceEquals(
                        leftResource.Resource, rightResource.Resource)
                    || !EntriesEqual(
                        leftResource.Entries, rightResource.Entries, kind))
                    return false;
                left = NextResource(Resources, left + 1, kind);
                right = NextResource(other.Resources, right + 1, kind);
            }
            return left == Resources.Length && right == other.Resources.Length;
        }

        private static int NextResource(
            QualityJobsResourceWork[] resources,
            int start,
            PlannedWorkKind kind)
        {
            for (int resource = start; resource < resources.Length; resource++)
            {
                QualityJobsWorkEntry[] entries = resources[resource].Entries;
                for (int entry = 0; entry < entries.Length; entry++)
                    if (entries[entry].Kind == kind) return resource;
            }
            return resources.Length;
        }

        private static bool EntriesEqual(
            QualityJobsWorkEntry[] left,
            QualityJobsWorkEntry[] right,
            PlannedWorkKind kind)
        {
            int leftIndex = NextEntry(left, 0, kind);
            int rightIndex = NextEntry(right, 0, kind);
            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                if (!left[leftIndex].Equals(right[rightIndex])) return false;
                leftIndex = NextEntry(left, leftIndex + 1, kind);
                rightIndex = NextEntry(right, rightIndex + 1, kind);
            }
            return leftIndex == left.Length && rightIndex == right.Length;
        }

        private static int NextEntry(
            QualityJobsWorkEntry[] entries,
            int start,
            PlannedWorkKind kind)
        {
            for (int i = start; i < entries.Length; i++)
                if (entries[i].Kind == kind) return i;
            return entries.Length;
        }
    }

    internal sealed class QualityJobsResourceWork
        : IEquatable<QualityJobsResourceWork>
    {
        internal QualityJobsResourceWork(
            ThingDef resource,
            QualityJobsWorkEntry[] entries)
        {
            Resource = resource;
            Entries = entries;
        }

        internal readonly ThingDef Resource;
        internal readonly QualityJobsWorkEntry[] Entries;

        public bool Equals(QualityJobsResourceWork? other)
        {
            if (other == null
                || !ReferenceEquals(Resource, other.Resource)
                || Entries.Length != other.Entries.Length)
                return false;
            for (int i = 0; i < Entries.Length; i++)
                if (!Entries[i].Equals(other.Entries[i])) return false;
            return true;
        }
    }

    internal readonly struct QualityJobsWorkEntry
        : IEquatable<QualityJobsWorkEntry>
    {
        internal QualityJobsWorkEntry(
            PlannedWorkKind kind,
            string workDefName,
            string? stuffDefName,
            int queued,
            int unitCost,
            int drain)
        {
            Kind = kind;
            WorkDefName = workDefName;
            StuffDefName = stuffDefName;
            Queued = queued;
            UnitCost = unitCost;
            Drain = drain;
        }

        internal readonly PlannedWorkKind Kind;
        internal readonly string WorkDefName;
        internal readonly string? StuffDefName;
        internal readonly int Queued;
        internal readonly int UnitCost;
        internal readonly int Drain;

        public bool Equals(QualityJobsWorkEntry other)
            => Kind == other.Kind
               && string.Equals(WorkDefName, other.WorkDefName,
                   StringComparison.Ordinal)
               && string.Equals(StuffDefName, other.StuffDefName,
                   StringComparison.Ordinal)
               && Queued == other.Queued
               && UnitCost == other.UnitCost
               && Drain == other.Drain;
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
