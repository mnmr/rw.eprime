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
    }
}
