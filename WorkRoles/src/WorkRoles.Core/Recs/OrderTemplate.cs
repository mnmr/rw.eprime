using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// The recommendation-order template over RoleViews: the stored list pins
    /// positions for its members; everything else floats via
    /// Ordering.BasePositions. The DERIVED default (vanilla-grid columns)
    /// seeds/backs the template, so an unedited template is never empty and
    /// Restore Defaults can return to it.
    public static class OrderTemplate
    {
        /// A role the template may pin — and the Add menu therefore offers.
        public static bool IsPinnable(RoleView role) => !role.Blocker && !role.HasRules;

        private static bool IsCovered(RoleView role, IReadOnlyList<RoleView> catalog)
            => catalog.Any(other => other.Id != role.Id
                && !other.Blocker && !other.HasRules
                && CoverageMath.MakesRedundant(other.Coverage, other.Id, role.Coverage, role.Id));

        /// The derived default: pinnable non-hunting roles no other normal
        /// role covers, by work-type priority descending.
        public static List<int> DeriveTemplate(IReadOnlyList<RoleView> catalog)
            => catalog
                .Where(r => IsPinnable(r) && !r.Hunting && !IsCovered(r, catalog))
                .OrderByDescending(r => r.NaturalPriority)
                .Select(r => r.Id)
                .ToList();

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
        /// spans every pinnable role.
        public static List<int> AddCandidates(IReadOnlyList<RoleView> catalog,
            IReadOnlyList<int> template)
            => catalog
                .Where(r => IsPinnable(r) && !template.Contains(r.Id))
                .Select(r => r.Id)
                .ToList();

        /// Where an unlisted role belongs when pinned: right after the slot
        /// its floating position hangs off.
        public static int InsertIndex(RoleView role, IReadOnlyList<int> template,
            IReadOnlyList<RoleView> catalog)
        {
            long position = Ordering.BasePositions(catalog, template)[role.Id];
            for (int i = 0; i < template.Count; i++)
                if (position < i * Ordering.Slot)
                    return i;
            return template.Count;
        }
    }
}
