using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// The recommendation-order template over RoleViews: the stored list pins
    /// positions for its members; everything else floats via
    /// Ordering.BasePositions. The default is the shipped template-def order
    /// below, so an unedited template is never empty and Restore Defaults can
    /// return to it.
    public static class OrderTemplate
    {
        /// The shipped default order, by template def name. Absent defs
        /// (DLC/mod-gated or deleted roles) are skipped at resolve time.
        public static readonly IReadOnlyList<string> DefaultTemplateDefNames = new[]
        {
            "WS_Core",
            "WS_Doctor",
            "WS_Basics",
            "WS_Childminder",
            "WS_Warden",
            "WS_Handler",
            "WS_Cook",
            "WS_Builder",
            "WS_Farmer",
            "WS_Miner",
            "WS_Fabricator",
            "WS_DrugMaker",
            "WS_Smith",
            "WS_Tailor",
            "WS_Crafter",
            "WS_Artist",
            "WS_Fisher",
            "WS_Grunt",
            "WS_DarkStudier",
            "WS_Researcher",
        };

        /// A role the template may pin — and the Add menu therefore offers.
        /// Composites are excluded: they never join the assignment run, so they
        /// have no ordering to pin.
        public static bool IsPinnable(RoleView role) =>
            !role.Blocker && !role.HasRules && role.MemberRoleIds == null;

        private static bool IsCovered(RoleView role, IReadOnlyList<RoleView> catalog)
            => catalog.Any(other => other.Id != role.Id
                && !other.Blocker && !other.HasRules
                && CoverageMath.MakesRedundant(other.Coverage, other.Id, role.Coverage, role.Id));

        /// The derived default: the shipped def-name order projected onto the
        /// catalog's pinnable template roles. Catalogs with no shipped
        /// template roles at all fall back to the priority-derived order.
        public static List<int> DeriveTemplate(IReadOnlyList<RoleView> catalog)
        {
            var result = new List<int>();
            foreach (string defName in DefaultTemplateDefNames)
                foreach (RoleView role in catalog)
                    if (role.TemplateDefName == defName && IsPinnable(role))
                    {
                        result.Add(role.Id);
                        break;
                    }
            return result.Count > 0 ? result : PriorityDerivedTemplate(catalog);
        }

        /// The retired derived default: pinnable non-hunting roles no other
        /// normal role covers, by work-type priority descending. Kept as the
        /// def-name projection's fallback and to recognize stored lists that
        /// are just this old seed (normalized away at load).
        public static List<int> PriorityDerivedTemplate(IReadOnlyList<RoleView> catalog)
            => catalog
                .Where(r => IsPinnable(r) && !r.Hunting && !IsCovered(r, catalog))
                .OrderByDescending(r => r.NaturalPriority)
                .Select(r => r.Id)
                .ToList();

        /// The stored list is exactly the old priority-derived seed, meaning
        /// the player never actually reordered anything.
        public static bool MatchesPriorityDerivedTemplate(
            IReadOnlyList<int> stored, IReadOnlyList<RoleView> catalog)
            => stored != null
                && stored.SequenceEqual(PriorityDerivedTemplate(catalog));

        /// The effective template: the user's stored list (minus deleted or
        /// unpinnable roles), or the derived default when never edited. A
        /// pure override — unlisted roles float via Ordering.BasePositions.
        public static List<int> ResolveTemplate(IReadOnlyList<int> stored,
            IReadOnlyList<RoleView> catalog)
        {
            if (stored == null || stored.Count == 0) return DeriveTemplate(catalog);
            var pinnable = new HashSet<int>(catalog.Where(IsPinnable).Select(r => r.Id));
            return stored.Where(pinnable.Contains).Distinct().ToList();
        }

        /// Unpinned roles the Add menu offers: together with the template this
        /// spans every pinnable role. Newly pinned roles append at the end.
        public static List<int> AddCandidates(IReadOnlyList<RoleView> catalog,
            IReadOnlyList<int> template)
            => catalog
                .Where(r => IsPinnable(r) && !template.Contains(r.Id))
                .Select(r => r.Id)
                .ToList();
    }
}
