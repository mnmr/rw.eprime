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
        private static List<ResourceTreeNode> cachedRoots;

        public static List<ResourceTreeNode> GetRoots()
        {
            if (cachedRoots != null) return cachedRoots;
            cachedRoots = new List<ResourceTreeNode>();
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
    }
}
