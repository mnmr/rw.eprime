using System;
using System.Collections.Generic;
using RimShared.Common;

namespace WorkRoles.Core.Recs
{
    internal interface IRecommendationVerdictSource
    {
        IReadOnlyList<Dictionary<int, SignalBucket>> For(RecommendationSnapshot snapshot);
    }

    public sealed class RecommendationSnapshot
    {
        private readonly RecommendationsTuningOptions options;
        // Owner/key: this detached snapshot and its immutable Colony identity.
        // Value: shared immutable engine inputs for the lazy plan and suitability.
        // Dependencies: Colony only; tuning changes reuse the same context.
        // Refresh: first consumer builds once. Equality: an unchanged Colony
        // carries its existing context forward. Teardown: snapshot collection.
        private EngineContext? context;
        private RecommendationPlan? plan;
        private IReadOnlyList<PawnFixTarget>? fixTargets;
        private IReadOnlyList<Dictionary<int, SignalBucket>>? suitability;
        private IReadOnlyList<Dictionary<int, SignalBucket>>? previousSuitability;
        private readonly IRecommendationVerdictSource verdictSource;

        internal RecommendationSnapshot(ColonyView colony,
            RecommendationsTuningOptions options, RecommendationSnapshot? previous,
            IRecommendationVerdictSource verdictSource)
        {
            Colony = colony;
            this.options = options;
            this.verdictSource = verdictSource;
            if (previous == null) return;
            if (ReferenceEquals(colony, previous.Colony))
            {
                context = previous.CachedContext;
                if (RecommendationInputEquality.Options(options, previous.options)) plan = previous.plan;
            }
            if (RecommendationInputEquality.Suitability(colony, previous.Colony))
                suitability = previous.suitability;
            else
                previousSuitability = previous.suitability;
        }

