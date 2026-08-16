using System.Collections.Generic;
using EPrimeReadouts.Core;
using Verse;

namespace EPrimeReadouts
{
    /// IResourceCatalog over DefDatabase, with process-owned immutable def data.
    public sealed class GameResourceCatalog : IResourceCatalog
    {
        public static readonly GameResourceCatalog Instance = new GameResourceCatalog();

        // Cache contract:
        // Owner: process/loaded def set and current presentation revision.
        // Key: ThingCategoryDef defName.
        // Value: read-only member names and translated category labels.
        // Dependencies: loaded category tree; labels additionally depend on language.
        // Refresh policy: members lazy for def lifetime; labels clear on language revision.
        // Equality policy: unchanged entries preserve collection/string identity.
        // Teardown: Reset clears both dictionaries.
        private static readonly Dictionary<string, IReadOnlyList<string>> categoryMembersCache =
            new Dictionary<string, IReadOnlyList<string>>();
        private static readonly Dictionary<string, string> categoryLabelCache =
            new Dictionary<string, string>();
        private static int categoryLabelLanguageVersion = -1;

        // Cache contract:
        // Owner: process/loaded def set.
        // Key: the loaded ThingDef database generation.
        // Value: immutable array/set of PlayerAcquirable defs omitted by ResourceCounter.
        // Dependencies: loaded defs only (not readout groups or pools).
        // Refresh policy: lazy once per loaded-def lifetime.
        // Equality policy: the built array and set retain identity until teardown.
        // Teardown: Reset clears all def-derived entries on global game teardown.
        private static ThingDef[]? extraCountedDefs;
        private static HashSet<ThingDef>? extraCountedDefSet;

        internal static int ExtraCountedDefCount
        {
            get { EnsureExtraCountedDefs(); return extraCountedDefs!.Length; }
        }

        internal static ThingDef ExtraCountedDefAt(int index)
        {
            EnsureExtraCountedDefs();
            return extraCountedDefs![index];
        }

        internal static bool IsExtraCountedDef(ThingDef def)
        {
            EnsureExtraCountedDefs();
            return extraCountedDefSet!.Contains(def);
        }

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
            IReadOnlyList<string> published = result.AsReadOnly();
            categoryMembersCache[categoryDefName] = published;
            return published;
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
            UiVersion.ObserveCurrentMetrics();
            if (categoryLabelLanguageVersion != UiVersion.LanguageCurrent)
            {
                categoryLabelCache.Clear();
                categoryLabelLanguageVersion = UiVersion.LanguageCurrent;
            }
            if (categoryLabelCache.TryGetValue(categoryDefName, out var cached))
                return cached;
            var cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(categoryDefName);
            string label = cat?.label ?? "";
            categoryLabelCache[categoryDefName] = label;
            return label;
        }

        private static void EnsureExtraCountedDefs()
        {
            if (extraCountedDefs != null) return;

            var defs = new List<ThingDef>();
            var allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                ThingDef def = allDefs[i];
                if (def.PlayerAcquirable && !def.CountAsResource)
                    defs.Add(def);
            }
            defs.Sort((left, right) =>
            {
                int hashOrder = left.shortHash.CompareTo(right.shortHash);
                return hashOrder != 0
                    ? hashOrder
                    : string.CompareOrdinal(left.defName, right.defName);
            });
            extraCountedDefs = defs.ToArray();
            extraCountedDefSet = new HashSet<ThingDef>(extraCountedDefs);
        }

        internal static void Reset()
        {
            categoryMembersCache.Clear();
            categoryLabelCache.Clear();
            categoryLabelLanguageVersion = -1;
            extraCountedDefs = null;
            extraCountedDefSet = null;
        }
    }
}
