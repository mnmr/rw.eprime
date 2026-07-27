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
            if (cat != null)
                foreach (var def in cat.DescendantThingDefs)
                    if (def.CountAsResource) result.Add(def.defName);
            categoryMembersCache[categoryDefName] = result;
            return result;
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
