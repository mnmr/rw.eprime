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
        private readonly List<PathActivation> activations =
            new List<PathActivation>();

        internal void AddRole(
            int roleId,
            byte source = DirectSource,
            byte minimumBonus = 0)
        {
            int index = roleIds.IndexOf(roleId);
            if (index >= 0)
            {
                roleSources[index] |= source;
                if (minimumBonus > roleMinimumBonuses[index])
                    roleMinimumBonuses[index] = minimumBonus;
                return;
            }
            roleIds.Add(roleId);
            roleSources.Add(source);
            roleMinimumBonuses.Add(minimumBonus);
        }

        internal bool ContainsRole(int roleId) => roleIds.Contains(roleId);

        internal void RemoveRole(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            if (index < 0) return;
            roleIds.RemoveAt(index);
            roleSources.RemoveAt(index);
            roleMinimumBonuses.RemoveAt(index);
        }

        internal bool IsPathRole(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            return index >= 0 && (roleSources[index] & PathSource) != 0;
        }

        private bool HasDirectRole(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            return index >= 0 && (roleSources[index] & DirectSource) != 0;
        }

        private byte MinimumBonusOf(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            return index < 0 ? (byte)0 : roleMinimumBonuses[index];
        }

        internal int RoleCount => roleIds.Count;
        internal int RoleAt(int index) => roleIds[index];

        internal void AddActivation(
            PathActivation activation,
            byte minimumBonus = 0)
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
                    minimumBonus);
        }

        internal int[] PublishRoles(
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

            int count = roleIds.Count;
            if (count < 2) return roleIds.ToArray();
            var keys = new long[count];
            for (int index = 0; index < count; index++)
                keys[index] = positions[roleIds[index]];
            ApplySpecialKeys(facts, keys);

            var targetKeys = new long[placements.Count];
            var targetStates = new byte[placements.Count];
            for (int placementIndex = 0;
                 placementIndex < placements.Count;
                 placementIndex++)
                ResolveTargetKey(
                    placementIndex,
                    placements,
                    facts,
                    pawnIndex,
                    positions,
                    targetKeys,
                    targetStates);

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
            int[] ordered = Ordering.PreserveProtectedOrder(
                facts, pawnIndex, result);
            OrderByScore(
                facts, pawnIndex, positions, edges, incoming, ordered);
            return ordered;
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
        {
            IReadOnlyList<RoleSkillView> left = facts.RequiredSkills(
                facts.RoleOf(leftRoleId));
            IReadOnlyList<RoleSkillView> right = facts.RequiredSkills(
                facts.RoleOf(rightRoleId));
            for (int leftAt = 0; leftAt < left.Count; leftAt++)
                for (int rightAt = 0; rightAt < right.Count; rightAt++)
                    if (left[leftAt].SkillDefName
                        == right[rightAt].SkillDefName)
                        return true;
            return false;
        }

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
            SignalBucket verdict = facts.BestSignal(
                pawnIndex,
                facts.RoleOf(roleId),
                out string skillDefName,
                out _);
            return formulas.OrderingScore(
                verdict,
                facts.SkillLevel(pawnIndex, skillDefName),
                MinimumBonusOf(roleId));
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

        private long ResolveTargetKey(
            int placementIndex,
            List<PathView> placements,
            EngineContext facts,
            int pawnIndex,
            IReadOnlyDictionary<int, long> positions,
            long[] targetKeys,
            byte[] states)
        {
            PathView path = placements[placementIndex];
            int targetRoleId = PathActivation.UniqueTargetRoleId(path);
            if (states[placementIndex] == 2) return targetKeys[placementIndex];
            if (states[placementIndex] == 1)
                return positions[targetRoleId];
            states[placementIndex] = 1;
            if (!positions.TryGetValue(path.AnchorRoleId, out long anchorKey))
            {
                targetKeys[placementIndex] = positions[targetRoleId];
                states[placementIndex] = 2;
                return targetKeys[placementIndex];
            }
            int anchorPlacement = PlacementTargeting(placements, path.AnchorRoleId);
            if (anchorPlacement >= 0 && anchorPlacement != placementIndex)
                anchorKey = ResolveTargetKey(
                    anchorPlacement,
                    placements,
                    facts,
                    pawnIndex,
                    positions,
                    targetKeys,
                    states);

            int rank = 0;
            int groupCount = 0;
            for (int otherIndex = 0; otherIndex < placements.Count; otherIndex++)
            {
                PathView otherPath = placements[otherIndex];
                if (otherPath.AnchorRoleId != path.AnchorRoleId
                    || otherPath.AnchorBefore != path.AnchorBefore)
                    continue;
                groupCount++;
                if (ComparePaths(
                        otherPath,
                        path,
                        facts,
                        pawnIndex,
                        positions) < 0)
                    rank++;
            }
            targetKeys[placementIndex] = path.AnchorBefore
                ? anchorKey - (groupCount - rank) * 1000L
                : anchorKey + (rank + 1) * 1000L;
            states[placementIndex] = 2;
            return targetKeys[placementIndex];
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

        private static int ComparePaths(
            PathView left,
            PathView right,
            EngineContext facts,
            int pawnIndex,
            IReadOnlyDictionary<int, long> positions)
        {
            int leftTargetRoleId = PathActivation.UniqueTargetRoleId(left);
            int rightTargetRoleId = PathActivation.UniqueTargetRoleId(right);
            RoleView leftTarget = facts.RoleOf(leftTargetRoleId);
            RoleView rightTarget = facts.RoleOf(rightTargetRoleId);
            SignalBucket leftVerdict = facts.BestSignal(
                pawnIndex, leftTarget, out string leftSkill, out _);
            SignalBucket rightVerdict = facts.BestSignal(
                pawnIndex, rightTarget, out string rightSkill, out _);
            int verdict = rightVerdict.CompareTo(leftVerdict);
            if (verdict != 0) return verdict;
            int skill = facts.SkillLevel(pawnIndex, rightSkill)
                .CompareTo(facts.SkillLevel(pawnIndex, leftSkill));
            if (skill != 0) return skill;
            int position = positions[leftTargetRoleId]
                .CompareTo(positions[rightTargetRoleId]);
            if (position != 0) return position;
            int structure = PathActivation.CompareStructure(left, right);
            return structure != 0
                ? structure
                : left.Id.CompareTo(right.Id);
        }
    }

    public sealed partial class RecommendationPlan
    {
        private readonly int[][] rolesByPawn;
        private readonly int[][] pathsByPawn;
        private readonly int[] activatedPathCountsByPawn;

        private RecommendationPlan(
            int[][] rolesByPawn,
            int[][] pathsByPawn,
            int[] activatedPathCountsByPawn)
        {
            this.rolesByPawn = rolesByPawn;
            this.pathsByPawn = pathsByPawn;
            this.activatedPathCountsByPawn = activatedPathCountsByPawn;
        }

        public int PawnCount => rolesByPawn.Length;
        public int RoleCountAt(int pawnIndex) => rolesByPawn[pawnIndex].Length;
        public int RoleAt(int pawnIndex, int index) => rolesByPawn[pawnIndex][index];
        public int PathCountAt(int pawnIndex) => pathsByPawn[pawnIndex].Length;
        public int PathAt(int pawnIndex, int index) => pathsByPawn[pawnIndex][index];
        public bool PathActivatedAt(int pawnIndex, int index) =>
            index < activatedPathCountsByPawn[pawnIndex];

        public static RecommendationPlan Build(ColonyView colony)
            => Build(colony, RecommendationsTuningOptions.Default);

        public static RecommendationPlan Build(
            ColonyView colony,
            RecommendationsTuningOptions options)
        {
            var formulas = new RecommendationFormulaEngine(options);
            var facts = new EngineContext(colony, initializeRuleState: false);
            int pawnCount = colony.Pawns.Count;
            var drafts = new PawnDraft[pawnCount];
            for (int pawnIndex = 0; pawnIndex < pawnCount; pawnIndex++)
                drafts[pawnIndex] = new PawnDraft();
            AddSpecialRoles(facts, drafts);

            IReadOnlyDictionary<int, long> positions = facts.BasePositions();
            var roles = new List<RoleView>(colony.Roles);
            roles.Sort((left, right) =>
            {
                int position = positions[left.Id].CompareTo(positions[right.Id]);
                return position != 0 ? position : left.Id.CompareTo(right.Id);
            });
            var scaling = new UnitScaling();
            var rolePlans = new List<RolePlan>();
            for (int roleIndex = 0; roleIndex < roles.Count; roleIndex++)
            {
                RoleView role = roles[roleIndex];
                if (!role.Available
                    || !role.Enabled
                    || role.HolderMode == RoleHolderMode.Never
                    || role.AutoAssign
                    || role.HasRules
                    || role.Blocker
                    || role.Unskilled
                    || role.Hunting
                    || role.Id == colony.HunterRoleId)
                    continue;
                RolePlan rolePlan = RolePlan.Build(
                    facts, role, scaling, formulas);
                rolePlans.Add(rolePlan);
            }

            for (int planIndex = 0; planIndex < rolePlans.Count; planIndex++)
            {
                RolePlan rolePlan = rolePlans[planIndex];
                RoleView role = facts.RoleOf(rolePlan.RoleId);
                for (int candidateIndex = 0;
                     candidateIndex < rolePlan.CandidateCount;
                     candidateIndex++)
                {
                    if (!rolePlan.IsSelected(candidateIndex)) continue;
                    int pawnIndex = rolePlan.CandidateAt(candidateIndex).PawnIndex;
                    bool surplus = rolePlan.IsSurplus(candidateIndex);
                    byte minimumBonus =
                        rolePlan.MinimumBonusAt(candidateIndex);
                    if (surplus
                        && !PathActivation.TargetBandContains(
                            facts, pawnIndex, role))
                    {
                        PathActivation activation = PathActivation.Find(
                            facts,
                            pawnIndex,
                            role);
                        if (activation != null)
                            drafts[pawnIndex].AddActivation(
                                activation, minimumBonus);
                        continue;
                    }
                    if (rolePlan.IsTrainingWaiver(candidateIndex))
                    {
                        PathActivation activation = PathActivation.Find(
                            facts,
                            pawnIndex,
                            role);
                        if (activation != null)
                        {
                            drafts[pawnIndex].AddActivation(
                                activation, minimumBonus);
                            continue;
                        }
                    }
                    drafts[pawnIndex].AddRole(
                        role.Id, minimumBonus: minimumBonus);
                }
            }

            ResolveCoverage(facts, rolePlans, drafts);
            DiversifyQualifiedLeads(facts, drafts, positions);
            AddLateSpecialRoles(facts, drafts);

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
                    out activatedPathCountsByPawn[pawnIndex]);
            }
            return new RecommendationPlan(
                rolesByPawn, pathsByPawn, activatedPathCountsByPawn);
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
                        plan.SelectForCoverage(candidateIndex);
                    drafts[pawnIndex].AddRole(
                        role.Id, minimumBonus: minimumBonus);
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

        private static bool PawnHasCoverer(
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
    }
}
