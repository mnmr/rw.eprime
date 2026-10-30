using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    internal sealed partial class PawnDraft
    {
        private const byte DirectSource = 1;
        private const byte PathSource = 2;

        private readonly List<int> roleIds = new List<int>();
        private readonly List<byte> roleSources = new List<byte>();
        private readonly List<byte> roleMinimumBonuses = new List<byte>();
        private readonly List<bool> roleMinimumPicks = new List<bool>();
        private readonly List<bool> roleChampionPicks = new List<bool>();
        private readonly List<PathActivation> activations =
            new List<PathActivation>();

        internal void AddRole(
            int roleId,
            byte source = DirectSource,
            byte minimumBonus = 0,
            bool minimumPick = false)
        {
            int index = roleIds.IndexOf(roleId);
            if (index >= 0)
            {
                roleSources[index] |= source;
                if (minimumBonus > roleMinimumBonuses[index])
                    roleMinimumBonuses[index] = minimumBonus;
                if (minimumPick) roleMinimumPicks[index] = true;
                return;
            }
            roleIds.Add(roleId);
            roleSources.Add(source);
            roleMinimumBonuses.Add(minimumBonus);
            roleMinimumPicks.Add(minimumPick);
            roleChampionPicks.Add(false);
        }

        internal bool ContainsRole(int roleId) => roleIds.Contains(roleId);

        internal void RemoveRole(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            if (index < 0) return;
            roleIds.RemoveAt(index);
            roleSources.RemoveAt(index);
            roleMinimumBonuses.RemoveAt(index);
            roleMinimumPicks.RemoveAt(index);
            roleChampionPicks.RemoveAt(index);
        }

        internal void MarkChampionPick(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            if (index >= 0) roleChampionPicks[index] = true;
        }

        internal bool IsChampionPick(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            return index >= 0 && roleChampionPicks[index];
        }

        internal bool IsPathRole(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            return index >= 0 && (roleSources[index] & PathSource) != 0;
        }

        internal bool HasDirectRole(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            return index >= 0 && (roleSources[index] & DirectSource) != 0;
        }

        private byte MinimumBonusOf(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            return index < 0 ? (byte)0 : roleMinimumBonuses[index];
        }

        internal bool IsMinimumRole(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            return index >= 0 && roleMinimumPicks[index];
        }

        internal int TrainingTargetFor(int roleId)
        {
            for (int activationIndex = 0;
                 activationIndex < activations.Count;
                 activationIndex++)
            {
                PathActivation activation = activations[activationIndex];
                if (activation.TargetRoleId == roleId) continue;
                for (int roleIndex = 0;
                     roleIndex < activation.ActiveRoleIds.Length;
                     roleIndex++)
                    if (activation.ActiveRoleIds[roleIndex] == roleId)
                        return activation.TargetRoleId;
            }
            return -1;
        }

        internal PathActivation? ActivationToward(int targetRoleId)
        {
            for (int index = 0; index < activations.Count; index++)
                if (activations[index].TargetRoleId == targetRoleId)
                    return activations[index];
            return null;
        }

        internal int RoleCount => roleIds.Count;
        internal int RoleAt(int index) => roleIds[index];
        private HashSet<int>? retainedLearning;
        internal bool RetainedLearning(int roleId) => retainedLearning?.Contains(roleId) == true;

        internal void RetainLearning(PawnDraft baseline, EngineContext facts, int pawnIndex)
        {
            foreach (PathActivation activation in baseline.activations)
            {
                PathView path = facts.PathsById[activation.PathId];
                int targetIndex = path.RoleIds.IndexOf(activation.TargetRoleId);
                if (targetIndex < 0) continue;
                int minimum = path.BandMins[targetIndex];
                var kept = new List<int>();
                foreach (int id in activation.ActiveRoleIds)
                {
                    if (id == activation.TargetRoleId || !baseline.ContainsRole(id)) continue;
                    RoleView role = facts.RoleOf(id);
                    foreach (string skill in facts.PathSkills(path).Qualifying)
                        if (facts.SkillLevel(pawnIndex, skill) < minimum
                            && facts.CanTrain(pawnIndex, role, skill))
                        { kept.Add(id); break; }
                }
                if (kept.Count > 0)
                {
                    foreach (int id in kept)
                        if (TrainingTargetFor(id) != activation.TargetRoleId)
                            (retainedLearning ??= new HashSet<int>()).Add(id);
                    AddActivation(activation.WithRoles(kept.ToArray()));
                }
            }
        }

        internal void AddActivation(
            PathActivation activation,
            byte minimumBonus = 0,
            bool minimumPick = false)
        {
            bool found = false;
            for (int index = 0; index < activations.Count; index++)
                if (activations[index].PathId == activation.PathId)
                {
                    found = true;
                    break;
                }
            if (!found) activations.Add(activation);
            for (int index = 0; index < activation.ActiveRoleIds.Length; index++)
                AddRole(
                    activation.ActiveRoleIds[index],
                    PathSource,
                    minimumBonus,
                    minimumPick);
        }

        internal int[] PublishRoles(
            EngineContext facts,
            int pawnIndex,
            IReadOnlyDictionary<int, long> positions,
            RecommendationFormulaEngine formulas,
            out int[] pathIds,
            out int activatedPathCount,
            RoleOrderingStrategy ordering = RoleOrderingStrategy.Current)
        {
            if (ordering == RoleOrderingStrategy.Experimental)
                return PublishRolesExperimental(
                    facts, pawnIndex, positions, formulas, out pathIds, out activatedPathCount);
            List<PathView> placements = OrderingPaths(facts);
            activatedPathCount = activations.Count;
            pathIds = new int[placements.Count];
            for (int index = 0; index < placements.Count; index++)
                pathIds[index] = placements[index].Id;

            int count = roleIds.Count;
            if (count < 2) return roleIds.ToArray();
            var keys = new long[count];
            for (int index = 0; index < count; index++)
                keys[index] = positions[roleIds[index]];
            ApplySpecialKeys(facts, keys);

            // Path targets sit at their own resolved recommendation-order
            // position: the order template is the single placement authority
            // (the retired per-path anchor used to override this).
            var targetKeys = new long[placements.Count];
            for (int placementIndex = 0;
                 placementIndex < placements.Count;
                 placementIndex++)
                targetKeys[placementIndex] = positions[
                    PathActivation.UniqueTargetRoleId(placements[placementIndex])];

            for (int placementIndex = 0;
                 placementIndex < placements.Count;
                 placementIndex++)
            {
                PathView path = placements[placementIndex];
                int targetRoleId = PathActivation.UniqueTargetRoleId(path);
                int targetAt = roleIds.IndexOf(targetRoleId);
                if (targetAt >= 0) keys[targetAt] = targetKeys[placementIndex];
                for (int roleAt = 0; roleAt < count; roleAt++)
                {
                    int roleId = roleIds[roleAt];
                    if (roleId != targetRoleId
                        && OrdersMember(path, targetRoleId, roleId)
                        && keys[roleAt] <= targetKeys[placementIndex])
                        keys[roleAt] = targetKeys[placementIndex] + 1;
                }
            }

            var result = new int[count];
            var emitted = new bool[count];
            for (int outputIndex = 0; outputIndex < count; outputIndex++)
            {
                int next = BestAvailableRole(emitted, keys, positions);
                emitted[next] = true;
                result[outputIndex] = roleIds[next];
            }
            var scores = new int[count];
            for (int roleAt = 0; roleAt < count; roleAt++)
                scores[roleAt] = OrderingScore(
                    facts, pawnIndex, roleIds[roleAt], formulas);
            int[] trimmed = WithoutPlacedHunter(facts, pawnIndex, result);
            int[] ordered = Ordering.PreserveProtectedOrder(
                facts, pawnIndex, trimmed);
            OrderByScore(
                facts, pawnIndex, positions, scores, ordered);
            if (!ReferenceEquals(trimmed, result))
                ordered = PlaceHunter(facts, pawnIndex, ordered);
            SlideHunterPastConditionalRoles(facts, pawnIndex, ordered);
            return ordered;
        }

        // Placeholder for the redesigned role ordering, wired in parallel to the current path so tests can build both and diff.
        // For now it reuses the current path placement and emits roles by base recommendation position; the real algorithm is designed next.
        private int[] PublishRolesExperimental(
            EngineContext facts,
            int pawnIndex,
            IReadOnlyDictionary<int, long> positions,
            RecommendationFormulaEngine formulas,
            out int[] pathIds,
            out int activatedPathCount)
        {
            List<PathView> placements = OrderingPaths(facts);
            activatedPathCount = activations.Count;
            pathIds = new int[placements.Count];
            for (int index = 0; index < placements.Count; index++)
                pathIds[index] = placements[index].Id;

            int[] ordered = roleIds.ToArray();
            System.Array.Sort(ordered, (left, right) => positions[left].CompareTo(positions[right]));
            return ordered;
        }

        // Prefer newly suggested conditional/blocker roles before Hunter,
        // within the boundaries of the player's protected assignment order.
        private static void SlideHunterPastConditionalRoles(EngineContext facts, int pawnIndex, int[] ordered)
        {
            int hunterRoleId = facts.Colony.HunterRoleId;
            if (hunterRoleId < 0) return;
            // Placement conveniences cannot undo an authored pin or the
            // relative order protected by an existing rule/blocker assignment.
            if (facts.Colony.OrderTemplate.Contains(hunterRoleId)
                || facts.HasProtectedDirectAssignment(pawnIndex, hunterRoleId)) return;
            int at = -1;
            for (int index = 0; index < ordered.Length; index++)
                if (ordered[index] == hunterRoleId) { at = index; break; }
            if (at < 0) return;
            while (at + 1 < ordered.Length && PreferRoleBeforeHunter(facts, ordered[at + 1])
                && !facts.HasProtectedDirectAssignment(pawnIndex, ordered[at + 1]))
            {
                int moved = ordered[at];
                ordered[at] = ordered[at + 1];
                ordered[at + 1] = moved;
                at++;
            }
        }

        private static bool PreferRoleBeforeHunter(EngineContext facts, int roleId)
        {
            RoleView role = facts.RoleOf(roleId);
            return role != null && (role.HasRules || role.Blocker);
        }

        private void OrderByScore(
            EngineContext facts,
            int pawnIndex,
            IReadOnlyDictionary<int, long> positions,
            int[] scores,
            int[] ordered)
        {
            bool changed;
            do
            {
                changed = false;
                for (int index = 0; index + 1 < ordered.Length; index++)
                {
                    int leftRoleId = ordered[index];
                    int rightRoleId = ordered[index + 1];
                    int leftAt = roleIds.IndexOf(leftRoleId);
                    int rightAt = roleIds.IndexOf(rightRoleId);
                    if (IsOrderingBarrier(facts, pawnIndex, leftRoleId)
                        || IsOrderingBarrier(facts, pawnIndex, rightRoleId)
                        || CompareOrderingRoles(
                            facts,
                            pawnIndex,
                            rightRoleId,
                            leftRoleId,
                            scores[rightAt],
                            scores[leftAt],
                            positions) >= 0)
                        continue;

                    ordered[index] = rightRoleId;
                    ordered[index + 1] = leftRoleId;
                    changed = true;
                }
            }
            while (changed);
        }

        /// One bubble pass for a single appended role, using the same swap
        /// rule as OrderByScore, so a hand-added role lands where the engine
        /// would let it rise without moving anything else.
        internal int PlacementIndex(
            EngineContext facts,
            int pawnIndex,
            IReadOnlyDictionary<int, long> positions,
            RecommendationFormulaEngine formulas,
            IReadOnlyList<int> existing,
            int roleId)
        {
            int at = existing.Count;
            if (IsOrderingBarrier(facts, pawnIndex, roleId)) return at;
            int score = OrderingScore(facts, pawnIndex, roleId, formulas);
            while (at > 0)
            {
                int left = existing[at - 1];
                if (IsOrderingBarrier(facts, pawnIndex, left)
                    || CompareOrderingRoles(
                        facts, pawnIndex, roleId, left, score,
                        OrderingScore(facts, pawnIndex, left, formulas),
                        positions) >= 0)
                    break;
                at--;
            }
            return at;
        }

        private bool IsOrderingBarrier(
            EngineContext facts,
            int pawnIndex,
            int roleId)
        {
            RoleView role = facts.RoleOf(roleId);
            return role == null
                || role.PreserveRecommendationOrder
                || IsSpecialRole(roleId)
                || facts.HasProtectedDirectAssignment(pawnIndex, roleId);
        }

        private int CompareOrderingRoles(
            EngineContext facts,
            int pawnIndex,
            int leftRoleId,
            int rightRoleId,
            int leftScore,
            int rightScore,
            IReadOnlyDictionary<int, long> positions)
        {
            int score = rightScore.CompareTo(leftScore);
            if (score != 0) return score;
            if (SharesRequiredSkill(facts, leftRoleId, rightRoleId))
            {
                int minimum = EligibleTargetMinimum(
                        facts, pawnIndex, rightRoleId)
                    .CompareTo(EligibleTargetMinimum(
                        facts, pawnIndex, leftRoleId));
                if (minimum != 0) return minimum;
            }
            else
            {
                int skill = OrderingSkillLevel(facts, pawnIndex, rightRoleId)
                    .CompareTo(OrderingSkillLevel(
                        facts, pawnIndex, leftRoleId));
                if (skill != 0) return skill;
            }
            int position = positions[leftRoleId].CompareTo(positions[rightRoleId]);
            return position != 0
                ? position
                : leftRoleId.CompareTo(rightRoleId);
        }

        private static bool SharesRequiredSkill(
            EngineContext facts,
            int leftRoleId,
            int rightRoleId)
            => RepeatChampionPenalties.SharesRequiredSkill(
                facts, facts.RoleOf(leftRoleId), facts.RoleOf(rightRoleId));

        private static int OrderingSkillLevel(
            EngineContext facts,
            int pawnIndex,
            int roleId)
        {
            facts.BestSignal(
                pawnIndex,
                facts.RoleOf(roleId),
                out string skillDefName,
                out _);
            return facts.SkillLevel(pawnIndex, skillDefName);
        }

        private int OrderingScore(
            EngineContext facts,
            int pawnIndex,
            int roleId,
            RecommendationFormulaEngine formulas)
        {
            RoleView role = facts.RoleOf(roleId);
            SignalBucket verdict = facts.BestSignal(
                pawnIndex,
                role,
                out string skillDefName,
                out _);
            return formulas.OrderingScore(
                verdict,
                facts.SkillLevel(pawnIndex, skillDefName),
                MinimumBonusOf(roleId),
                role.Category,
                role.Time) - RecommendationFormulaEngine.AversionPenalty(facts.HasWorkAversion(pawnIndex, role));
        }

        private static int EligibleTargetMinimum(
            EngineContext facts,
            int pawnIndex,
            int roleId)
        {
            int result = -1;
            RoleView role = facts.RoleOf(roleId);
            for (int pathIndex = 0;
                 pathIndex < facts.Colony.Paths.Count;
                 pathIndex++)
            {
                PathView path = facts.Colony.Paths[pathIndex];
                if (PathActivation.UniqueTargetRoleId(path) != roleId) continue;
                int targetAt = path.RoleIds.IndexOf(roleId);
                if (facts.InsideBand(pawnIndex, role, path, targetAt)
                    && path.BandMins[targetAt] > result)
                    result = path.BandMins[targetAt];
            }
            return result;
        }

        private List<PathView> OrderingPaths(EngineContext facts)
        {
            var result = new List<PathView>(activations.Count);
            for (int index = 0; index < activations.Count; index++)
                result.Add(facts.PathsById[activations[index].PathId]);
            for (int pathIndex = 0;
                 pathIndex < facts.Colony.Paths.Count;
                 pathIndex++)
            {
                PathView candidate = facts.Colony.Paths[pathIndex];
                int targetRoleId = PathActivation.UniqueTargetRoleId(candidate);
                if (targetRoleId < 0 || !HasDirectRole(targetRoleId)) continue;
                int existing = PlacementTargeting(result, targetRoleId);
                if (existing < activations.Count && existing >= 0) continue;
                if (existing < 0)
                {
                    result.Add(candidate);
                    continue;
                }
                int structure = PathActivation.CompareStructure(
                    candidate, result[existing]);
                if (structure < 0
                    || structure == 0 && candidate.Id < result[existing].Id)
                    result[existing] = candidate;
            }
            return result;
        }

        private static int PlacementTargeting(
            List<PathView> placements,
            int roleId)
        {
            for (int index = 0; index < placements.Count; index++)
                if (PathActivation.UniqueTargetRoleId(placements[index]) == roleId)
                    return index;
            return -1;
        }

        private bool OrdersMember(PathView path, int targetRoleId, int roleId)
        {
            PathActivation? activation = ActivationFor(path.Id);
            if (activation != null)
            {
                for (int index = 0; index < activation.ActiveRoleIds.Length; index++)
                    if (activation.ActiveRoleIds[index] == roleId) return true;
                return false;
            }
            int targetAt = path.RoleIds.IndexOf(targetRoleId);
            int roleAt = path.RoleIds.IndexOf(roleId);
            return targetAt >= 0
                && roleAt >= 0
                && path.BandMins[roleAt] < path.BandMins[targetAt];
        }

        private PathActivation? ActivationFor(int pathId)
        {
            for (int index = 0; index < activations.Count; index++)
                if (activations[index].PathId == pathId) return activations[index];
            return null;
        }

        private int BestAvailableRole(
            bool[] emitted,
            long[] keys,
            IReadOnlyDictionary<int, long> positions)
        {
            int best = -1;
            for (int index = 0; index < roleIds.Count; index++)
            {
                if (emitted[index]) continue;
                if (best < 0
                    || keys[index] < keys[best]
                    || keys[index] == keys[best]
                    && (positions[roleIds[index]] < positions[roleIds[best]]
                        || positions[roleIds[index]] == positions[roleIds[best]]
                        && roleIds[index] < roleIds[best]))
                    best = index;
            }
            return best;
        }

    }

    /// Immutable diagnostic projection of one target-role selection. It is
    /// built from final drafts, so consumers never have to reproduce planner
    /// classification or path-expansion logic.
    internal sealed class RecommendationTargetAssignment
    {
        private readonly int[] roleIds;

        internal RecommendationTargetAssignment(
            int targetRoleId,
            int pawnIndex,
            RecommendationTargetAssignmentKind kind,
            int[] roleIds,
            RecommendationSelectionStage stage,
            int candidateRank,
            int candidateCount,
            int stageRank,
            int selectionSlot,
            int selectionSlotCount,
            int pathId,
            SignalBucket signalBucket,
            SignalBucket surplusMinimumSignalBucket,
            bool surplusQualifiedBySignal)
        {
            TargetRoleId = targetRoleId;
            PawnIndex = pawnIndex;
            Kind = kind;
            this.roleIds = roleIds;
            Stage = stage;
            CandidateRank = candidateRank;
            CandidateCount = candidateCount;
            StageRank = stageRank;
            SelectionSlot = selectionSlot;
            SelectionSlotCount = selectionSlotCount;
            PathId = pathId;
            SignalBucket = signalBucket;
            SurplusMinimumSignalBucket = surplusMinimumSignalBucket;
            SurplusQualifiedBySignal = surplusQualifiedBySignal;
            for (int index = 0; index < roleIds.Length; index++)
                if (roleIds[index] == targetRoleId)
                {
                    AssignsTargetRole = true;
                    break;
                }
        }

        internal int TargetRoleId { get; }
        internal int PawnIndex { get; }
        internal RecommendationTargetAssignmentKind Kind { get; }
        internal RecommendationSelectionStage Stage { get; }
        internal int CandidateRank { get; }
        internal int CandidateCount { get; }
        internal int StageRank { get; }
        internal int SelectionSlot { get; }
        internal int SelectionSlotCount { get; }
        internal int PathId { get; }
        internal bool AssignsTargetRole { get; }
        internal SignalBucket SignalBucket { get; }
        internal SignalBucket SurplusMinimumSignalBucket { get; }
        internal bool SurplusQualifiedBySignal { get; }
        internal int RoleCount => roleIds.Length;
        internal int RoleAt(int index) => roleIds[index];
    }

    public sealed partial class RecommendationPlan
    {
        private readonly int[][] rolesByPawn;
        private readonly int[][] pathsByPawn;
        private readonly int[] activatedPathCountsByPawn;
        private readonly Dictionary<int, RoleRecommendationExplanation>[]
            explanationsByPawn;
        private readonly RecommendationTargetAssignment[] targetAssignments;
        // Retained for PlacementIndex: the run's facts, formulas, positions and
        // per-pawn drafts, all immutable once the plan is published.
        private readonly EngineContext facts;
        internal EngineContext Context => facts;
        private readonly RecommendationFormulaEngine formulas;
        private readonly IReadOnlyDictionary<int, long> positions;
        private readonly PawnDraft[] drafts;
        // Owner/key: this immutable plan and pawn index. Value: optional detached
        // source-order provenance plus affected role IDs, never mutated after
        // publication. Dependencies: the plan's assignments/pins, composite work,
        // activation and rule/blocker policies. Refresh: plan build only; equal
        // plan inputs retain it with the plan. Teardown: plan/window/store release.
        // Final pin composition consumes this same producer artifact so excluded
        // pinned barriers cannot discard a removed source's ordering provenance.
        private readonly CompositePlacement?[]? compositePlacements;

        private RecommendationPlan(
            int[][] rolesByPawn,
            int[][] pathsByPawn,
            int[] activatedPathCountsByPawn,
            Dictionary<int, RoleRecommendationExplanation>[]
                explanationsByPawn,
            RecommendationTargetAssignment[] targetAssignments,
            EngineContext facts,
            RecommendationFormulaEngine formulas,
            IReadOnlyDictionary<int, long> positions,
            PawnDraft[] drafts,
            CompositePlacement?[]? compositePlacements)
        {
            this.rolesByPawn = rolesByPawn;
            this.pathsByPawn = pathsByPawn;
            this.activatedPathCountsByPawn = activatedPathCountsByPawn;
            this.explanationsByPawn = explanationsByPawn;
            this.targetAssignments = targetAssignments;
            this.facts = facts;
            this.formulas = formulas;
            this.positions = positions;
            this.drafts = drafts;
            this.compositePlacements = compositePlacements;
        }

        internal int[] OrderFixTarget(int pawnIndex, IReadOnlyDictionary<int, RoleView> roles,
            IReadOnlyList<AssignmentView> existing, IReadOnlyList<int> recommended)
        {
            CompositePlacement? placement = compositePlacements?[pawnIndex];
            return placement == null ? Ordering.PreserveProtectedOrder(roles, existing, recommended)
                : RestoreCompositePlacement(facts, roles, placement, recommended);
        }

        /// Where a role the player adds by hand lands in the pawn's current
        /// assignment order: appended, then moved ahead of every role it
        /// outranks under the ordering score and tie-breaks this plan used,
        /// stopping at an ordering barrier (pinned, rule-bound, blocker or
        /// preserved-order roles). The other roles keep their places; unknown
        /// roles append.
        public int PlacementIndex(int pawnIndex, IReadOnlyList<int> existing, int roleId)
            => drafts[pawnIndex].PlacementIndex(
                facts, pawnIndex, positions, formulas, existing, roleId);

        public int PawnCount => rolesByPawn.Length;
        public int RoleCountAt(int pawnIndex) => rolesByPawn[pawnIndex].Length;
        public int RoleAt(int pawnIndex, int index) => rolesByPawn[pawnIndex][index];
        public int PathCountAt(int pawnIndex) => pathsByPawn[pawnIndex].Length;
        public int PathAt(int pawnIndex, int index) => pathsByPawn[pawnIndex][index];
        public bool PathActivatedAt(int pawnIndex, int index) =>
            index < activatedPathCountsByPawn[pawnIndex];
        public bool TryGetExplanation(
            int pawnIndex,
            int roleId,
            out RoleRecommendationExplanation explanation) =>
            explanationsByPawn[pawnIndex].TryGetValue(roleId, out explanation);
        internal int TargetAssignmentCount => targetAssignments.Length;
        internal RecommendationTargetAssignment TargetAssignmentAt(int index)
            => targetAssignments[index];

        public static RecommendationPlan Build(ColonyView colony)
            => Build(colony, RecommendationsTuningOptions.Default);

        public static RecommendationPlan Build(
            ColonyView colony, RoleOrderingStrategy ordering)
            => Build(colony, RecommendationsTuningOptions.Default, ordering);

        public static RecommendationPlan Build(
            ColonyView colony,
            RecommendationsTuningOptions options,
            RoleOrderingStrategy ordering = RoleOrderingStrategy.Current)
            => Build(new EngineContext(colony), options, ordering);

        internal static RecommendationPlan Build(
            EngineContext facts,
            RecommendationsTuningOptions options,
            RoleOrderingStrategy ordering = RoleOrderingStrategy.Current)
        {
            var formulas = new RecommendationFormulaEngine(options);
            ColonyView colony = facts.Colony;
            RecommendationPlan? humanBaseline = null;
            if (facts.MechanicalCoverageApplied)
            {
                // Detached shared immutable inputs; only external supply differs.
                // Baseline learning is selected before mechanical displacement.
                var humanOnly = new ColonyView
                {
                    Pawns = colony.Pawns, Roles = colony.Roles, Paths = colony.Paths,
                    OrderTemplate = colony.OrderTemplate, SkillMaxLevels = colony.SkillMaxLevels,
                    HunterRoleId = colony.HunterRoleId, FireBlockerRoleId = colony.FireBlockerRoleId,
                };
                humanBaseline = Build(humanOnly, options, ordering);
            }
            int pawnCount = colony.Pawns.Count;
            var drafts = new PawnDraft[pawnCount];
            for (int pawnIndex = 0; pawnIndex < pawnCount; pawnIndex++)
                drafts[pawnIndex] = new PawnDraft();
            AddSpecialRoles(facts, drafts);

            IReadOnlyDictionary<int, long> positions = facts.BasePositions();
            List<RoleView> roles = OrderRolesForProcessing(facts, colony.Roles);
            var rolePlans = new List<RolePlan>();
            // Roles are processed skilled-first, targets before their trainees,
            // and coverers before covered, so each plan's assignments are visible
            // before the roles they cover or train are selected.
            var priorChampionsByPawn = new List<int>[pawnCount];
            // Build and publish each role in recommended order so every plan's
            // assignments are visible in the drafts before the next role is
            // selected. The surplus eligibility check reads those drafts to fold
            // a pick under a broader covering role assigned earlier.
            for (int roleIndex = 0; roleIndex < roles.Count; roleIndex++)
            {
                RoleView role = roles[roleIndex];
                if (!role.Available
                    || facts.MechanicallyCovered(role.Id)
                    || !role.Enabled
                    || facts.IsRecommendationExcluded(role)
                    || !role.PlannedByDemand
                    // Skill-less roles without demand are retained chores; with
                    // demand they fill every capable pawn (UnskilledFill).
                    || role.IsNever
                    // Composites never join the run: their members are planned
                    // individually and a composite is substituted back in only
                    // when a member run reproduces its exact job priorities.
                    || role.MemberRoleIds != null
                    || role.Id == colony.HunterRoleId)
                    continue;
                RolePlan rolePlan = RolePlan.Build(
                    facts, role, formulas, drafts, priorChampionsByPawn);
                rolePlans.Add(rolePlan);
                for (int candidateIndex = 0;
                     candidateIndex < rolePlan.CandidateCount;
                     candidateIndex++)
                {
                    if (!rolePlan.IsSelected(candidateIndex)) continue;
                    int pawnIndex = rolePlan.CandidateAt(candidateIndex).PawnIndex;
                    bool surplus = rolePlan.IsSurplus(candidateIndex);
                    byte minimumBonus =
                        rolePlan.MinimumBonusAt(candidateIndex);
                    bool minimumPick =
                        rolePlan.IsMinimumPick(candidateIndex);
                    // RolePlan already resolved each downgraded pick's path
                    // activation; publish never re-runs path resolution.
                    PathActivation? activation =
                        rolePlan.ResolvedActivationAt(candidateIndex);
                    if (surplus
                        && !PathActivation.TargetBandContains(
                            facts, pawnIndex, role))
                    {
                        if (activation != null)
                            drafts[pawnIndex].AddActivation(
                                activation, minimumBonus, minimumPick);
                        continue;
                    }
                    if (rolePlan.IsTrainingWaiver(candidateIndex)
                        && activation != null)
                    {
                        drafts[pawnIndex].AddActivation(
                            activation, minimumBonus, minimumPick);
                        continue;
                    }
                    drafts[pawnIndex].AddRole(
                        role.Id,
                        minimumBonus: minimumBonus,
                        minimumPick: minimumPick);
                }
                if (rolePlan.ChampionPawnIndex < 0) continue;
                List<int> championed =
                    priorChampionsByPawn[rolePlan.ChampionPawnIndex]
                    ?? (priorChampionsByPawn[rolePlan.ChampionPawnIndex] =
                        new List<int>());
                championed.Add(role.Id);
                drafts[rolePlan.ChampionPawnIndex].MarkChampionPick(role.Id);
            }

            // Coverage is now resolved during selection (single-pass RolePlan
            // plus champion selection) and general redundancy is a surplus
            // eligibility check; ResolveCoverage is retained but inactive.
            // ResolveCoverage(facts, rolePlans, drafts);
            AddLateSpecialRoles(facts, drafts, formulas);

            if (colony.IntegrateMechanicalWorkers)
                for (int pawnIndex = 0; pawnIndex < pawnCount; pawnIndex++)
                    for (int roleIndex = drafts[pawnIndex].RoleCount - 1; roleIndex >= 0; roleIndex--)
                    {
                        int id = drafts[pawnIndex].RoleAt(roleIndex);
                        if (facts.MechanicallyCovered(id)
                            && !facts.HasProtectedDirectAssignment(pawnIndex, id))
                            drafts[pawnIndex].RemoveRole(id);
                    }
            if (humanBaseline != null)
                for (int pawnIndex = 0; pawnIndex < pawnCount; pawnIndex++)
                    drafts[pawnIndex].RetainLearning(humanBaseline.drafts[pawnIndex], facts, pawnIndex);

            var rolesByPawn = new int[pawnCount][];
            var pathsByPawn = new int[pawnCount][];
            var activatedPathCountsByPawn = new int[pawnCount];
            for (int pawnIndex = 0; pawnIndex < pawnCount; pawnIndex++)
            {
                rolesByPawn[pawnIndex] = drafts[pawnIndex].PublishRoles(
                    facts,
                    pawnIndex,
                    positions,
                    formulas,
                    out pathsByPawn[pawnIndex],
                    out activatedPathCountsByPawn[pawnIndex],
                    ordering);
            }
            RecommendationTargetAssignment[] targetAssignments =
                BuildTargetAssignments(
                    rolePlans, drafts, rolesByPawn, formulas);
            Dictionary<int, RoleRecommendationExplanation>[] explanations =
                BuildExplanations(
                    facts, drafts, formulas, targetAssignments, rolePlans);
            if (humanBaseline != null)
                for (int pawnIndex = 0; pawnIndex < pawnCount; pawnIndex++)
                    for (int roleIndex = 0; roleIndex < drafts[pawnIndex].RoleCount; roleIndex++)
                    {
                        int id = drafts[pawnIndex].RoleAt(roleIndex);
                        if (drafts[pawnIndex].RetainedLearning(id)
                            && humanBaseline.TryGetExplanation(pawnIndex, id, out var learning))
                            explanations[pawnIndex][id] = learning.PreserveLearning();
                    }
            CompositePlacement?[]? compositePlacements = SubstituteComposites(facts, rolesByPawn, explanations);
            KeepIncapacitatedAssignments(facts, rolesByPawn, pathsByPawn,
                activatedPathCountsByPawn, explanations);
            return new RecommendationPlan(
                rolesByPawn,
                pathsByPawn,
                activatedPathCountsByPawn,
                explanations,
                targetAssignments,
                facts,
                formulas,
                positions,
                drafts,
                compositePlacements);
        }

        /// An incapacitated pawn was no candidate and no holder during the
        /// run; retain their existing assignments except explicit exclusions.
        private static void KeepIncapacitatedAssignments(
            EngineContext facts,
            int[][] rolesByPawn,
            int[][] pathsByPawn,
            int[] activatedPathCountsByPawn,
            Dictionary<int, RoleRecommendationExplanation>[] explanationsByPawn)
        {
            ColonyView colony = facts.Colony;
            for (int pawnIndex = 0; pawnIndex < colony.Pawns.Count; pawnIndex++)
            {
                PawnView pawn = colony.Pawns[pawnIndex];
                if (!pawn.Incapacitated) continue;
                var kept = new List<int>(pawn.Existing.Count);
                var explanations = new Dictionary<int, RoleRecommendationExplanation>();
                for (int index = 0; index < pawn.Existing.Count; index++)
                {
                    int roleId = pawn.Existing[index].RoleId;
                    if (facts.IsRecommendationExcluded(facts.RoleOf(roleId))) continue;
                    kept.Add(roleId);
                    explanations[roleId] = new RoleRecommendationExplanation
                    {
                        RoleId = roleId,
                        Recommended = true,
                        SpecialPickReason = SpecialPickReason.Incapacitated,
                    };
                }
                rolesByPawn[pawnIndex] = kept.ToArray();
                pathsByPawn[pawnIndex] = System.Array.Empty<int>();
                activatedPathCountsByPawn[pawnIndex] = 0;
                explanationsByPawn[pawnIndex] = explanations;
            }
        }

        private static RecommendationTargetAssignment[] BuildTargetAssignments(
            List<RolePlan> rolePlans,
            PawnDraft[] drafts,
            int[][] rolesByPawn,
            RecommendationFormulaEngine formulas)
        {
            var assignments = new List<RecommendationTargetAssignment>();
            for (int planIndex = 0; planIndex < rolePlans.Count; planIndex++)
            {
                RolePlan plan = rolePlans[planIndex];
                for (int candidateIndex = 0;
                     candidateIndex < plan.CandidateCount;
                     candidateIndex++)
                {
                    RecommendationTargetAssignmentKind kind =
                        plan.AssignmentKindAt(candidateIndex);
                    if (kind == RecommendationTargetAssignmentKind.None)
                        continue;
                    int pawnIndex = plan.CandidateAt(candidateIndex).PawnIndex;
                    PawnDraft draft = drafts[pawnIndex];
                    PathActivation? activation = draft.ActivationToward(
                        plan.RoleId);
                    bool direct = draft.HasDirectRole(plan.RoleId);
                    if (activation == null && !direct) continue;

                    var assignedRoleIds = new List<int>();
                    if (direct
                        || activation != null
                            && ContainsRole(
                                activation.ActiveRoleIds, plan.RoleId))
                        assignedRoleIds.Add(plan.RoleId);
                    if (activation != null)
                    {
                        int[] published = rolesByPawn[pawnIndex];
                        for (int roleIndex = 0;
                             roleIndex < published.Length;
                             roleIndex++)
                        {
                            int roleId = published[roleIndex];
                            if (roleId != plan.RoleId
                                && ContainsRole(
                                    activation.ActiveRoleIds, roleId))
                                assignedRoleIds.Add(roleId);
                        }
                    }
                    if (assignedRoleIds.Count == 0) continue;
                    assignments.Add(new RecommendationTargetAssignment(
                        plan.RoleId,
                        pawnIndex,
                        kind,
                        assignedRoleIds.ToArray(),
                        plan.SelectionStageAt(candidateIndex),
                        candidateIndex + 1,
                        plan.CandidateCount,
                        plan.StageRankAt(candidateIndex),
                        plan.SelectionSlotAt(candidateIndex),
                        plan.SelectionSlotCount,
                        activation?.PathId ?? -1,
                        plan.SelectionSignalAt(candidateIndex),
                        formulas.SurplusMinimumSignal,
                        plan.IsSignalQualifiedSurplus(candidateIndex)));
                }
            }
            return assignments.ToArray();
        }

        private static bool ContainsRole(int[] roleIds, int roleId)
        {
            for (int index = 0; index < roleIds.Length; index++)
                if (roleIds[index] == roleId) return true;
            return false;
        }

        private sealed class CompositeSpec
        {
            internal RoleView Role = null!;    // always set at creation
            internal int[] MemberIds = null!;  // always set at creation
        }

        private readonly struct CompositeReservation
        {
            internal CompositeReservation(CompositeSpec composite, int start)
            { Composite = composite; Start = start; }
            internal readonly CompositeSpec Composite;
            internal readonly int Start;
        }

        private readonly struct ProjectedMemberPlacement
        {
            internal ProjectedMemberPlacement(int roleId, int sourceIndex)
            { RoleId = roleId; SourceIndex = sourceIndex; }
            internal readonly int RoleId, SourceIndex;
        }

        private sealed class CompositePlacement
        {
            internal CompositePlacement(List<AssignmentView> sources, int[] ownedRoles)
            { Sources = sources; OwnedRoles = ownedRoles; }
            internal readonly List<AssignmentView> Sources;
            internal readonly int[] OwnedRoles;
        }

        /// Post-planning pass: collapse a consecutive, in-member-order run of a
        /// composite's members into the composite. This is job-priority-neutral —
        /// a composite emits exactly its members' givers in member order and the
        /// surrounding roles are unchanged, so the compiled order is identical —
        /// provided the composite and its members have ordinary activation
        /// semantics and no assignment state or pin is discarded. The match
        /// then needs only a role-id window compare, never a giver recompute.
        /// The explanation carries the folded members' explanations.
        private static CompositePlacement?[]? SubstituteComposites(
            EngineContext facts,
            int[][] rolesByPawn,
            Dictionary<int, RoleRecommendationExplanation>[] explanationsByPawn)
        {
            var byFirstMember = new Dictionary<int, List<CompositeSpec>>();
            IReadOnlyList<RoleView> roles = facts.Colony.Roles;
            for (int index = 0; index < roles.Count; index++)
            {
                RoleView role = roles[index];
                if (!CompositeSubstitutionPolicy.IsCandidate(role, roles))
                    continue;
                var members = new int[role.MemberRoleIds!.Count];
                for (int m = 0; m < members.Length; m++)
                    members[m] = role.MemberRoleIds[m];
                var spec = new CompositeSpec
                {
                    Role = role,
                    MemberIds = members,
                };
                if (!byFirstMember.TryGetValue(members[0], out List<CompositeSpec> list))
                    byFirstMember[members[0]] = list = new List<CompositeSpec>();
                list.Add(spec);
            }
            if (byFirstMember.Count == 0) return null;

            var scratch = new List<int>();
            List<CompositeReservation>? reservations = null;
            List<ProjectedMemberPlacement>? projectedMembers = null;
            CompositePlacement?[]? placementsByPawn = null;
            for (int pawnIndex = 0; pawnIndex < rolesByPawn.Length; pawnIndex++)
            {
                int[] published = rolesByPawn[pawnIndex];
                scratch.Clear();
                reservations?.Clear();
                projectedMembers?.Clear();
                ReserveCompositeOverrides(facts, pawnIndex, byFirstMember, published, ref reservations);
                bool changed = false;
                for (int i = 0; i < published.Length;)
                {
                    bool reservedMember = false;
                    if (reservations != null)
                        for (int index = 0; index < reservations.Count; index++)
                        {
                            CompositeReservation reservation = reservations[index];
                            if (reservation.Start == i)
                            {
                                scratch.Add(reservation.Composite.Role.Id);
                                AddBundledExplanation(explanationsByPawn[pawnIndex], reservation.Composite);
                            }
                            reservedMember |= System.Array.IndexOf(reservation.Composite.MemberIds, published[i]) >= 0;
                        }
                    if (reservedMember)
                    {
                        // The composite controls only generated members. An
                        // explicit member remains a separate authored assignment
                        // with its own activation and pin, even inside a group.
                        if (HasIndependentMemberAssignment(facts, pawnIndex, published[i], reservations!, out int sourceIndex))
                        {
                            scratch.Add(published[i]);
                            if (sourceIndex >= 0)
                                (projectedMembers ??= new List<ProjectedMemberPlacement>()).Add(
                                    new ProjectedMemberPlacement(published[i], sourceIndex));
                        }
                        i++; changed = true; continue;
                    }
                    CompositeSpec? match = LongestCompositeMatch(
                        facts, pawnIndex, byFirstMember, published, i, reservations);
                    if (match == null)
                    {
                        scratch.Add(published[i]);
                        i++;
                        continue;
                    }
                    scratch.Add(match.Role.Id);
                    AddBundledExplanation(explanationsByPawn[pawnIndex], match);
                    i += match.MemberIds.Length;
                    changed = true;
                }
                if (changed)
                {
                    int[] replacement = scratch.ToArray();
                    if (reservations != null && reservations.Count != 0
                        && HasProtectedPlacementSource(facts, pawnIndex, replacement))
                    {
                        List<AssignmentView> sources = CompositeOrderingSources(facts, pawnIndex, projectedMembers);
                        int count = 0;
                        for (int index = 0; index < replacement.Length; index++)
                            if (IsCompositePlacementRole(reservations, replacement[index])) count++;
                        var ownedRoles = new int[count];
                        for (int index = 0, target = 0; index < replacement.Length; index++)
                            if (IsCompositePlacementRole(reservations, replacement[index])) ownedRoles[target++] = replacement[index];
                        var placement = new CompositePlacement(sources, ownedRoles);
                        (placementsByPawn ??= new CompositePlacement?[rolesByPawn.Length])[pawnIndex] = placement;
                        replacement = RestoreCompositePlacement(facts, facts.RolesById, placement, replacement);
                    }
                    rolesByPawn[pawnIndex] = replacement;
                }
            }
            return placementsByPawn;
        }

        private static bool IsCompositePlacementRole(List<CompositeReservation> reservations, int roleId)
        {
            for (int owner = 0; owner < reservations.Count; owner++)
                if (reservations[owner].Composite.Role.Id == roleId
                    || System.Array.IndexOf(reservations[owner].Composite.MemberIds, roleId) >= 0) return true;
            return false;
        }

        private static int[] RestoreCompositePlacement(EngineContext facts, IReadOnlyDictionary<int, RoleView> roles,
            CompositePlacement placement, IReadOnlyList<int> recommended)
        {
            int[] ordered = Ordering.PreserveProtectedOrder(roles, placement.Sources, recommended);
            PlaceOwnersWithinProtectedBounds(facts, placement.Sources, placement.OwnedRoles, ordered);
            return ordered;
        }

        // Detached provenance only; actual assignment pins and states remain
        // unchanged. A projected member inherits the removed source's position
        // relative to real surviving vetoes, conditions and pins.
        private static List<AssignmentView> CompositeOrderingSources(EngineContext facts, int pawnIndex,
            List<ProjectedMemberPlacement>? projected)
        {
            List<AssignmentView> existing = facts.Colony.Pawns[pawnIndex].Existing;
            var sources = new List<AssignmentView>(existing.Count + (projected?.Count ?? 0));
            for (int index = 0; index < existing.Count; index++)
            {
                AssignmentView assignment = existing[index];
                sources.Add(assignment);
                if (projected == null || projected.Count == 0) continue;
                RoleView source = facts.RoleOf(assignment.RoleId);
                if (source?.MemberRoleIds == null) continue;
                for (int member = 0; member < source.MemberRoleIds.Count; member++)
                    for (int projectedIndex = 0; projectedIndex < projected.Count; projectedIndex++)
                        if (projected[projectedIndex].SourceIndex == index
                            && projected[projectedIndex].RoleId == source.MemberRoleIds[member])
                            sources.Add(new AssignmentView { RoleId = source.MemberRoleIds[member], Enabled = true });
            }
            return sources;
        }

        private static bool IsProtectedSource(EngineContext facts, AssignmentView assignment)
        {
            RoleView role = facts.RoleOf(assignment.RoleId);
            return assignment.Pinned || role != null && (role.HasRules || role.Blocker);
        }

        private static bool HasProtectedPlacementSource(EngineContext facts, int pawnIndex, int[] ordered)
        {
            List<AssignmentView> existing = facts.Colony.Pawns[pawnIndex].Existing;
            for (int index = 0; index < existing.Count; index++)
                if (IsProtectedSource(facts, existing[index])
                    && (existing[index].Pinned || System.Array.IndexOf(ordered, existing[index].RoleId) >= 0)) return true;
            return false;
        }

        // Restore/project only within the window set by real protected roles.
        // An unpinned owner keeps the engine's preferred position when that
        // position already satisfies the barriers; it never becomes a new pin.
        private static void PlaceOwnersWithinProtectedBounds(EngineContext facts, List<AssignmentView> sources,
            int[] ownedRoles, int[] ordered)
        {
            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                AssignmentView source = sources[sourceIndex];
                if (IsProtectedSource(facts, source)) continue;
                if (System.Array.IndexOf(ownedRoles, source.RoleId) < 0) continue;
                int at = System.Array.IndexOf(ordered, source.RoleId);
                if (at < 0) continue;
                int lower = 0, upper = ordered.Length - 1;
                for (int barrier = 0; barrier < sources.Count; barrier++)
                {
                    if (!IsProtectedSource(facts, sources[barrier])) continue;
                    int barrierAt = System.Array.IndexOf(ordered, sources[barrier].RoleId);
                    if (barrierAt < 0) continue;
                    if (barrierAt > at) barrierAt--; // position after removing this owner
                    if (barrier < sourceIndex) lower = System.Math.Max(lower, barrierAt + 1);
                    if (barrier > sourceIndex) upper = System.Math.Min(upper, barrierAt);
                }
                int target = System.Math.Min(upper, System.Math.Max(lower, at));
                if (target > at)
                    for (int index = at; index < target; index++) ordered[index] = ordered[index + 1];
                else
                    for (int index = at; index > target; index--) ordered[index] = ordered[index - 1];
                ordered[target] = source.RoleId;
            }
        }

        // A held override owns its still-recommended generated member identities.
        // Reserve those identities and every overlapping held composite transitively, so
        // equal, longer or partly overlapping alternatives cannot erase any
        // existing assignment's identity/state. Explicit members remain separate;
        // only generated member entries are replaced by their existing owners.
        private static void ReserveCompositeOverrides(EngineContext facts, int pawnIndex,
            Dictionary<int, List<CompositeSpec>> byFirstMember, int[] published,
            ref List<CompositeReservation>? reservations)
        {
            List<AssignmentView> existing = facts.Colony.Pawns[pawnIndex].Existing;
            for (int index = 0; index < existing.Count; index++)
            {
                AssignmentView assignment = existing[index];
                if (assignment.Enabled && !assignment.ForceOn) continue;
                if (TryMatchHeldComposite(facts, pawnIndex, assignment, byFirstMember, published,
                        out CompositeReservation reservation))
                    (reservations ??= new List<CompositeReservation>()).Add(reservation);
            }
            if (reservations == null || reservations.Count == 0) return;
            bool added;
            do
            {
                added = false;
                for (int index = 0; index < existing.Count; index++)
                {
                    AssignmentView assignment = existing[index];
                    if (!assignment.Enabled || assignment.ForceOn) continue; // already seeded
                    bool included = false;
                    for (int prior = 0; prior < reservations.Count; prior++)
                        if (reservations[prior].Composite.Role.Id == assignment.RoleId) { included = true; break; }
                    if (included || !TryMatchHeldComposite(facts, pawnIndex, assignment, byFirstMember, published,
                            out CompositeReservation reservation)) continue;
                    for (int prior = 0; prior < reservations.Count; prior++)
                        if (SharesCompositeMember(reservation.Composite.MemberIds, reservations[prior].Composite.MemberIds))
                        { reservations.Add(reservation); added = true; break; }
                }
            } while (added);
        }

        private static bool TryMatchHeldComposite(EngineContext facts, int pawnIndex, AssignmentView assignment,
            Dictionary<int, List<CompositeSpec>> byFirstMember, int[] published, out CompositeReservation reservation)
        {
            reservation = default;
            RoleView role = facts.RoleOf(assignment.RoleId);
            if (role?.MemberRoleIds == null || role.MemberRoleIds.Count == 0
                || !facts.MeetsExplicitRequiredSkills(pawnIndex, role)
                || System.Array.IndexOf(published, role.Id) >= 0
                || !byFirstMember.TryGetValue(role.MemberRoleIds[0], out List<CompositeSpec> candidates)) return false;
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                CompositeSpec composite = candidates[candidateIndex];
                if (composite.Role.Id != role.Id) continue;
                int start = published.Length;
                // Existing assignments keep their authored member order. The
                // planner may separate/reorder generated members around a pin;
                // adjacency is required only for new, ordinary substitutions.
                for (int member = 0; member < composite.MemberIds.Length; member++)
                {
                    int at = System.Array.IndexOf(published, composite.MemberIds[member]);
                    if (at < 0) return false;
                    if (at < start) start = at;
                }
                reservation = new CompositeReservation(composite, start);
                return true;
            }
            return false;
        }

        private static bool HasIndependentMemberAssignment(EngineContext facts, int pawnIndex, int roleId,
            List<CompositeReservation> reservations, out int sourceIndex)
        {
            sourceIndex = -1;
            List<AssignmentView> existing = facts.Colony.Pawns[pawnIndex].Existing;
            for (int index = 0; index < existing.Count; index++)
                if (existing[index].RoleId == roleId) return true;
            // A removed ordinary composite may have supplied this still-wanted
            // member while another of its members is no longer recommended.
            // Project only that active member; do not retain the unwanted role
            // or work. A pin already retains its source at the apply boundary.
            for (int index = 0; index < existing.Count; index++)
            {
                AssignmentView assignment = existing[index];
                if (!assignment.Enabled || assignment.Pinned) continue;
                RoleView role = facts.RoleOf(assignment.RoleId);
                if (role?.MemberRoleIds == null || !role.MemberRoleIds.Contains(roleId)
                    || role.HasRules || role.Blocker || !role.Enabled && !assignment.ForceOn) continue;
                bool retained = false;
                for (int prior = 0; prior < reservations.Count; prior++)
                    if (reservations[prior].Composite.Role.Id == role.Id) { retained = true; break; }
                if (!retained) { sourceIndex = index; return true; }
            }
            return false;
        }

        private static bool SharesCompositeMember(int[] first, int[] second)
        {
            for (int index = 0; index < first.Length; index++)
                if (System.Array.IndexOf(second, first[index]) >= 0) return true;
            return false;
        }

        private static bool HasDirectMemberAssignment(List<AssignmentView> existing, int[] members)
        {
            for (int index = 0; index < existing.Count; index++)
                if (System.Array.IndexOf(members, existing[index].RoleId) >= 0) return true;
            return false;
        }

        /// The longest composite whose member ids equal published[start..] in
        /// order; ties broken by smallest composite id. Member roles are unique
        /// per pawn, so a composite matches at most one window.
        private static CompositeSpec? LongestCompositeMatch(
            EngineContext facts,
            int pawnIndex,
            Dictionary<int, List<CompositeSpec>> byFirstMember,
            int[] published,
            int start,
            List<CompositeReservation>? reservations)
        {
            if (!byFirstMember.TryGetValue(
                    published[start], out List<CompositeSpec> candidates))
                return null;
            CompositeSpec? best = null;
            for (int c = 0; c < candidates.Count; c++)
            {
                if (!facts.MeetsExplicitRequiredSkills(
                        pawnIndex, candidates[c].Role))
                    continue;
                int[] members = candidates[c].MemberIds;
                if (start + members.Length > published.Length) continue;
                bool overlaps = false;
                if (reservations != null)
                    for (int index = 0; index < reservations.Count; index++)
                        if (SharesCompositeMember(members, reservations[index].Composite.MemberIds))
                        { overlaps = true; break; }
                if (overlaps) continue;
                if (System.Array.IndexOf(published, candidates[c].Role.Id) >= 0
                    || HasConflictingCompositeAssignmentState(facts, pawnIndex, candidates[c])) continue;
                bool match = true;
                for (int m = 0; m < members.Length; m++)
                {
                    if (published[start + m] != members[m]
                        || HasPreservedAssignmentState(facts, pawnIndex, members[m]))
                    { match = false; break; }
                }
                if (!match) continue;
                if (best == null
                    || members.Length > best.MemberIds.Length
                    || members.Length == best.MemberIds.Length
                        && candidates[c].Role.Id < best.Role.Id)
                    best = candidates[c];
            }
            return best;
        }

        private static bool HasConflictingCompositeAssignmentState(EngineContext facts, int pawnIndex, CompositeSpec composite)
        {
            List<AssignmentView> existing = facts.Colony.Pawns[pawnIndex].Existing;
            for (int index = 0; index < existing.Count; index++)
            {
                AssignmentView assignment = existing[index];
                if (assignment.RoleId != composite.Role.Id) continue;
                if (assignment.Pinned) return true;
                if (assignment.Enabled && !assignment.ForceOn) return false;
                // Reusing this same composite identity preserves its override
                // at apply time. It must not absorb directly-held members,
                // whose own activation would then inherit that override.
                return HasDirectMemberAssignment(existing, composite.MemberIds);
            }
            return false;
        }

        private static bool HasPreservedAssignmentState(EngineContext facts, int pawnIndex, int roleId)
        {
            List<AssignmentView> existing = facts.Colony.Pawns[pawnIndex].Existing;
            for (int index = 0; index < existing.Count; index++)
                if (existing[index].RoleId == roleId)
                    return !existing[index].Enabled || existing[index].ForceOn || existing[index].Pinned;
            return false;
        }

        private static void AddBundledExplanation(
            Dictionary<int, RoleRecommendationExplanation> explanations,
            CompositeSpec spec)
        {
            if (explanations == null) return;
            var members = new List<RoleRecommendationExplanation>(
                spec.MemberIds.Length);
            for (int m = 0; m < spec.MemberIds.Length; m++)
                if (explanations.TryGetValue(
                        spec.MemberIds[m],
                        out RoleRecommendationExplanation memberExplanation))
                    members.Add(memberExplanation);
            explanations[spec.Role.Id] = new RoleRecommendationExplanation
            {
                RoleId = spec.Role.Id,
                Recommended = true,
                SelectionStage = RecommendationSelectionStage.Special,
                SpecialPickReason = SpecialPickReason.Bundled,
                BundledMembers = members,
            };
        }

        private static void ResolveCoverage(
            EngineContext facts,
            List<RolePlan> rolePlans,
            PawnDraft[] drafts)
        {
            for (int planIndex = 0; planIndex < rolePlans.Count; planIndex++)
            {
                RolePlan plan = rolePlans[planIndex];
                RoleView role = facts.RoleOf(plan.RoleId);
                bool exactMinimumRequired =
                    PathActivation.PreferredTargetPath(
                        facts.Colony.Paths, role.Id) != null;
                int coveredPawns = CountCoveredPawns(facts, drafts, role);
                if (HasCoverer(facts, drafts, role))
                {
                    for (int candidateIndex = plan.CandidateCount - 1;
                         candidateIndex >= 0;
                         candidateIndex--)
                    {
                        int pawnIndex = plan.CandidateAt(candidateIndex).PawnIndex;
                        PawnDraft draft = drafts[pawnIndex];
                        if (!draft.ContainsRole(role.Id)
                            || draft.IsPathRole(role.Id)
                            || (plan.IsSignalQualifiedSurplus(candidateIndex)
                                && !PawnHasEarlierCoverer(
                                    facts, draft, pawnIndex, role))
                            // Unskilled reqTotal picks (e.g. a Hauler champion)
                            // coexist with a broader coverer like Grunt and are
                            // never folded; path targets keep their exact minimum.
                            || ((exactMinimumRequired
                                    || role.UnskilledFill)
                                && draft.IsMinimumRole(role.Id))
                            || draft.IsSpecialRole(role.Id))
                            continue;
                        int coverageLoss = PawnHasCoverer(
                            facts, draft, pawnIndex, role) ? 0 : 1;
                        if (coveredPawns - coverageLoss < plan.DirectMinimum)
                            continue;
                        draft.RemoveRole(role.Id);
                        coveredPawns -= coverageLoss;
                    }
                }

                for (int candidateIndex = 0;
                     coveredPawns < plan.DirectMinimum
                     && candidateIndex < plan.CandidateCount;
                     candidateIndex++)
                {
                    int pawnIndex = plan.CandidateAt(candidateIndex).PawnIndex;
                    if (PawnCovers(facts, drafts[pawnIndex], pawnIndex, role))
                        continue;
                    byte minimumBonus =
                        plan.SelectForCoverage(
                            candidateIndex, coveredPawns + 1);
                    drafts[pawnIndex].AddRole(
                        role.Id,
                        minimumBonus: minimumBonus,
                        minimumPick: true);
                    coveredPawns++;
                }
            }
        }

        private static int CountCoveredPawns(
            EngineContext facts,
            PawnDraft[] drafts,
            RoleView role)
        {
            int count = 0;
            for (int pawnIndex = 0; pawnIndex < drafts.Length; pawnIndex++)
                if (PawnCovers(facts, drafts[pawnIndex], pawnIndex, role)) count++;
            return count;
        }

        private static bool HasCoverer(
            EngineContext facts,
            PawnDraft[] drafts,
            RoleView role)
        {
            for (int pawnIndex = 0; pawnIndex < drafts.Length; pawnIndex++)
                if (PawnHasCoverer(facts, drafts[pawnIndex], pawnIndex, role))
                    return true;
            return false;
        }

        private static bool PawnCovers(
            EngineContext facts,
            PawnDraft draft,
            int pawnIndex,
            RoleView role)
            => facts.MeetsCapabilityRequirement(pawnIndex, role)
                && (draft.ContainsRole(role.Id)
                    || PawnHasCoverer(facts, draft, pawnIndex, role));

        internal static bool PawnHasCoverer(
            EngineContext facts,
            PawnDraft draft,
            int pawnIndex,
            RoleView role)
        {
            if (!facts.MeetsCapabilityRequirement(pawnIndex, role)) return false;
            for (int index = 0; index < draft.RoleCount; index++)
            {
                int otherRoleId = draft.RoleAt(index);
                RoleView other = facts.RoleOf(otherRoleId);
                if (otherRoleId != role.Id
                    && other != null
                    && !other.Blocker && !other.HasRules
                    && facts.Redundant(otherRoleId, role.Id)
                    && !HigherInSharedPath(
                        facts.Colony.Paths, role.Id, otherRoleId))
                    return true;
            }
            return false;
        }

        private static bool PawnHasEarlierCoverer(
            EngineContext facts,
            PawnDraft draft,
            int pawnIndex,
            RoleView role)
        {
            if (!facts.MeetsCapabilityRequirement(pawnIndex, role)) return false;
            for (int index = 0; index < draft.RoleCount; index++)
            {
                int otherRoleId = draft.RoleAt(index);
                if (otherRoleId == role.Id) return false;
                RoleView other = facts.RoleOf(otherRoleId);
                if (other != null
                    && !other.Blocker && !other.HasRules
                    && facts.Redundant(otherRoleId, role.Id)
                    && !HigherInSharedPath(
                        facts.Colony.Paths, role.Id, otherRoleId))
                    return true;
            }
            return false;
        }

        private static bool HigherInSharedPath(
            IReadOnlyList<PathView> paths,
            int roleId,
            int otherRoleId)
        {
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                PathView path = paths[pathIndex];
                if (path.RoleIds.Count != path.BandMins.Count) continue;
                int roleAt = path.RoleIds.IndexOf(roleId);
                int otherAt = path.RoleIds.IndexOf(otherRoleId);
                if (roleAt >= 0
                    && otherAt >= 0
                    && path.BandMins[roleAt] > path.BandMins[otherAt])
                    return true;
            }
            return false;
        }

        /// The one role processing order. Selection order is skilled roles
        /// before skill-less ones, then higher natural priority, then id. Layered
        /// on top as hard "must precede" constraints: a covering role before what
        /// it covers, and a higher training-path band before its lower trainees.
        /// Built as a single constraint-respecting selection because those two
        /// relations are partial orders; a plain comparator over them would be
        /// intransitive and List.Sort could throw.
        private static List<RoleView> OrderRolesForProcessing(
            EngineContext facts, IReadOnlyList<RoleView> catalog)
        {
            int count = catalog.Count;
            var predecessors = new int[count];
            var successors = new List<int>[count];
            for (int i = 0; i < count; i++) successors[i] = new List<int>();
            IReadOnlyList<PathView> paths = facts.Colony.Paths;
            for (int i = 0; i < count; i++)
                for (int j = 0; j < count; j++)
                    if (i != j
                        && MustPrecede(facts, paths, catalog[i], catalog[j]))
                    {
                        successors[i].Add(j);
                        predecessors[j]++;
                    }
            var ordered = new List<RoleView>(count);
            var used = new bool[count];
            for (int placed = 0; placed < count; placed++)
            {
                int next = BestAvailable(catalog, used, predecessors, true);
                // A constraint cycle would strand roles; take the best remaining.
                if (next < 0)
                    next = BestAvailable(catalog, used, predecessors, false);
                used[next] = true;
                ordered.Add(catalog[next]);
                for (int k = 0; k < successors[next].Count; k++)
                    predecessors[successors[next][k]]--;
            }
            return ordered;
        }

        private static int BestAvailable(
            IReadOnlyList<RoleView> catalog,
            bool[] used,
            int[] predecessors,
            bool requireNoPredecessor)
        {
            int best = -1;
            for (int i = 0; i < catalog.Count; i++)
            {
                if (used[i]) continue;
                if (requireNoPredecessor && predecessors[i] != 0) continue;
                if (best < 0 || CompareSelection(catalog[i], catalog[best]) < 0)
                    best = i;
            }
            return best;
        }

        /// Selection order among roles with no remaining unmet constraint:
        /// skilled before skill-less, then higher natural priority, then id.
        private static int CompareSelection(RoleView left, RoleView right)
        {
            if (left.UseUnskilledPlacementRules != right.UseUnskilledPlacementRules)
                return left.UseUnskilledPlacementRules ? 1 : -1;
            int priority = right.NaturalPriority.CompareTo(left.NaturalPriority);
            if (priority != 0) return priority;
            return left.Id.CompareTo(right.Id);
        }

        /// A role must precede another when it covers it (and is not covered by
        /// it) or occupies a higher band of a shared training path. An unskilled
        /// role never precedes a skilled one.
        private static bool MustPrecede(
            EngineContext facts,
            IReadOnlyList<PathView> paths,
            RoleView left,
            RoleView right)
        {
            if (left.UseUnskilledPlacementRules
                && !right.UseUnskilledPlacementRules) return false;
            if (HigherInSharedPath(paths, left.Id, right.Id)) return true;
            return facts.Redundant(left.Id, right.Id)
                && !facts.Redundant(right.Id, left.Id);
        }
    }
}