        /// Detached producer-owned inputs: never mutate after publication.
        /// Assignment Enabled is deliberately excluded from plan inputs;
        /// apply/preview projections overlay current assignment state separately.
        public ColonyView Colony { get; }
        internal EngineContext? CachedContext => plan?.Context ?? context;
        internal EngineContext Context => CachedContext ?? (context = new EngineContext(Colony));
        public RecommendationPlan Plan => plan ?? (plan = RecommendationPlan.Build(Context, options));
        public IReadOnlyList<PawnFixTarget> FixTargets => fixTargets
            ?? (fixTargets = ColonyFixPlanner.Build(Colony, Plan));
        public IReadOnlyList<Dictionary<int, SignalBucket>> Suitability
        {
            get
            {
                if (suitability != null) return suitability;
                var rebuilt = verdictSource.For(this);
                suitability = previousSuitability != null
                    && RecommendationInputEquality.VerdictReferencesEqual(previousSuitability, rebuilt)
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
    /// Deferred reads of retired snapshots calculate detached results without
    /// publishing pawn entries into the current owner/cohort cache.
    public sealed class RecommendationSnapshotCache<TPawn> where TPawn : class
    {
        private object? currentOwner;
        private Entry? recent;
        private Entry? other;
        // Owner/key/teardown: same window/store and union of the two cohorts above.
        // Values: owned immutable pawn inputs and verdict maps. Projection keys
        // use the pawn's assignment revision and published external identity;
        // verdict keys consume only skill membership/signals and role work/gates.
        // Refresh: on cohort generation change; equal inputs/results retain identity.
        private readonly Dictionary<TPawn, PawnEntry> pawnEntries =
            new Dictionary<TPawn, PawnEntry>(ReferenceIdentityComparer<TPawn>.Instance);
        private readonly List<TPawn> removedPawns = new List<TPawn>();
        public PawnView PawnFor<TState>(object owner, TPawn pawn, int assignments,
            object external, TState state, Func<TState, PawnView> build)
        {
            EnsureOwner(owner);
            PawnEntry entry = PawnEntryFor(pawn);
            if (entry.Projection != null && entry.Assignments == assignments
                && ReferenceEquals(entry.External, external)) return entry.Projection;
            PawnView rebuilt = build(state);
            if (entry.Projection == null || !RecommendationInputEquality.Pawn(rebuilt, entry.Projection))
                entry.Projection = rebuilt;
            entry.Assignments = assignments;
            entry.External = external;
            return entry.Projection;
        }

        private void EnsureOwner(object owner)
        {
            if (ReferenceEquals(currentOwner, owner)) return;
            Reset();
            currentOwner = owner;
        }

        private PawnEntry PawnEntryFor(TPawn pawn)
        {
            if (!pawnEntries.TryGetValue(pawn, out PawnEntry entry))
                pawnEntries.Add(pawn, entry = new PawnEntry());
            return entry;
        }

        public RecommendationSnapshot For(object owner, IReadOnlyList<TPawn> pawns,
            object catalog, int assignments, int external, int order,
            RecommendationsTuningOptions options,
            Func<IReadOnlyList<TPawn>, IReadOnlyList<PawnView>?, ColonyView> build)
        {
            EnsureOwner(owner);
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
                entry = recent = new Entry(this, pawns);
                PrunePawns();
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
                    ? previous : new RecommendationSnapshot(colony, options, previous, entry.Verdicts);
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
            pawnEntries.Clear();
            removedPawns.Clear();
        }

        private void PrunePawns()
        {
            foreach (TPawn pawn in pawnEntries.Keys)
                if (!Contains(recent, pawn) && !Contains(other, pawn)) removedPawns.Add(pawn);
            for (int i = 0; i < removedPawns.Count; i++) pawnEntries.Remove(removedPawns[i]);
            removedPawns.Clear();
        }

        private static bool Contains(Entry? entry, TPawn pawn)
        {
            if (entry == null) return false;
            for (int i = 0; i < entry.Pawns.Length; i++)
                if (ReferenceEquals(entry.Pawns[i], pawn)) return true;
            return false;
        }

        private IReadOnlyList<Dictionary<int, SignalBucket>> VerdictsFor(TPawn[] pawns, RecommendationSnapshot snapshot)
        {
            ColonyView colony = snapshot.Colony;
            var result = new Dictionary<int, SignalBucket>[pawns.Length];
            for (int index = 0; index < pawns.Length; index++)
            {
                PawnEntry entry = PawnEntryFor(pawns[index]);
                PawnView input = colony.Pawns[index];
                bool same = entry.Verdicts != null && entry.VerdictPawn != null
                    && RecommendationInputEquality.PawnSuitability(input, entry.VerdictPawn)
                    && RecommendationInputEquality.RoleSuitability(colony.Roles, entry.VerdictRoles!);
                if (!same)
                {
                    var rebuilt = RoleSuitability.VerdictsForPawn(snapshot.Context, index);
                    if (entry.Verdicts == null || !RecommendationInputEquality.VerdictMap(entry.Verdicts, rebuilt))
                        entry.Verdicts = rebuilt;
                }
                entry.VerdictPawn = input;
                entry.VerdictRoles = colony.Roles;
                result[index] = entry.Verdicts!;
            }
            return result;
        }

        private sealed class PawnEntry
        {
            internal int Assignments;
            internal object? External;
            internal PawnView? Projection;
            internal PawnView? VerdictPawn;
            internal IReadOnlyList<RoleView>? VerdictRoles;
            internal Dictionary<int, SignalBucket>? Verdicts;
        }

        private sealed class VerdictSource : IRecommendationVerdictSource
        {
            private readonly RecommendationSnapshotCache<TPawn> owner;
            private readonly TPawn[] pawns;
            internal VerdictSource(RecommendationSnapshotCache<TPawn> owner, TPawn[] pawns)
            { this.owner = owner; this.pawns = pawns; }
            public IReadOnlyList<Dictionary<int, SignalBucket>> For(RecommendationSnapshot snapshot)
            {
                bool current = owner.recent != null && ReferenceEquals(owner.recent.Verdicts, this)
                        && ReferenceEquals(owner.recent.Snapshot, snapshot)
                    || owner.other != null && ReferenceEquals(owner.other.Verdicts, this)
                        && ReferenceEquals(owner.other.Snapshot, snapshot);
                return current ? owner.VerdictsFor(pawns, snapshot)
                    : RoleSuitability.Verdicts(snapshot.Context);
            }
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
            internal Entry(RecommendationSnapshotCache<TPawn> owner, IReadOnlyList<TPawn> pawns)
            {
                Pawns = new TPawn[pawns.Count];
                for (int index = 0; index < pawns.Count; index++) Pawns[index] = pawns[index];
                Verdicts = new VerdictSource(owner, Pawns);
            }
            internal readonly TPawn[] Pawns;
            internal readonly IRecommendationVerdictSource Verdicts;
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
                if (!Pawn(left.Pawns[index], right.Pawns[index])) return false;
            return true;
        }

        internal static bool Pawn(PawnView a, PawnView b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.BiologicalAgeTicks != b.BiologicalAgeTicks || a.AgeLimitsApply != b.AgeLimitsApply
                || a.HasRangedWeapon != b.HasRangedWeapon || a.ShootingLevel != b.ShootingLevel
                || a.FireFear != b.FireFear || a.Incapacitated != b.Incapacitated
                || a.IsMechanitor != b.IsMechanitor || !a.CapableWorkTypes.SetEquals(b.CapableWorkTypes)
                || !Dictionary(a.SkillLevels, b.SkillLevels) || !PawnSignals(a, b)
                || !a.WorkEvidence.ContentEquals(b.WorkEvidence)
                || a.Existing.Count != b.Existing.Count) return false;
            for (int index = 0; index < a.Existing.Count; index++)
                if (a.Existing[index].RoleId != b.Existing[index].RoleId
                    || a.Existing[index].Pinned != b.Existing[index].Pinned) return false;
            return true;
        }

        internal static bool Suitability(ColonyView left, ColonyView right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left.Pawns.Count != right.Pawns.Count || !RoleSuitability(left.Roles, right.Roles)) return false;
            for (int index = 0; index < left.Pawns.Count; index++)
                if (!PawnSuitability(left.Pawns[index], right.Pawns[index])) return false;
            return true;
        }

        internal static bool RoleSuitability(IReadOnlyList<RoleView> left, IReadOnlyList<RoleView> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
            {
                RoleView a = left[index], b = right[index];
                if (ReferenceEquals(a, b)) continue;
                if (a.Id != b.Id || a.PartialCapability != b.PartialCapability
                    || !Sequence(a.MemberRoleIds, b.MemberRoleIds)
                    || !RoleWorkSpec.StructurallyEqual(a.WorkSpec, b.WorkSpec)) return false;
            }
            return true;
        }

        internal static bool PawnSuitability(PawnView a, PawnView b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.SkillLevels.Count != b.SkillLevels.Count || !PawnSignals(a, b)) return false;
            foreach (string skill in a.SkillLevels.Keys)
                if (!b.SkillLevels.ContainsKey(skill)) return false;
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
            && a.ExcludedFromRecommendations == b.ExcludedFromRecommendations
            && RoleWorkSpec.StructurallyEqual(a.WorkSpec, b.WorkSpec);

        internal static bool Options(RecommendationsTuningOptions a, RecommendationsTuningOptions b)
        {
            if (ReferenceEquals(a, b)) return true;
            var descriptors = RecommendationsTuningOptions.Descriptors;
            for (int index = 0; index < descriptors.Count; index++)
                if (a.Get(descriptors[index].Option) != b.Get(descriptors[index].Option)) return false;
            return true;
        }

        internal static bool VerdictMap(Dictionary<int, SignalBucket> a, Dictionary<int, SignalBucket> b)
            => Dictionary(a, b);

        internal static bool VerdictReferencesEqual(IReadOnlyList<Dictionary<int, SignalBucket>> a,
            IReadOnlyList<Dictionary<int, SignalBucket>> b)
        {
            if (a.Count != b.Count) return false;
            for (int index = 0; index < a.Count; index++)
                if (!ReferenceEquals(a[index], b[index])) return false;
            return true;
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
