using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Identifies target work that must not contribute to a broader training
    /// role's skill profile: a trainer covering its target keeps the target's
    /// givers as capability units but excludes them from its own evidence.
    public static class TrainingCoverageExclusion
    {
        public static IReadOnlyDictionary<int, HashSet<string>>
            ExcludedCoverageByRole(
                IReadOnlyList<RoleView> roles,
                IReadOnlyList<PathView> paths)
        {
            var rolesById = new Dictionary<int, RoleView>();
            if (roles != null)
                for (int roleIndex = 0; roleIndex < roles.Count; roleIndex++)
                {
                    RoleView role = roles[roleIndex];
                    if (role != null) rolesById[role.Id] = role;
                }

            var result = new Dictionary<int, HashSet<string>>();
            if (paths == null) return result;
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                PathView path = paths[pathIndex];
                int targetRoleId = PathActivation.UniqueTargetRoleId(path);
                if (targetRoleId < 0
                    || !rolesById.TryGetValue(
                        targetRoleId, out RoleView target))
                    continue;

                for (int roleIndex = 0;
                     roleIndex < path.RoleIds.Count;
                     roleIndex++)
                {
                    int trainingRoleId = path.RoleIds[roleIndex];
                    if (trainingRoleId == targetRoleId
                        || !rolesById.TryGetValue(
                            trainingRoleId, out RoleView training)
                        || !CoverageMath.Covers(
                            training.Coverage, target.Coverage))
                        continue;
                    if (!result.TryGetValue(
                            trainingRoleId, out HashSet<string> excluded))
                    {
                        excluded = new HashSet<string>();
                        result.Add(trainingRoleId, excluded);
                    }
                    excluded.UnionWith(target.Coverage);
                }
            }
            return result;
        }
    }
}
