using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Builds the resource-readout and broad storage category trees once and
    /// caches them. Both picker views share these trees so the expensive
    /// DefDatabase walk happens only once per game session and language.
    public static class GameResourceTree
    {
        // Cache contract:
        // Owner: process/loaded def set.
        // Key: loaded defs and the current UI language revision.
        // Value: detached resource-tree nodes consumed by both editor trees.
        // Dependencies: ThingCategoryDef/ThingDef data and UiVersion.LanguageCurrent.
        // Refresh policy: lazy, immediate on UI language revision changes.
        // Equality policy: unchanged dependencies preserve root identity.
        // Teardown: Reset releases all cached nodes on global teardown.
        private static List<ResourceTreeNode>? cachedResourceRoots;
        private static List<ResourceTreeNode>? cachedStorableRoots;
        private static int cachedLanguageVersion = -1;

        public static List<ResourceTreeNode> GetRoots(ItemPickerType type = ItemPickerType.Resources)
        {
            UiVersion.ObserveCurrentMetrics();
            if (cachedLanguageVersion != UiVersion.LanguageCurrent)
            {
                cachedResourceRoots = null;
                cachedStorableRoots = null;
                cachedLanguageVersion = UiVersion.LanguageCurrent;
            }

            if (type == ItemPickerType.AllStorableItems)
            {
                if (cachedStorableRoots == null)
                {
                    cachedStorableRoots = new List<ResourceTreeNode>();
                    ThingCategoryDef root = ThingCategoryDefOf.Root;
                    foreach (var child in root.childCategories)
                        cachedStorableRoots.Add(BuildNode(child, splitResourceRoots: false));
                    if (root.childThingDefs.Count != 0)
                        cachedStorableRoots.Add(BuildNode(root, splitResourceRoots: false,
                            includeChildren: false));
                }
                return cachedStorableRoots;
            }

            if (cachedResourceRoots == null)
            {
                cachedResourceRoots = new List<ResourceTreeNode>();
                foreach (var category in DefDatabase<ThingCategoryDef>.AllDefs)
                    if (category.resourceReadoutRoot)
                        cachedResourceRoots.Add(BuildNode(category, splitResourceRoots: true));
            }
            return cachedResourceRoots;
        }

        private static ResourceTreeNode BuildNode(ThingCategoryDef category,
            bool splitResourceRoots, bool includeChildren = true)
        {
            var node = new ResourceTreeNode { Id = category.defName, Label = category.LabelCap };
            if (includeChildren)
            {
                foreach (var child in category.childCategories)
                {
                    if (splitResourceRoots && child.resourceReadoutRoot) continue;
                    node.Children.Add(BuildNode(child, splitResourceRoots));
                }
            }
            var defs = new List<ThingDef>(category.childThingDefs);
            defs.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase));
            foreach (var def in defs)
                if (def.PlayerAcquirable)
                    node.DefNames.Add(def.defName);
            node.Poolable = GameResourceCatalog.Instance.CountedDefsIn(category.defName).Count >= 2;
            return node;
        }

        internal static void Reset()
        {
            cachedResourceRoots = null;
            cachedStorableRoots = null;
            cachedLanguageVersion = -1;
        }
    }
}
