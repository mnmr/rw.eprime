using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    internal sealed class PathActivation
    {
        private PathActivation(
            int pathId,
            int targetRoleId,
            int[] activeRoleIds)
        {
            PathId = pathId;
            TargetRoleId = targetRoleId;
            ActiveRoleIds = activeRoleIds;
        }

        internal int PathId { get; }
        internal int TargetRoleId { get; }
        internal int[] ActiveRoleIds { get; }

        internal static PathActivation Find(
            EngineContext facts,
            int pawnIndex,
            RoleView target,
            RecommendationFormulaEngine formulas)
        {
            PathActivation best = null;
            PathView bestPath = null;
            for (int pathIndex = 0;
                 pathIndex < facts.Colony.Paths.Count;
                 pathIndex++)
            {
                PathView path = facts.Colony.Paths[pathIndex];
                PathActivation candidate = Build(
                    facts, pawnIndex, target, path, formulas);
                if (candidate == null) continue;
                int structure = best == null
                    ? -1 : CompareStructure(path, bestPath);
                if (best == null
                    || structure < 0
                    || structure == 0 && path.Id < bestPath.Id)
                {
                    best = candidate;
                    bestPath = path;
                }
            }
            return best;
        }

        private static PathActivation Build(
            EngineContext facts,
            int pawnIndex,
            RoleView target,
            PathView path,
            RecommendationFormulaEngine formulas)
        {
            int count = path.RoleIds.Count;
            if (count == 0
                || path.BandMins.Count != count
                || path.BandMaxes.Count != count)
                return null;
            if (UniqueTargetRoleId(path) != target.Id)
                return null;

            EngineContext.PathSkillModel model = facts.PathSkills(path);
            var activeRoles = new List<int>();
            var activeEntries = new bool[count];
            for (int entry = 0; entry < count; entry++)
            {
                RoleView role = facts.RoleOf(path.RoleIds[entry]);
                // A training role is Never by design (no own demand); it is
                // still a valid path step to assign as the target's substitute,
                // so Never does not exclude it here. A role with an empty
                // contribution fails InsideBand and cannot substitute.
                if (role == null
                    || !role.Available
                    || !role.Enabled
                    || !facts.MeetsCapabilityRequirement(pawnIndex, role)
                    || facts.BestSignal(pawnIndex, role, out _, out _)
                        < formulas.PathMinimumSignal
                    || !facts.InsideBand(pawnIndex, role, path, entry))
                    continue;
                activeEntries[entry] = true;
                if (!activeRoles.Contains(role.Id)) activeRoles.Add(role.Id);
            }
            if (activeRoles.Count == 0) return null;

            // Every path-covered skill still below the target band must be
            // trainable by an active non-target entry whose band holds the
            // pawn's level in that skill; otherwise the path is unavailable.
            int targetAt = path.RoleIds.IndexOf(target.Id);
            PawnView pawn = facts.Colony.Pawns[pawnIndex];
            for (int index = 0; index < model.Covered.Length; index++)
            {
                string skill = model.Covered[index];
                bool hasLevel = pawn.SkillLevels.TryGetValue(
                    skill, out int level);
                if (hasLevel && level >= path.BandMins[targetAt]) continue;
                bool coveredByActive = false;
                for (int entry = 0; entry < count && !coveredByActive; entry++)
                {
                    if (!activeEntries[entry] || entry == targetAt) continue;
                    string[] contribution = model.Contributions[entry];
                    for (int at = 0; at < contribution.Length; at++)
                        if (contribution[at] == skill
                            && hasLevel
                            && PathMath.InsideBand(path, entry, level))
                        {
                            coveredByActive = true;
                            break;
                        }
                }
                if (!coveredByActive) return null;
            }
            return new PathActivation(
                path.Id,
                target.Id,
                activeRoles.ToArray());
        }

        internal static int UniqueTargetRoleId(PathView path)
        {
            int count = path.RoleIds.Count;
            if (count == 0
                || path.BandMins.Count != count
                || path.BandMaxes.Count != count)
                return -1;
            int highestMin = int.MinValue;
            int highestIndex = -1;
            bool uniqueHighest = true;
            for (int index = 0; index < count; index++)
            {
                int bandMin = path.BandMins[index];
                if (bandMin > highestMin)
                {
                    highestMin = bandMin;
                    highestIndex = index;
                    uniqueHighest = true;
                }
                else if (bandMin == highestMin)
                {
                    uniqueHighest = false;
                }
            }
            return uniqueHighest ? path.RoleIds[highestIndex] : -1;
        }

        internal static int CompareStructure(PathView left, PathView right)
        {
            int count = System.Math.Min(left.RoleIds.Count, right.RoleIds.Count);
            for (int index = 0; index < count; index++)
            {
                int min = left.BandMins[index].CompareTo(right.BandMins[index]);
                if (min != 0) return min;
                int max = left.BandMaxes[index].CompareTo(right.BandMaxes[index]);
                if (max != 0) return max;
                int role = left.RoleIds[index].CompareTo(right.RoleIds[index]);
                if (role != 0) return role;
            }
            return left.RoleIds.Count.CompareTo(right.RoleIds.Count);
        }

        internal static int TargetMinimum(
            IReadOnlyList<PathView> paths,
            int roleId)
        {
            int result = -1;
            for (int index = 0; index < paths.Count; index++)
            {
                PathView path = paths[index];
                if (UniqueTargetRoleId(path) != roleId) continue;
                int roleAt = path.RoleIds.IndexOf(roleId);
                if (path.BandMins[roleAt] > result)
                    result = path.BandMins[roleAt];
            }
            return result;
        }

        internal static PathView PreferredTargetPath(
            IReadOnlyList<PathView> paths,
            int targetRoleId)
        {
            PathView best = null;
            for (int index = 0; index < paths.Count; index++)
            {
                PathView candidate = paths[index];
                if (UniqueTargetRoleId(candidate) != targetRoleId) continue;
                int structure = best == null
                    ? -1
                    : CompareStructure(candidate, best);
                if (best == null
                    || structure < 0
                    || structure == 0 && candidate.Id < best.Id)
                    best = candidate;
            }
            return best;
        }

        /// True when the pawn sits in a higher band than this role on a shared
        /// path and is not in this role's own band: the path places the pawn at
        /// the higher role, so it should not also hold this lower trainee role.
        /// Overlapping bands (pawn in both) keep the role; a below-band pawn is
        /// left eligible so a forced or downgraded target still resolves.
        internal static bool BelongsToHigherBand(
            EngineContext facts,
            int pawnIndex,
            RoleView role)
        {
            IReadOnlyList<PathView> paths = facts.Colony.Paths;
            bool higher = false;
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                PathView path = paths[pathIndex];
                int count = path.RoleIds.Count;
                if (count == 0
                    || path.BandMins.Count != count
                    || path.BandMaxes.Count != count)
                    continue;
                int entry = path.RoleIds.IndexOf(role.Id);
                if (entry < 0) continue;
                if (facts.InsideBand(pawnIndex, role, path, entry))
                    return false;
                int ownMin = path.BandMins[entry];
                for (int higherEntry = 0; higherEntry < count; higherEntry++)
                {
                    if (path.BandMins[higherEntry] <= ownMin) continue;
                    RoleView higherRole =
                        facts.RoleOf(path.RoleIds[higherEntry]);
                    if (higherRole != null
                        && facts.InsideBand(
                            pawnIndex, higherRole, path, higherEntry))
                        higher = true;
                }
            }
            return higher;
        }

        internal static bool TargetBandContains(
            EngineContext facts,
            int pawnIndex,
            RoleView target)
        {
            bool targetHasPath = false;
            for (int pathIndex = 0;
                 pathIndex < facts.Colony.Paths.Count;
                 pathIndex++)
            {
                PathView path = facts.Colony.Paths[pathIndex];
                if (UniqueTargetRoleId(path) != target.Id) continue;
                targetHasPath = true;
                int targetEntry = path.RoleIds.IndexOf(target.Id);
                if (facts.InsideBand(pawnIndex, target, path, targetEntry))
                    return true;
            }
            return !targetHasPath;
        }

        internal static bool QualifiesOptionalTarget(
            EngineContext facts,
            int pawnIndex,
            RoleView target,
            RecommendationFormulaEngine formulas,
            out bool qualifiedByMultiSkillAptitude)
        {
            qualifiedByMultiSkillAptitude = false;
            bool targetHasPath = false;
            for (int pathIndex = 0;
                 pathIndex < facts.Colony.Paths.Count;
                 pathIndex++)
            {
                if (UniqueTargetRoleId(facts.Colony.Paths[pathIndex])
                    == target.Id)
                {
                    targetHasPath = true;
                    break;
                }
            }
            if (!targetHasPath) return true;

            PathActivation activation = Find(
                facts, pawnIndex, target, formulas);
            if (activation == null) return false;

            // Aptitude is judged over the path's qualifying skills (primary,
            // gated, trainer-primary), as the pre-spec engine did.
            PathView path = facts.PathsById[activation.PathId];
            string[] covered = facts.PathSkills(path).Qualifying;
            if (covered.Length < formulas.OptionalTargetMinimumSkillCount)
                return true;

            int points = 0;
            PawnView pawn = facts.Colony.Pawns[pawnIndex];
            for (int index = 0; index < covered.Length; index++)
            {
                string skill = covered[index];
                if (!pawn.SkillLevels.TryGetValue(skill, out int level))
                    return false;
                SignalBucket signal = pawn.SignalBuckets.TryGetValue(
                    skill, out SignalBucket classified)
                    ? classified
                    : SignalBucket.Neutral;
                if (signal < formulas.OptionalTargetMinimumSignal)
                    return false;
                signal = formulas.PromoteSkillSignal(level, signal);
                points += signal - formulas.OptionalTargetMinimumSignal;
            }
            qualifiedByMultiSkillAptitude = points >=
                formulas.OptionalTargetMinimumPoints;
            return qualifiedByMultiSkillAptitude;
        }


        internal static bool Connected(
            IReadOnlyList<PathView> paths,
            int leftRoleId,
            int rightRoleId)
        {
            if (leftRoleId == rightRoleId) return true;
            var reached = new HashSet<int> { leftRoleId };
            var frontier = new List<int> { leftRoleId };
            for (int frontierIndex = 0;
                 frontierIndex < frontier.Count;
                 frontierIndex++)
            {
                int current = frontier[frontierIndex];
                for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
                {
                    PathView path = paths[pathIndex];
                    if (!path.RoleIds.Contains(current)) continue;
                    for (int roleIndex = 0;
                         roleIndex < path.RoleIds.Count;
                         roleIndex++)
                    {
                        int roleId = path.RoleIds[roleIndex];
                        if (roleId == rightRoleId) return true;
                        if (reached.Add(roleId)) frontier.Add(roleId);
                    }
                }
            }
            return false;
        }
    }
}
