using System.Collections.Generic;
using EPrimeReadouts.Core;
using Verse;

namespace EPrimeReadouts
{
    /// IResourceCatalog over DefDatabase. Stateless; safe to share.
    public sealed class GameResourceCatalog : IResourceCatalog
    {
        public static readonly GameResourceCatalog Instance = new GameResourceCatalog();

        private static readonly Dictionary<string, List<string>> categoryMembersCache =
            new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, string> categoryLabelCache =
            new Dictionary<string, string>();

        public bool Exists(string defName) =>
            DefDatabase<ThingDef>.GetNamedSilentFail(defName) != null;

        public string LabelOf(string defName) =>
            DefDatabase<ThingDef>.GetNamedSilentFail(defName)?.label ?? "";

        public IReadOnlyList<string> CountedDefsIn(string categoryDefName)
        {
            if (categoryMembersCache.TryGetValue(categoryDefName, out var cached))
                return cached;
            var cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(categoryDefName);
            var result = new List<string>();
            if (cat != null) CollectCounted(cat, result);
            categoryMembersCache[categoryDefName] = result;
            return result;
        }

        /// Category membership follows the DISPLAYED tree twice over: the def
        /// filter is PlayerAcquirable (what the picker trees list — includes
        /// stone chunks, which are acquirable but not counted, so a
        /// folder-only pool selection still yields members and an icon), and
        /// recursion stops at child categories that are readout roots
        /// themselves (e.g. Drugs nests under Manufactured in def data but
        /// displays at top level, so Manufactured must not claim the drug
        /// defs). Raw DescendantThingDefs would break both rules.
        private static void CollectCounted(ThingCategoryDef cat, List<string> into)
        {
            foreach (var def in cat.childThingDefs)
                if (def.PlayerAcquirable && !into.Contains(def.defName))
                    into.Add(def.defName);
            foreach (var child in cat.childCategories)
                if (!child.resourceReadoutRoot)
                    CollectCounted(child, into);
        }

        public string CategoryLabelOf(string categoryDefName)
        {
            if (categoryLabelCache.TryGetValue(categoryDefName, out var cached))
                return cached;
            var cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(categoryDefName);
            string label = cat?.label ?? "";
            categoryLabelCache[categoryDefName] = label;
            return label;
        }
    }
}
