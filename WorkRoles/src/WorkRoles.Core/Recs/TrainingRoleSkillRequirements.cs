using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// <summary>
    /// Derives hard skill requirements from role evidence and training paths,
    /// and identifies target work that must not contribute to a broader
    /// training role's profile. Job-to-skill facts remain adapter-owned.
    /// </summary>
    public static class TrainingRoleSkillRequirements
    {
        public static void ApplyTargetRequirements(
            IReadOnlyList<RoleView> roles,
            IReadOnlyList<PathView> paths)
        {
            Dictionary<int, RoleView> rolesById = RolesById(roles);
            if (roles != null)
                for (int roleIndex = 0;
                     roleIndex < roles.Count;
                     roleIndex++)
                {
                    RoleView role = roles[roleIndex];
                    if (role == null) continue;
                    for (int skillIndex = 0;
                         skillIndex < role.Skills.Count;
                         skillIndex++)
                        role.Skills[skillIndex].Required =
                            IsBaseSkillRequired(role.Skills[skillIndex]);
                }
            if (paths == null) return;
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                PathView path = paths[pathIndex];
                int targetRoleId = PathActivation.UniqueTargetRoleId(path);
                if (targetRoleId < 0
                    || !rolesById.TryGetValue(
                        targetRoleId, out RoleView target))
                    continue;
                for (int skillIndex = 0;
                     skillIndex < target.Skills.Count;
                     skillIndex++)
                    if (IsTargetSkillRequired(
                            rolesById,
                            target,
                            path,
                            target.Skills[skillIndex]))
                        target.Skills[skillIndex].Required = true;
            }
        }

        internal static bool IsBaseSkillRequired(RoleSkillView skill) =>
            skill.Primary || skill.RequiredContent > 0;

        internal static bool IsTargetSkillRequired(
            IReadOnlyDictionary<int, RoleView> rolesById,
            RoleView target,
            PathView path,
            RoleSkillView targetSkill)
        {
            if (IsBaseSkillRequired(targetSkill))
                return true;
            for (int roleIndex = 0;
                 roleIndex < path.RoleIds.Count;
                 roleIndex++)
            {
                int roleId = path.RoleIds[roleIndex];
                if (roleId == target.Id
                    || !rolesById.TryGetValue(
                        roleId, out RoleView trainingRole))
                    continue;
                for (int skillIndex = 0;
                     skillIndex < trainingRole.Skills.Count;
                     skillIndex++)
                {
                    RoleSkillView trainingSkill =
                        trainingRole.Skills[skillIndex];
                    if (trainingSkill.Primary
                        && trainingSkill.SkillDefName
                            == targetSkill.SkillDefName)
                        return true;
                }
            }
            return false;
        }

        public static IReadOnlyDictionary<int, HashSet<string>>
            ExcludedCoverageByRole(
                IReadOnlyList<RoleView> roles,
                IReadOnlyList<PathView> paths)
        {
            Dictionary<int, RoleView> rolesById = RolesById(roles);

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

        private static Dictionary<int, RoleView> RolesById(
            IReadOnlyList<RoleView> roles)
        {
            var result = new Dictionary<int, RoleView>();
            if (roles == null) return result;
            for (int roleIndex = 0; roleIndex < roles.Count; roleIndex++)
            {
                RoleView role = roles[roleIndex];
                if (role != null) result[role.Id] = role;
            }
            return result;
        }
    }
}
