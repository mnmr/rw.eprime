using System;
using System.Collections.Generic;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles
{
    /// Single shared producer of full-fidelity RoleWorkSpecs for UI and
    /// adapter consumers. Plan builds derive their exclusion-adjusted specs
    /// from the same Core builder inside RecommendationCatalogBuilder.
    /// The contract mechanics (owner partition, revision and index stamps,
    /// equal-rebuild identity, teardown) live in Core's RoleWorkSpecCache so
    /// they run under the executable test suite; this wrapper supplies the
    /// live inputs: RoleStore.Current as owner, each role's work revision,
    /// and the JobProfileIndex snapshot identity (definition reloads replace
    /// it through JobSkillProfiles.InvalidateDefinitions). Language is
    /// deliberately not a dependency: specs hold invariant names only.
    internal static class RoleWorkSpecs
    {
        private static readonly RoleWorkSpecCache Cache = new RoleWorkSpecCache();
        private static readonly Func<Role, RoleWorkSpec> BuildSpec = Build;
        internal static void Remove(int roleId) => Cache.Remove(roleId);
        private static JobProfileIndex? prioritiesIndex;
        private static Dictionary<string, int>? naturalPriorities;

        internal static void Reset()
        {
            Cache.Reset();
            prioritiesIndex = null;
            naturalPriorities = null;
        }

        internal static RoleWorkSpec For(Role role)
        {
            if (role == null) return RoleWorkSpec.Empty;
            RoleStore? store = RoleStore.Current;
            if (store == null) return RoleWorkSpec.Empty;
            return Cache.For(
                role.id,
                store,
                UiVersion.WorkForRole(role.id),
                JobSkillProfiles.RecommendationIndex(),
                role, BuildSpec);
        }

        private static RoleWorkSpec Build(Role role)
        {
            if (role.composite)
            {
                var store = RoleStore.Current;
                var members = new List<RoleWorkSpec>();
                if (store != null)
                    foreach (int memberId in role.memberRoleIds)
                    {
                        Role? member = store.RoleById(memberId);
                        if (member == null || member.composite) continue;
                        if (member.blocker && !role.blocker) continue;
                        members.Add(For(member));
                    }
                return RoleWorkSpecBuilder.Merge(
                    role.id, members, GateUnion(role, store));
            }

            IJobCatalog jobs = GameJobCatalog.Instance;
            var seedWorkTypes = new List<string>();
            var literalWorkTypes = new List<string>();
            foreach (JobEntry entry in role.entries)
            {
                string workType;
                if (entry.Kind == JobEntryKind.WorkType)
                {
                    literalWorkTypes.Add(entry.DefName);
                    workType = entry.DefName;
                }
                else
                    workType = jobs.WorkTypeOf(entry.DefName);
                if (workType != null) seedWorkTypes.Add(workType);
            }
            return RoleWorkSpecBuilder.Build(
                role.id,
                CoverageMath.OrderedCoverageOf(role.entries, jobs),
                seedWorkTypes,
                literalWorkTypes,
                jobs.WorkTypeOf,
                NaturalPriorities(),
                JobSkillProfiles.GiverFacts(),
                JobSkillProfiles.RecommendationIndex(),
                role.requiredSkills);
        }

        private static IReadOnlyList<string> GateUnion(Role role, RoleStore? store)
        {
            var gates = new List<string>(role.requiredSkills);
            if (store == null) return gates;
            foreach (int memberId in role.memberRoleIds)
            {
                Role? member = store.RoleById(memberId);
                if (member == null || member.composite) continue;
                if (member.blocker && !role.blocker) continue;
                foreach (string gate in member.requiredSkills)
                    if (!gates.Contains(gate)) gates.Add(gate);
            }
            return gates;
        }

        /// Keyed by the index snapshot identity: a definition reload replaces
        /// the index and rebuilds this map on the next read.
        private static Dictionary<string, int> NaturalPriorities()
        {
            JobProfileIndex index = JobSkillProfiles.RecommendationIndex();
            if (naturalPriorities != null
                && ReferenceEquals(prioritiesIndex, index))
                return naturalPriorities;
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (WorkTypeDef workType
                in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                if (workType != null)
                    map[workType.defName] = workType.naturalPriority;
            prioritiesIndex = index;
            return naturalPriorities = map;
        }
    }
}
