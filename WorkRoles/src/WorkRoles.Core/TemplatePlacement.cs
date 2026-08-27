using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Placement of template entries restored into live (possibly player-edited)
    /// role entry lists.
    public static class TemplatePlacement
    {
        /// Insert index for template[templateIndex] in live: after the nearest
        /// preceding template sibling present in live, else before the nearest
        /// following one, else the end. Neighbor anchoring reproduces template
        /// order no matter which entries restore first.
        public static int AnchoredInsertIndex(
            IReadOnlyList<JobEntry> live, IReadOnlyList<JobEntry> template, int templateIndex)
        {
            for (int i = templateIndex - 1; i >= 0; i--)
            {
                int at = IndexOf(live, template[i]);
                if (at >= 0) return at + 1;
            }
            for (int i = templateIndex + 1; i < template.Count; i++)
            {
                int at = IndexOf(live, template[i]);
                if (at >= 0) return at;
            }
            return live.Count;
        }

        private static int IndexOf(IReadOnlyList<JobEntry> entries, JobEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Kind == entry.Kind && entries[i].DefName == entry.DefName)
                    return i;
            return -1;
        }
    }
}
