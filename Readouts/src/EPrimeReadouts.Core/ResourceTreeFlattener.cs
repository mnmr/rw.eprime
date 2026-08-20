using System;
using System.Collections.Generic;
using RimShared.Common;

namespace EPrimeReadouts.Core
{
    public sealed class ResourceTreeNode
    {
        public string Id = "";
        public string Label = "";
        public bool Poolable;
        public List<ResourceTreeNode> Children = new List<ResourceTreeNode>();
        public List<string> DefNames = new List<string>();
    }

    public struct TreeRow
    {
        public int Indent;
        public bool IsCategory;
        public string Id;      // categories
        public string Label;
        public string DefName; // resources
        public bool Expanded;  // categories
        public bool Poolable;  // categories: copied from ResourceTreeNode.Poolable
        public IReadOnlyList<string> MatchingDefNames; // categories: active-filter scope, descendants included
    }

    /// <summary>
    /// Depth-first flatten. Child categories list before the category's own
    /// resources. An active filter force-expands every branch containing a
    /// match, hides non-matching resources, and drops matchless branches.
    /// </summary>
    public static class ResourceTreeFlattener
    {
        public static List<TreeRow> Flatten(List<ResourceTreeNode> roots,
            HashSet<string> expanded, string filter, IResourceCatalog catalog)
        {
            var rows = new List<TreeRow>();
            bool filtering = SearchMatcher.IsActive(filter);
            foreach (var root in roots)
                AddNode(root, 0, rows, expanded, filter, filtering, catalog);
            return rows;
        }

        public static List<TreeRow> Flatten(List<ResourceTreeNode> roots,
            HashSet<string> expanded, ItemTreeFilter filter, IItemPickerCatalog catalog)
        {
            var rows = new List<TreeRow>();
            bool queryActive = SearchMatcher.IsActive(filter.Query);
            var matchesByNode = new Dictionary<ResourceTreeNode, List<string>>();
            foreach (var root in roots)
                BuildMatches(root, filter, catalog, matchesByNode);
            foreach (var root in roots)
                AddFilteredNode(root, 0, rows, expanded, filter, queryActive,
                    catalog, matchesByNode);
            return rows;
        }

        private static bool HasMatch(ResourceTreeNode node, string filter, IResourceCatalog catalog)
        {
            foreach (var defName in node.DefNames)
                if (SearchMatcher.Matches(catalog.LabelOf(defName), filter)) return true;
            foreach (var child in node.Children)
                if (HasMatch(child, filter, catalog)) return true;
            return false;
        }

        private static void AddNode(ResourceTreeNode node, int indent, List<TreeRow> rows,
            HashSet<string> expanded, string filter, bool filtering, IResourceCatalog catalog)
        {
            if (filtering && !HasMatch(node, filter, catalog)) return;
            bool open = filtering || expanded.Contains(node.Id);
            rows.Add(new TreeRow
            {
                Indent = indent,
                IsCategory = true,
                Id = node.Id,
                Label = node.Label,
                Expanded = open,
                Poolable = node.Poolable,
                MatchingDefNames = catalog.CountedDefsIn(node.Id),
            });
            if (!open) return;
            foreach (var child in node.Children)
                AddNode(child, indent + 1, rows, expanded, filter, filtering, catalog);
            foreach (var defName in node.DefNames)
            {
                if (filtering && !SearchMatcher.Matches(catalog.LabelOf(defName), filter)) continue;
                rows.Add(new TreeRow
                {
                    Indent = indent + 1,
                    DefName = defName,
                    Label = catalog.LabelOf(defName),
                });
            }
        }

        private static List<string> BuildMatches(ResourceTreeNode node,
            ItemTreeFilter filter, IItemPickerCatalog catalog,
            Dictionary<ResourceTreeNode, List<string>> matchesByNode)
        {
            var result = new List<string>();
            foreach (var child in node.Children)
            {
                var childMatches = BuildMatches(child, filter, catalog, matchesByNode);
                result.AddRange(childMatches);
            }
            foreach (var defName in node.DefNames)
                if (Matches(defName, filter, catalog))
                    result.Add(defName);
            matchesByNode.Add(node, result);
            return result;
        }

        private static bool Matches(string defName, ItemTreeFilter filter, IItemPickerCatalog catalog)
        {
            bool matchesType = filter.Type == ItemPickerType.Resources
                ? catalog.IsResource(defName)
                : catalog.IsResource(defName) || catalog.IsStorable(defName);
            if (!matchesType) return false;

            if (!string.IsNullOrEmpty(filter.SourceId)
                && !string.Equals(catalog.SourceIdOf(defName), filter.SourceId,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            return !SearchMatcher.IsActive(filter.Query)
                || SearchMatcher.Matches(catalog.LabelOf(defName), filter.Query);
        }

        private static void AddFilteredNode(ResourceTreeNode node, int indent, List<TreeRow> rows,
            HashSet<string> expanded, ItemTreeFilter filter, bool queryActive,
            IItemPickerCatalog catalog,
            Dictionary<ResourceTreeNode, List<string>> matchesByNode)
        {
            var matchingDefs = matchesByNode[node];
            if (matchingDefs.Count == 0) return;

            bool open = queryActive || expanded.Contains(node.Id);
            rows.Add(new TreeRow
            {
                Indent = indent,
                IsCategory = true,
                Id = node.Id,
                Label = node.Label,
                Expanded = open,
                Poolable = node.Poolable,
                MatchingDefNames = matchingDefs,
            });
            if (!open) return;

            foreach (var child in node.Children)
                AddFilteredNode(child, indent + 1, rows, expanded, filter, queryActive,
                    catalog, matchesByNode);
            foreach (var defName in node.DefNames)
            {
                if (!Matches(defName, filter, catalog)) continue;
                rows.Add(new TreeRow
                {
                    Indent = indent + 1,
                    DefName = defName,
                    Label = catalog.LabelOf(defName),
                });
            }
        }
    }
}
