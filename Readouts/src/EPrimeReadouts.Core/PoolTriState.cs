using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    public enum TriState { Off, Partial, On }

    /// Pure tri-state picker logic for pool member editing.
    /// Runs on clicks only — allocation is acceptable here.
    public static class PoolTriState
    {
        // ── Queries ───────────────────────────────────────────────────────

        /// Returns true when <paramref name="defName"/> is selected: either
        /// listed explicitly as a plain member, or covered by an @Category
        /// member whose catalog expansion includes it.
        public static bool IsSelected(List<string> members, string defName,
            IResourceCatalog catalog)
        {
            foreach (var member in members)
            {
                if (string.IsNullOrEmpty(member)) continue;
                if (member.StartsWith("@"))
                {
                    string cat = member.Substring(1);
                    var defs = catalog.CountedDefsIn(cat);
                    foreach (var d in defs)
                        if (d == defName) return true;
                }
                else if (member == defName)
                {
                    return true;
                }
            }
            return false;
        }

        /// Returns the tri-state for a category from its counted-def expansion:
        /// On when all defs covered (or the exact @ref is present), Off when
        /// none covered, Partial otherwise. Empty categories are Off.
        public static TriState CategoryState(List<string> members, string categoryDefName,
            IResourceCatalog catalog)
        {
            // Shortcut: exact @ref present → On
            string atRef = "@" + categoryDefName;
            foreach (var member in members)
                if (member == atRef) return TriState.On;

            var defs = catalog.CountedDefsIn(categoryDefName);
            if (defs.Count == 0) return TriState.Off;

            int selected = 0;
            foreach (var def in defs)
                if (IsSelected(members, def, catalog)) selected++;

            if (selected == 0) return TriState.Off;
            if (selected == defs.Count) return TriState.On;
            return TriState.Partial;
        }

        // ── Mutations ─────────────────────────────────────────────────────

        /// Toggles <paramref name="defName"/>: adds when not selected, removes
        /// when selected. If removal involves an @Category ref that covers
        /// this def, the ref is expanded into its other defs first (the ref is
        /// removed). Returns a NEW list.
        public static List<string> ToggleDef(List<string> members, string defName,
            IResourceCatalog catalog)
        {
            bool selected = IsSelected(members, defName, catalog);
            if (!selected)
            {
                // Add the def explicitly
                var result = new List<string>(members) { defName };
                return result;
            }
            else
            {
                // Remove: first expand any @ref that covers this def into its
                // other members, then remove the explicit entry if present.
                var expanded = ExpandCoveringRefs(members, defName, catalog);
                expanded.Remove(defName);
                return expanded;
            }
        }

        /// Toggles a category:
        /// Off or Partial → On: add @Cat, drop any individually listed defs
        ///   that are subsumed by the category expansion.
        /// On → Off: remove the @ref and/or all individual defs that are in
        ///   the category expansion.
        /// Returns a NEW list.
        public static List<string> ToggleCategory(List<string> members, string categoryDefName,
            IResourceCatalog catalog)
        {
            TriState current = CategoryState(members, categoryDefName, catalog);
            var catDefs = catalog.CountedDefsIn(categoryDefName);
            var catSet = new HashSet<string>(catDefs);
            string atRef = "@" + categoryDefName;

            if (current == TriState.On)
            {
                // Off: remove the @ref and all individually listed cat defs
                var result = new List<string>(members.Count);
                foreach (var member in members)
                {
                    if (member == atRef) continue;
                    if (!member.StartsWith("@") && catSet.Contains(member)) continue;
                    result.Add(member);
                }
                return result;
            }
            else
            {
                // On: add @Cat, remove individually listed defs subsumed by it,
                // and also remove the @ref itself if somehow already present
                var result = new List<string>(members.Count + 1);
                foreach (var member in members)
                {
                    if (member == atRef) continue; // will be re-added fresh
                    if (!member.StartsWith("@") && catSet.Contains(member)) continue; // subsumed
                    result.Add(member);
                }
                result.Add(atRef);
                return result;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// Returns a new list where every @Category ref that covers
        /// <paramref name="defName"/> is replaced by its other members
        /// (the ref is removed and the remaining defs are inserted).
        private static List<string> ExpandCoveringRefs(List<string> members, string defName,
            IResourceCatalog catalog)
        {
            var result = new List<string>(members.Count + 8);
            foreach (var member in members)
            {
                if (member.StartsWith("@"))
                {
                    string cat = member.Substring(1);
                    var defs = catalog.CountedDefsIn(cat);
                    bool covers = false;
                    foreach (var d in defs) if (d == defName) { covers = true; break; }
                    if (covers)
                    {
                        // Expand: add all defs except the one being removed
                        foreach (var d in defs)
                            if (d != defName && !result.Contains(d))
                                result.Add(d);
                        continue;
                    }
                }
                result.Add(member);
            }
            return result;
        }
    }
}
