using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    public sealed class RecommendationSnapshot
    {
        private readonly RecommendationsTuningOptions options;
        private RecommendationPlan? plan;
        private IReadOnlyList<PawnFixTarget>? fixTargets;
        private IReadOnlyList<Dictionary<int, SignalBucket>>? suitability;
        private IReadOnlyList<Dictionary<int, SignalBucket>>? previousSuitability;

        internal RecommendationSnapshot(ColonyView colony,
            RecommendationsTuningOptions options, RecommendationSnapshot? previous)
        {
            Colony = colony;
            this.options = options;
            if (previous == null) return;
            if (ReferenceEquals(colony, previous.Colony)
                && RecommendationInputEquality.Options(options, previous.options))
                plan = previous.plan;
            if (RecommendationInputEquality.Suitability(colony, previous.Colony))
                suitability = previous.suitability;
            else
                previousSuitability = previous.suitability;
        }

        /// Detached producer-owned inputs: never mutate after publication.
        /// Assignment Enabled is deliberately excluded from plan inputs;
        /// apply/preview projections overlay current assignment state separately.
        public ColonyView Colony { get; }
        public RecommendationPlan Plan => plan ?? (plan = RecommendationPlan.Build(Colony, options));
        public IReadOnlyList<PawnFixTarget> FixTargets => fixTargets
            ?? (fixTargets = ColonyFixPlanner.Build(Colony, Plan));
        public IReadOnlyList<Dictionary<int, SignalBucket>> Suitability
        {
            get
            {
                if (suitability != null) return suitability;
                var rebuilt = RoleSuitability.Verdicts(Colony);
                suitability = previousSuitability != null
                    && RecommendationInputEquality.ReuseEqualVerdicts(previousSuitability, rebuilt)
                    ? previousSuitability : rebuilt;
                previousSuitability = null;
                return suitability;
            }
        }

        internal bool SameOptions(RecommendationsTuningOptions other)
            => RecommendationInputEquality.Options(options, other);
    }

    /// Owner: one window, partitioned by store identity. Key: exact ordered pawn
    /// identity cohort (at most two retained: listed pawns and recommendation map).
    /// Value: immutable-by-ownership detached colony, lazy plan and verdict maps.
    /// Dependencies: catalog identity; assignment membership/order/pin revision;
    /// external-facts generation; recommendation order revision; normalized tuning.
    /// Refresh: event-driven on a changed stamp, never polling authoritative data.
    /// Equality: equal consumed inputs preserve snapshot and plan identity; verdicts
    /// ignore assignment/order/activation/tuning and preserve equal result identity.
    /// Teardown: Reset/store change drops both cohorts and every prior owner reference.
    public sealed class RecommendationSnapshotCache<TPawn> where TPawn : class
    {
        private object? currentOwner;
        private Entry? recent;
        private Entry? other;

        public RecommendationSnapshot For(object owner, IReadOnlyList<TPawn> pawns,
            object catalog, int assignments, int external, int order,
            RecommendationsTuningOptions options,
            Func<IReadOnlyList<TPawn>, IReadOnlyList<PawnView>?, ColonyView> build)
        {
            if (!ReferenceEquals(currentOwner, owner))
            {
                Reset();
                currentOwner = owner;
            }
            Entry entry;
            if (recent != null && SameCohort(recent.Pawns, pawns))
                entry = recent;
            else if (other != null && SameCohort(other.Pawns, pawns))
            {
                entry = other;
                other = recent;
                recent = entry;
            }
            else
            {
                other = recent;
                entry = recent = new Entry(pawns);
            }

            RecommendationSnapshot? previous = entry.Snapshot;
            bool inputsCurrent = previous != null
                && ReferenceEquals(entry.Catalog, catalog)
                && entry.Assignments == assignments && entry.External == external
                && entry.Order == order;
            if (inputsCurrent && ReferenceEquals(entry.Options, options))
                return previous!;
            IReadOnlyList<PawnView>? reusablePawns = previous != null
                && entry.Assignments == assignments && entry.External == external
                    ? previous.Colony.Pawns : null;
            ColonyView colony = inputsCurrent ? previous!.Colony : build(pawns, reusablePawns);
            if (previous != null && !ReferenceEquals(colony, previous.Colony)
                && RecommendationInputEquality.Plan(colony, previous.Colony))
                colony = previous.Colony;
            RecommendationSnapshot result = previous != null
                && ReferenceEquals(colony, previous.Colony) && previous.SameOptions(options)
                    ? previous : new RecommendationSnapshot(colony, options, previous);
            entry.Catalog = catalog;
            entry.Assignments = assignments;
            entry.External = external;
            entry.Order = order;
            entry.Options = options;
            entry.Snapshot = result;
            return result;
        }

        public void Reset()
        {
            currentOwner = null;
            recent = null;
            other = null;
        }

        private static bool SameCohort(TPawn[] left, IReadOnlyList<TPawn> right)
        {
            if (left.Length != right.Count) return false;
            for (int index = 0; index < left.Length; index++)
                if (!ReferenceEquals(left[index], right[index])) return false;
            return true;
        }

        private sealed class Entry
        {
            internal Entry(IReadOnlyList<TPawn> pawns)
            {
                Pawns = new TPawn[pawns.Count];
                for (int index = 0; index < pawns.Count; index++) Pawns[index] = pawns[index];
            }
            internal readonly TPawn[] Pawns;
            internal object? Catalog;
            internal int Assignments;
            internal int External;
            internal int Order;
            internal RecommendationsTuningOptions? Options;
            internal RecommendationSnapshot? Snapshot;
        }
    }

    /// Exact comparisons run only behind the producer's invalidation gate.
    /// They compare retained facts as well as visible outputs because PlacementIndex
    /// and explanations continue to use the plan's original engine context.
    internal static class RecommendationInputEquality
    {
        internal static bool Plan(ColonyView left, ColonyView right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left.HunterRoleId != right.HunterRoleId
                || left.FireBlockerRoleId != right.FireBlockerRoleId
                || !Sequence(left.OrderTemplate, right.OrderTemplate)
                || !Dictionary(left.SkillMaxLevels, right.SkillMaxLevels)
                || left.Roles.Count != right.Roles.Count || left.Paths.Count != right.Paths.Count
                || left.Pawns.Count != right.Pawns.Count) return false;
            for (int index = 0; index < left.Roles.Count; index++)
                if (!Role(left.Roles[index], right.Roles[index])) return false;
            for (int index = 0; index < left.Paths.Count; index++)
            {
                PathView a = left.Paths[index], b = right.Paths[index];
                if (a.Id != b.Id || !Sequence(a.RoleIds, b.RoleIds)
                    || !Sequence(a.BandMins, b.BandMins) || !Sequence(a.BandMaxes, b.BandMaxes)) return false;
            }
            for (int index = 0; index < left.Pawns.Count; index++)
            {
                PawnView a = left.Pawns[index], b = right.Pawns[index];
                if (ReferenceEquals(a, b)) continue;
                if (a.BiologicalAgeTicks != b.BiologicalAgeTicks || a.AgeLimitsApply != b.AgeLimitsApply
                    || a.HasRangedWeapon != b.HasRangedWeapon || a.ShootingLevel != b.ShootingLevel
                    || a.FireFear != b.FireFear || a.Incapacitated != b.Incapacitated
                    || a.IsMechanitor != b.IsMechanitor || !a.CapableWorkTypes.SetEquals(b.CapableWorkTypes)
                    || !Dictionary(a.SkillLevels, b.SkillLevels) || !PawnSignals(a, b)
                    || a.Existing.Count != b.Existing.Count) return false;
                for (int assignment = 0; assignment < a.Existing.Count; assignment++)
                    if (a.Existing[assignment].RoleId != b.Existing[assignment].RoleId
                        || a.Existing[assignment].Pinned != b.Existing[assignment].Pinned) return false;
            }
            return true;
        }

        internal static bool Suitability(ColonyView left, ColonyView right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left.Roles.Count != right.Roles.Count || left.Pawns.Count != right.Pawns.Count) return false;
            for (int index = 0; index < left.Roles.Count; index++)
            {
                RoleView a = left.Roles[index], b = right.Roles[index];
                if (a.Id != b.Id || a.PartialCapability != b.PartialCapability
                    || !Sequence(a.MemberRoleIds, b.MemberRoleIds)
                    || !RoleWorkSpec.StructurallyEqual(a.WorkSpec, b.WorkSpec)) return false;
            }
            for (int index = 0; index < left.Pawns.Count; index++)
            {
                PawnView a = left.Pawns[index], b = right.Pawns[index];
                if (ReferenceEquals(a, b)) continue;
                // Suitability reads enabled-skill membership, never skill levels.
                if (a.SkillLevels.Count != b.SkillLevels.Count || !PawnSignals(a, b)) return false;
                foreach (string skill in a.SkillLevels.Keys)
                    if (!b.SkillLevels.ContainsKey(skill)) return false;
            }
            return true;
        }

        private static bool PawnSignals(PawnView a, PawnView b) =>
            Dictionary(a.SignalBuckets, b.SignalBuckets)
            && Dictionary(a.WorkTypeSignalBuckets, b.WorkTypeSignalBuckets);

        private static bool Role(RoleView a, RoleView b) => ReferenceEquals(a, b)
            || a.Id == b.Id && a.TemplateDefName == b.TemplateDefName
            && Sequence(a.MemberRoleIds, b.MemberRoleIds) && a.Coverage.SetEquals(b.Coverage)
            && Sequence(a.OrderedCoverage, b.OrderedCoverage)
            && a.AutoAssign == b.AutoAssign && a.HasRules == b.HasRules && a.Blocker == b.Blocker
            && a.PreserveRecommendationOrder == b.PreserveRecommendationOrder
            && a.ChampionPenalty == b.ChampionPenalty && a.PartialCapability == b.PartialCapability
            && a.Category == b.Category && a.Time == b.Time && a.MinAge == b.MinAge && a.MaxAge == b.MaxAge
            && a.ColonyMin == b.ColonyMin && a.CoveragePercent == b.CoveragePercent
            && a.Available == b.Available && a.Enabled == b.Enabled && a.RequiresMechanitor == b.RequiresMechanitor
            && RoleWorkSpec.StructurallyEqual(a.WorkSpec, b.WorkSpec);

        internal static bool Options(RecommendationsTuningOptions a, RecommendationsTuningOptions b)
        {
            if (ReferenceEquals(a, b)) return true;
            var descriptors = RecommendationsTuningOptions.Descriptors;
            for (int index = 0; index < descriptors.Count; index++)
                if (a.Get(descriptors[index].Option) != b.Get(descriptors[index].Option)) return false;
            return true;
        }

        internal static bool ReuseEqualVerdicts(IReadOnlyList<Dictionary<int, SignalBucket>> previous,
            List<Dictionary<int, SignalBucket>> rebuilt)
        {
            if (previous.Count != rebuilt.Count) return false;
            bool allEqual = true;
            for (int index = 0; index < previous.Count; index++)
            {
                // Only the unpublished list is changed. Equal pawn maps retain
                // their identity even when another pawn's result has changed.
                if (Dictionary(previous[index], rebuilt[index]))
                    rebuilt[index] = previous[index];
                else
                    allEqual = false;
            }
            return allEqual;
        }

        private static bool Sequence<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Count != right.Count) return false;
            var equality = EqualityComparer<T>.Default;
            for (int index = 0; index < left.Count; index++)
                if (!equality.Equals(left[index], right[index])) return false;
            return true;
        }

        private static bool Dictionary<TKey, TValue>(Dictionary<TKey, TValue>? left,
            Dictionary<TKey, TValue>? right) where TKey : notnull
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Count != right.Count) return false;
            var equality = EqualityComparer<TValue>.Default;
            foreach (var pair in left)
                if (!right.TryGetValue(pair.Key, out TValue value) || !equality.Equals(pair.Value, value)) return false;
            return true;
        }
    }
}
