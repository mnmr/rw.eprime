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

        internal PathActivation ActivationToward(int targetRoleId)
        {
            for (int index = 0; index < activations.Count; index++)
                if (activations[index].TargetRoleId == targetRoleId)
                    return activations[index];
            return null;
        }

        internal int RoleCount => roleIds.Count;
        internal int RoleAt(int index) => roleIds[index];

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

            var edges = new bool[count, count];
            var incoming = new int[count];
            AddLeadEdges(facts.Colony.Paths, edges, incoming);

            var result = new int[count];
            var emitted = new bool[count];
            for (int outputIndex = 0; outputIndex < count; outputIndex++)
            {
                int next = BestAvailableRole(
                    emitted,
                    incoming,
                    keys,
                    positions,
                    requireNoIncoming: true);
                if (next < 0)
                    next = BestAvailableRole(
                        emitted,
                        incoming,
                        keys,
                        positions,
                        requireNoIncoming: false);
                emitted[next] = true;
                result[outputIndex] = roleIds[next];
                for (int roleAt = 0; roleAt < count; roleAt++)
                    if (edges[next, roleAt]) incoming[roleAt]--;
            }
            for (int roleAt = 0; roleAt < count; roleAt++)
                incoming[roleAt] = OrderingScore(
                    facts, pawnIndex, roleIds[roleAt], formulas);
            int[] trimmed = WithoutPlacedHunter(facts, pawnIndex, result);
            int[] ordered = Ordering.PreserveProtectedOrder(
                facts, pawnIndex, trimmed);
            OrderByScore(
                facts, pawnIndex, positions, edges, incoming, ordered);
            if (!ReferenceEquals(trimmed, result))
                ordered = PlaceHunter(facts, pawnIndex, ordered);
            SlideHunterPastAutoRoles(facts, ordered);
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

        // Hunter must never sit immediately left of an auto (rule-based) or blocker role; slide it right past any contiguous run of them.
        private static void SlideHunterPastAutoRoles(EngineContext facts, int[] ordered)
        {
            int hunterRoleId = facts.Colony.HunterRoleId;
            if (hunterRoleId < 0) return;
            int at = -1;
            for (int index = 0; index < ordered.Length; index++)
                if (ordered[index] == hunterRoleId) { at = index; break; }
            if (at < 0) return;
            while (at + 1 < ordered.Length && PreferRoleBeforeHunter(facts, ordered[at + 1]))
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
            bool[,] edges,
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
                        || edges[leftAt, rightAt]
                        || edges[rightAt, leftAt]
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

        private bool IsOrderingBarrier(
            EngineContext facts,
            int pawnIndex,
            int roleId)
        {
            RoleView role = facts.RoleOf(roleId);
            return role == null
                || role.PreserveRecommendationOrder
                || IsSpecialRole(roleId)
                || leadRoleIds.Contains(roleId)
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
                role.Time);
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
            PathActivation activation = ActivationFor(path.Id);
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

        private PathActivation ActivationFor(int pathId)
        {
            for (int index = 0; index < activations.Count; index++)
                if (activations[index].PathId == pathId) return activations[index];
            return null;
        }

        private int BestAvailableRole(
            bool[] emitted,
            int[] incoming,
            long[] keys,
            IReadOnlyDictionary<int, long> positions,
            bool requireNoIncoming)
        {
            int best = -1;
            for (int index = 0; index < roleIds.Count; index++)
            {
                if (emitted[index]
                    || requireNoIncoming && incoming[index] != 0)
                    continue;
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

        private RecommendationPlan(
            int[][] rolesByPawn,
            int[][] pathsByPawn,
            int[] activatedPathCountsByPawn,
            Dictionary<int, RoleRecommendationExplanation>[]
                explanationsByPawn,
            RecommendationTargetAssignment[] targetAssignments)
        {
            this.rolesByPawn = rolesByPawn;
            this.pathsByPawn = pathsByPawn;
            this.activatedPathCountsByPawn = activatedPathCountsByPawn;
            this.explanationsByPawn = explanationsByPawn;
            this.targetAssignments = targetAssignments;
        }

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
        {
            var formulas = new RecommendationFormulaEngine(options);
            var facts = new EngineContext(colony);
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
                    || !role.Enabled
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
                    PathActivation activation =
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
            // Lead diversification is intentionally disabled while its
            // interaction with champion and minimum-pick ordering is evaluated.
            AddLateSpecialRoles(facts, drafts, formulas);

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
                    facts, drafts, formulas, targetAssignments);
            SubstituteComposites(facts, rolesByPawn, explanations);
            return new RecommendationPlan(
                rolesByPawn,
                pathsByPawn,
                activatedPathCountsByPawn,
                explanations,
                targetAssignments);
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
                    PathActivation activation = draft.ActivationToward(
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
            internal int Id;
            internal int[] MemberIds;
        }

        /// Post-planning pass: collapse a consecutive, in-member-order run of a
        /// composite's members into the composite. This is job-priority-neutral —
        /// a composite emits exactly its members' givers in member order and the
        /// surrounding roles are unchanged, so the compiled order is identical —
        /// which is why the match is a plain role-id window compare, never a
        /// giver recompute. The composite's explanation carries the folded
        /// members' explanations so nothing is lost.
        private static void SubstituteComposites(
            EngineContext facts,
            int[][] rolesByPawn,
            Dictionary<int, RoleRecommendationExplanation>[] explanationsByPawn)
        {
            var byFirstMember = new Dictionary<int, List<CompositeSpec>>();
            IReadOnlyList<RoleView> roles = facts.Colony.Roles;
            for (int index = 0; index < roles.Count; index++)
            {
                RoleView role = roles[index];
                if (role.MemberRoleIds == null || role.MemberRoleIds.Count == 0)
                    continue;
                var members = new int[role.MemberRoleIds.Count];
                for (int m = 0; m < members.Length; m++)
                    members[m] = role.MemberRoleIds[m];
                var spec = new CompositeSpec { Id = role.Id, MemberIds = members };
                if (!byFirstMember.TryGetValue(members[0], out List<CompositeSpec> list))
                    byFirstMember[members[0]] = list = new List<CompositeSpec>();
                list.Add(spec);
            }
            if (byFirstMember.Count == 0) return;

            var scratch = new List<int>();
            for (int pawnIndex = 0; pawnIndex < rolesByPawn.Length; pawnIndex++)
            {
                int[] published = rolesByPawn[pawnIndex];
                scratch.Clear();
                bool changed = false;
                for (int i = 0; i < published.Length;)
                {
                    CompositeSpec match = LongestCompositeMatch(
                        byFirstMember, published, i);
                    if (match == null)
                    {
                        scratch.Add(published[i]);
                        i++;
                        continue;
                    }
                    scratch.Add(match.Id);
                    AddBundledExplanation(explanationsByPawn[pawnIndex], match);
                    i += match.MemberIds.Length;
                    changed = true;
                }
                if (changed) rolesByPawn[pawnIndex] = scratch.ToArray();
            }
        }

        /// The longest composite whose member ids equal published[start..] in
        /// order; ties broken by smallest composite id. Member roles are unique
        /// per pawn, so a composite matches at most one window.
        private static CompositeSpec LongestCompositeMatch(
            Dictionary<int, List<CompositeSpec>> byFirstMember,
            int[] published,
            int start)
        {
            if (!byFirstMember.TryGetValue(
                    published[start], out List<CompositeSpec> candidates))
                return null;
            CompositeSpec best = null;
            for (int c = 0; c < candidates.Count; c++)
            {
                int[] members = candidates[c].MemberIds;
                if (start + members.Length > published.Length) continue;
                bool match = true;
                for (int m = 1; m < members.Length; m++)
                    if (published[start + m] != members[m]) { match = false; break; }
                if (!match) continue;
                if (best == null
                    || members.Length > best.MemberIds.Length
                    || members.Length == best.MemberIds.Length
                        && candidates[c].Id < best.Id)
                    best = candidates[c];
            }
            return best;
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
            explanations[spec.Id] = new RoleRecommendationExplanation
            {
                RoleId = spec.Id,
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
            => draft.ContainsRole(role.Id)
            || PawnHasCoverer(facts, draft, pawnIndex, role);

        internal static bool PawnHasCoverer(
            EngineContext facts,
            PawnDraft draft,
            int pawnIndex,
            RoleView role)
        {
            if (!facts.FullyCapable(pawnIndex, role)) return false;
            for (int index = 0; index < draft.RoleCount; index++)
            {
                int otherRoleId = draft.RoleAt(index);
                RoleView other = facts.RoleOf(otherRoleId);
                if (otherRoleId != role.Id
                    && other != null
                    && !other.Blocker
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
            if (!facts.FullyCapable(pawnIndex, role)) return false;
            for (int index = 0; index < draft.RoleCount; index++)
            {
                int otherRoleId = draft.RoleAt(index);
                if (otherRoleId == role.Id) return false;
                RoleView other = facts.RoleOf(otherRoleId);
                if (other != null
                    && !other.Blocker
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
            if (left.Unskilled != right.Unskilled)
                return left.Unskilled ? 1 : -1;
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
            if (left.Unskilled && !right.Unskilled) return false;
            if (HigherInSharedPath(paths, left.Id, right.Id)) return true;
            return facts.Redundant(left.Id, right.Id)
                && !facts.Redundant(right.Id, left.Id);
        }
    }
}
