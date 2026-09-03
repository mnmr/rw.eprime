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
    /// The text query matches item labels and category labels alike: a
    /// matching category counts every item beneath it as a text match.
    /// </summary>
    public static class ResourceTreeFlattener
    {
        public static List<TreeRow> Flatten(List<ResourceTreeNode> roots,
            HashSet<string> expanded, string filter, IResourceCatalog catalog)
        {
            var rows = new List<TreeRow>();
            bool filtering = SearchMatcher.IsActive(filter);
            foreach (var root in roots)
                AddNode(root, 0, rows, expanded, filter, filtering, false, catalog);
            return rows;
        }

        public static List<TreeRow> Flatten(List<ResourceTreeNode> roots,
            HashSet<string> expanded, ItemTreeFilter filter, IItemPickerCatalog catalog)
        {
            var rows = new List<TreeRow>();
            bool queryActive = SearchMatcher.IsActive(filter.Query);
            var matchesByNode = new Dictionary<ResourceTreeNode, List<string>>();
            foreach (var root in roots)
                BuildMatches(root, filter, false, catalog, matchesByNode);
            foreach (var root in roots)
                AddFilteredNode(root, 0, rows, expanded, filter, queryActive, false,
                    catalog, matchesByNode);
            return rows;
        }

        private static bool CategoryMatches(ResourceTreeNode node, string query, bool ancestorMatched)
            => ancestorMatched || SearchMatcher.Matches(node.Label, query);

        private static bool HasMatch(ResourceTreeNode node, string filter, bool ancestorMatched,
            IResourceCatalog catalog)
        {
            bool matched = CategoryMatches(node, filter, ancestorMatched);
            foreach (var defName in node.DefNames)
                if (matched || SearchMatcher.Matches(catalog.LabelOf(defName), filter)) return true;
            foreach (var child in node.Children)
                if (HasMatch(child, filter, matched, catalog)) return true;
            return false;
        }

        private static void AddNode(ResourceTreeNode node, int indent, List<TreeRow> rows,
            HashSet<string> expanded, string filter, bool filtering, bool ancestorMatched,
            IResourceCatalog catalog)
        {
            if (filtering && !HasMatch(node, filter, ancestorMatched, catalog)) return;
            bool matched = filtering && CategoryMatches(node, filter, ancestorMatched);
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
                AddNode(child, indent + 1, rows, expanded, filter, filtering, matched, catalog);
            foreach (var defName in node.DefNames)
            {
                if (filtering && !matched
                    && !SearchMatcher.Matches(catalog.LabelOf(defName), filter)) continue;
                rows.Add(new TreeRow
                {
                    Indent = indent + 1,
                    DefName = defName,
                    Label = catalog.LabelCapOf(defName),
                });
            }
        }

        private static List<string> BuildMatches(ResourceTreeNode node,
            ItemTreeFilter filter, bool ancestorMatched, IItemPickerCatalog catalog,
            Dictionary<ResourceTreeNode, List<string>> matchesByNode)
        {
            bool matched = CategoryMatches(node, filter.Query, ancestorMatched);
            var result = new List<string>();
            foreach (var child in node.Children)
            {
                var childMatches = BuildMatches(child, filter, matched, catalog, matchesByNode);
                result.AddRange(childMatches);
            }
            foreach (var defName in node.DefNames)
                if (Matches(defName, filter, matched, catalog))
                    result.Add(defName);
            matchesByNode.Add(node, result);
            return result;
        }

        private static bool Matches(string defName, ItemTreeFilter filter, bool categoryMatched,
            IItemPickerCatalog catalog)
        {
            bool matchesType = filter.Type == ItemPickerType.Resources
                ? catalog.IsResource(defName)
                : catalog.IsResource(defName) || catalog.IsStorable(defName);
            if (!matchesType) return false;

            if (!string.IsNullOrEmpty(filter.SourceId)
                && !string.Equals(catalog.SourceIdOf(defName), filter.SourceId,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            return categoryMatched
                || !SearchMatcher.IsActive(filter.Query)
                || SearchMatcher.Matches(catalog.LabelOf(defName), filter.Query);
        }

        private static void AddFilteredNode(ResourceTreeNode node, int indent, List<TreeRow> rows,
            HashSet<string> expanded, ItemTreeFilter filter, bool queryActive, bool ancestorMatched,
            IItemPickerCatalog catalog,
            Dictionary<ResourceTreeNode, List<string>> matchesByNode)
        {
            var matchingDefs = matchesByNode[node];
            if (matchingDefs.Count == 0) return;

            bool matched = queryActive && CategoryMatches(node, filter.Query, ancestorMatched);
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
                AddFilteredNode(child, indent + 1, rows, expanded, filter, queryActive, matched,
                    catalog, matchesByNode);
            foreach (var defName in node.DefNames)
            {
                if (!Matches(defName, filter, matched, catalog)) continue;
                rows.Add(new TreeRow
                {
                    Indent = indent + 1,
                    DefName = defName,
                    Label = catalog.LabelCapOf(defName),
                });
            }
        }
    }
}
