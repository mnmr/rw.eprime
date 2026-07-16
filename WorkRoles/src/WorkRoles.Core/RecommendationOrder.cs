using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
{
    /// The recommendation order: the stored template (the Options tab's chips)
    /// pins positions for its members; every other role gets a calculated
    /// position — trainers after their furthest-down target, covered roles
    /// after their tightest coverer, hunting roles in the legacy slot after
    /// the duty roles, anything else by work-type priority — so the final
    /// ordering always contains all roles.
    public static class RecommendationOrder
    {
        private const long Slot = 1000;

        /// A role the template may pin — and the Add menu therefore offers.
        /// Autos included: they hold real positions in the order (Core above
        /// Doctor, Basics below), so the panel must show and move them too.
        public static bool IsPinnable(RecRole role)
            => !role.Blocker && !role.HasRules && !role.Managed;

        /// Whether any other normal role's coverage makes this one redundant
        /// (autos and trainers count: Core knocks out Rescuer, Smith knocks
        /// out Fabricator).
        private static bool IsCovered(RecRole role, IReadOnlyList<RecRole> catalog)
            => catalog.Any(other => other.Id != role.Id
                && !other.Blocker && !other.HasRules && !other.Managed
                && CoverageMath.MakesRedundant(other.Coverage, other.Id, role.Coverage, role.Id));

        /// The derived default template: the vanilla grid's columns — pinnable
        /// non-hunting roles no other normal role covers — by work-type
        /// priority, descending.
        public static List<int> DeriveTemplate(IReadOnlyList<RecRole> catalog)
            => catalog
                .Where(r => IsPinnable(r) && !r.Hunting && !IsCovered(r, catalog))
                .OrderByDescending(r => r.NaturalPriority)
                .Select(r => r.Id)
                .ToList();

        /// The effective template: the user's stored list (minus deleted or
        /// unpinnable roles), or the derived default when never edited. Stored
        /// is a pure override — unlisted roles are not merged in; they float
        /// via PositionOf.
        public static List<int> ResolveTemplate(IReadOnlyList<int> stored,
            IReadOnlyList<RecRole> catalog)
        {
            if (stored == null || stored.Count == 0) return DeriveTemplate(catalog);
            var pinnable = new HashSet<int>(catalog.Where(IsPinnable).Select(r => r.Id));
            return stored.Where(pinnable.Contains).Distinct().ToList();
        }

        /// Unpinned roles the Add menu offers. Together with the template this
        /// spans every pinnable role: nothing a player creates can be neither
        /// listed nor addable.
        public static List<int> AddCandidates(IReadOnlyList<RecRole> catalog,
            IReadOnlyList<int> template)
            => catalog
                .Where(r => IsPinnable(r) && !template.Contains(r.Id))
                .Select(r => r.Id)
                .ToList();

        /// Every role's sort position in one map — the shared ordering the
        /// recommendation list and the target plan both consume.
        private static Dictionary<int, int> TemplateIndexOf(IReadOnlyList<int> template)
        {
            var index = new Dictionary<int, int>();
            for (int i = 0; i < template.Count; i++)
                index[template[i]] = i;
            return index;
        }

        public static Dictionary<int, long> PositionsFor(
            IReadOnlyList<RecRole> catalog, IReadOnlyList<int> template)
        {
            var templateIndex = TemplateIndexOf(template);
            var byId = catalog.ToDictionary(r => r.Id);
            return catalog.ToDictionary(r => r.Id, r => PositionOf(r, templateIndex, byId));
        }

        /// Where an unlisted role belongs when pinned into the template:
        /// right after the anchor its calculated position hangs off.
        public static int InsertIndex(RecRole role, IReadOnlyList<int> template,
            IReadOnlyDictionary<int, RecRole> byId)
        {
            long position = PositionOf(role, TemplateIndexOf(template), byId);
            for (int i = 0; i < template.Count; i++)
                if (position < i * Slot)
                    return i;
            return template.Count;
        }

        /// Sort position for a role: its template slot when pinned, else
        /// calculated. Chains resolve transitively (each hop one step past its
        /// anchor); cycles and 8+ deep chains fall back to work-type priority.
        public static long PositionOf(RecRole role, IReadOnlyDictionary<int, int> templateIndex,
            IReadOnlyDictionary<int, RecRole> byId)
        {
            long? Anchored(RecRole current, HashSet<int> visited, int depth)
            {
                if (templateIndex.TryGetValue(current.Id, out int index))
                    return index * Slot;
                if (depth >= 8 || !visited.Add(current.Id)) return null;
                if (current.Hunting) return DutySlot(templateIndex, byId);

                // Targets must always precede their trainer: slot after the
                // furthest-down target (Fabricator before Smith AND Crafter).
                if (current.TrainTargets.Count > 0)
                {
                    long? latest = null;
                    foreach (int targetId in current.TrainTargets)
                        if (byId.TryGetValue(targetId, out var target))
                        {
                            long? position = Anchored(target, visited, depth + 1);
                            if (position.HasValue && (!latest.HasValue || position > latest))
                                latest = position;
                        }
                    if (latest.HasValue) return latest + 1;
                }

                var coverer = TightestCoverer(current, byId);
                if (coverer != null)
                {
                    long? position = Anchored(coverer, visited, depth + 1);
                    // A covered role that is its coverer's train TARGET must
                    // still precede it (Fabricator before Smith).
                    if (position.HasValue)
                        return coverer.TrainTargets.Contains(current.Id)
                            ? position - 1 : position + 1;
                }

                return NaturalSlot(current, templateIndex, byId);
            }
            return Anchored(role, new HashSet<int>(), 0)
                ?? NaturalSlot(role, templateIndex, byId);
        }

        /// The legacy Hunter slot: right after the duty roles (hunting trains
        /// Shooting, so it outranks skilled work but never wardening/childcare).
        private static long DutySlot(IReadOnlyDictionary<int, int> templateIndex,
            IReadOnlyDictionary<int, RecRole> byId)
        {
            int lastDuty = -1;
            foreach (var entry in templateIndex)
                if (entry.Value > lastDuty && byId.TryGetValue(entry.Key, out var anchor)
                    && anchor.WorkTypes.Any(wt => wt == "Warden" || wt == "Childcare"))
                    lastDuty = entry.Value;
            return lastDuty < 0 ? -10 : lastDuty * Slot + 990;
        }

        /// The smallest role whose coverage makes this one redundant (autos
        /// count: Core anchors Rescuer just like Farmer anchors Grower).
        private static RecRole TightestCoverer(RecRole role, IReadOnlyDictionary<int, RecRole> byId)
        {
            RecRole tightest = null;
            foreach (var candidate in byId.Values)
            {
                if (candidate.Id == role.Id) continue;
                if (candidate.Blocker || candidate.HasRules || candidate.Managed) continue;
                if (!CoverageMath.MakesRedundant(candidate.Coverage, candidate.Id, role.Coverage, role.Id)) continue;
                if (tightest == null
                    || candidate.Coverage.Count < tightest.Coverage.Count
                    || (candidate.Coverage.Count == tightest.Coverage.Count && candidate.Id < tightest.Id))
                    tightest = candidate;
            }
            return tightest;
        }

        /// Fallback for roles with no anchor: after the last template entry of
        /// equal-or-higher work-type priority (before the template when none).
        private static long NaturalSlot(RecRole role, IReadOnlyDictionary<int, int> templateIndex,
            IReadOnlyDictionary<int, RecRole> byId)
        {
            int lastHigher = -1;
            foreach (var entry in templateIndex)
                if (entry.Value > lastHigher && byId.TryGetValue(entry.Key, out var anchor)
                    && anchor.NaturalPriority >= role.NaturalPriority)
                    lastHigher = entry.Value;
            return lastHigher < 0 ? -100 : lastHigher * Slot + 900;
        }
    }
}
