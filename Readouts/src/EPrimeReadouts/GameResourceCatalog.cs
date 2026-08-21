using System.Collections.Generic;
using EPrimeReadouts.Core;
using EPrimeReadouts.UI;
using Verse;

namespace EPrimeReadouts
{
    /// IResourceCatalog over DefDatabase, with process-owned immutable def data.
    public sealed class GameResourceCatalog : IItemPickerCatalog
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

        // Cache contract:
        // Owner: process/loaded def set and current presentation revision.
        // Key: loaded ThingDefs and UiVersion.LanguageCurrent.
        // Value: immutable All/Vanilla/user-mod picker options.
        // Dependencies: storable/acquirable defs, owning ModContentPacks, and
        // localized fixed labels.
        // Refresh policy: lazy, immediate on language revision changes.
        // Equality policy: unchanged dependencies preserve list identity.
        // Teardown: Reset releases the catalog with other def-derived caches.
        private static IReadOnlyList<ItemSourceOption>? sourceChoices;
        private static int sourceChoicesLanguageVersion = -1;

        private readonly struct PickerMetadata
        {
            internal readonly bool Resource;
            internal readonly bool Storable;
            internal readonly string SourceId;

            internal PickerMetadata(bool resource, bool storable, string sourceId)
            {
                Resource = resource;
                Storable = storable;
                SourceId = sourceId;
            }
        }

        // Cache contract:
        // Owner: process/loaded def set.
        // Key: ThingDef defName in the loaded database generation.
        // Value: immutable picker eligibility and stable source attribution.
        // Dependencies: PlayerAcquirable, CountAsResource, EverStorable and
        // owning ModContentPack identity.
        // Refresh policy: lazy once per loaded-def lifetime.
        // Equality policy: metadata dictionary retains identity until teardown.
        // Teardown: Reset releases all entries on global game teardown.
        private static Dictionary<string, PickerMetadata>? pickerMetadata;

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

        public string CanonicalDefNameOf(string defName) =>
            DefDatabase<ThingDef>.GetNamedSilentFail(defName)?.defName ?? "";

        public string LabelOf(string defName) =>
            DefDatabase<ThingDef>.GetNamedSilentFail(defName)?.label ?? "";

        public string LabelCapOf(string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def != null ? (string)def.LabelCap : "";
        }

        public bool IsResource(string defName)
        {
            EnsurePickerMetadata();
            return pickerMetadata!.TryGetValue(defName, out PickerMetadata metadata)
                && metadata.Resource;
        }

        public bool IsStorable(string defName)
        {
            EnsurePickerMetadata();
            return pickerMetadata!.TryGetValue(defName, out PickerMetadata metadata)
                && metadata.Storable;
        }

        public string SourceIdOf(string defName)
        {
            EnsurePickerMetadata();
            return pickerMetadata!.TryGetValue(defName, out PickerMetadata metadata)
                ? metadata.SourceId
                : ItemSourceIds.Vanilla;
        }

        internal IReadOnlyList<ItemSourceOption> SourceChoices()
        {
            UiVersion.ObserveCurrentMetrics();
            if (sourceChoices != null
                && sourceChoicesLanguageVersion == UiVersion.LanguageCurrent)
                return sourceChoices;

            var contributing = new List<ItemSourceOption>();
            var seenSources = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var defs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                ThingDef def = defs[i];
                if (!def.PlayerAcquirable
                    || (!def.CountAsResource && !def.EverStorable(true)))
                    continue;
                ModContentPack? pack = def.modContentPack;
                string sourceId = SourceIdOf(def);
                if (sourceId != ItemSourceIds.Vanilla && pack != null
                    && seenSources.Add(sourceId))
                    contributing.Add(new ItemSourceOption(sourceId, pack.Name));
            }
            sourceChoices = ItemSourceChoices.Build(
                contributing, UiText.Get("EPR.All"), UiText.Get("EPR.Vanilla")).AsReadOnly();
            sourceChoicesLanguageVersion = UiVersion.LanguageCurrent;
            return sourceChoices;
        }

        private static string SourceIdOf(ThingDef? def)
        {
            ModContentPack? pack = def?.modContentPack;
            if (pack == null || pack.IsCoreMod || pack.IsOfficialMod)
                return ItemSourceIds.Vanilla;
            return pack.PackageId;
        }

        private static void EnsurePickerMetadata()
        {
            if (pickerMetadata != null) return;
            var built = new Dictionary<string, PickerMetadata>();
            var defs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                ThingDef def = defs[i];
                bool resource = def.PlayerAcquirable && def.CountAsResource;
                bool storable = def.PlayerAcquirable && def.EverStorable(true);
                built[def.defName] = new PickerMetadata(
                    resource, storable, SourceIdOf(def));
            }
            pickerMetadata = built;
        }

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

        /// Category membership preserves the broad PlayerAcquirable expansion
        /// used by saved @Category pool refs (including uncounted stone chunks,
        /// so an existing folder-only pool still yields members and an icon).
        /// The Resources picker applies its stricter CountAsResource filter
        /// separately. Recursion stops at child categories that are readout roots
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
            sourceChoices = null;
            sourceChoicesLanguageVersion = -1;
            pickerMetadata = null;
        }
    }
}
