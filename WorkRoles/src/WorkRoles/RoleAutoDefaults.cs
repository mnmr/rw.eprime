using System.Collections.Generic;
using System.Linq;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles
{
    internal readonly struct RoleHolderDefaults
    {
        internal RoleHolderDefaults(int min, int max, int train)
        {
            Min = min;
            Max = max;
            Train = train;
        }

        internal int Min { get; }
        internal int Max { get; }
        internal int Train { get; }
    }

    /// Resolves Auto policy for player roles by matching their job coverage to
    /// the closest shipped role definition. Zero-overlap roles use a stable
    /// skilled or purely-unskilled fallback.
    internal static class RoleAutoDefaults
    {
        internal static RoleHolderDefaults Resolve(Role role)
        {
            var exact = role.templateDefName == null ? null
                : DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
            if (exact != null) return FromDef(exact);

            HashSet<string> coverage = role.Coverage();
            string primary = RoleSkillProfiles.ForCoverage(coverage)
                .FirstOrDefault(s => s.Primary)?.SkillDefName;
            var defsByName = new Dictionary<string, RoleDef>();
            var candidates = new List<RoleTemplateCandidate>();
            foreach (var def in DefDatabase<RoleDef>.AllDefsListForReading)
            {
                if (def.autoAssign || def.blocker || def.HasRules()) continue;
                var defCoverage = CoverageMath.CoverageOf(def.ParsedEntries(), GameJobCatalog.Instance);
                string defPrimary = RoleSkillProfiles.ForCoverage(defCoverage)
                    .FirstOrDefault(s => s.Primary)?.SkillDefName;
                defsByName[def.defName] = def;
                candidates.Add(new RoleTemplateCandidate(
                    def.defName, defCoverage, defPrimary));
            }
            var match = RoleTemplateMatcher.Closest(coverage, primary, candidates);
            if (match != null) return FromDef(defsByName[match.Key]);
            bool unskilled = RoleSkillProfiles.ForCoverage(coverage).Count == 0;
            return new RoleHolderDefaults(unskilled ? 8 : 1,
                RoleHolderRange.Uncapped, 0);
        }

        private static RoleHolderDefaults FromDef(RoleDef def)
            => new RoleHolderDefaults(def.minHolders.Count, def.maxHolders,
                RoleHolderPolicy.WithTraining(
                    def.minHolders.Count, def.minHolders.Waivers));
    }

    internal static class RoleDefPolicyExtensions
    {
        internal static bool HasRules(this RoleDef def)
            => !string.IsNullOrEmpty(def.activeHours)
            || def.locations != null && def.locations.Count > 0;
    }
}
