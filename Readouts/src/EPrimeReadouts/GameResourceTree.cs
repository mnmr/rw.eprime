using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Builds the vanilla resource category tree (ThingCategoryDef roots) once
    /// and caches it. Both ResourceTreeView and PoolEditorView share this tree
    /// so the expensive DefDatabase walk happens only once per game session.
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
        private static List<ResourceTreeNode> cachedRoots;
        private static int cachedLanguageVersion = -1;

        public static List<ResourceTreeNode> GetRoots()
        {
            UiVersion.ObserveCurrentMetrics();
            if (cachedRoots != null
                && cachedLanguageVersion == UiVersion.LanguageCurrent)
                return cachedRoots;
            cachedRoots = new List<ResourceTreeNode>();
            cachedLanguageVersion = UiVersion.LanguageCurrent;
            foreach (var category in DefDatabase<ThingCategoryDef>.AllDefs)
                if (category.resourceReadoutRoot)
                    cachedRoots.Add(BuildNode(category));
            return cachedRoots;
        }

        public static ResourceTreeNode BuildNode(ThingCategoryDef category)
        {
            var node = new ResourceTreeNode { Id = category.defName, Label = category.LabelCap };
            foreach (var child in category.childCategories)
            {
                if (child.resourceReadoutRoot) continue;
                node.Children.Add(BuildNode(child));
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
            cachedRoots = null;
            cachedLanguageVersion = -1;
        }
    }
}
