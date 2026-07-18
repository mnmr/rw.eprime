using System.Collections.Generic;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public class RoleDef : Def
    {
        /// Entries as "WorkType:DefName" or "WorkGiver:DefName" strings (tolerant of missing defs).
        public List<string> entries = new List<string>();
        public bool autoAssign;
        /// A PaletteDef defName ("slate-700"); preferred over the inline pair.
        public string colorRef;
        /// Inline one-off color (kept for mods that don't want a palette entry).
        public Color color = Color.white;
        public bool hasCustomColor;
        public string iconPath;

        /// Auto-mode default consumed by the existing recommendation scaling.
        public int minHolders = -1;

        /// Blocker role: its jobs are never done and are vetoed in all later roles.
        public bool blocker;

        /// Role-list group label (a RoleGroupDef label); empty = Default.
        public string group;
        /// Time rule: 24-char bitstring, hour 0 leftmost, '1' = active. Null = always.
        public string activeHours;
        /// Location rule: any of Settlements, Caravans. Empty = anywhere.
        public List<string> locations = new List<string>();

        public List<JobEntry> ParsedEntries()
        {
            var parsed = new List<JobEntry>();
            foreach (var raw in entries)
            {
                if (JobEntry.TryDecode(raw, out var entry)) parsed.Add(entry);
                else Log.Warning($"[WorkRoles] RoleDef {defName}: unparseable entry '{raw}'");
            }
            return parsed;
        }

        /// Stable fingerprint of the def's substance — everything copied onto
        /// roles at creation (colors excluded: color drift shouldn't read as
        /// role drift). Computed on demand, never stored in XML; saves stamp it
        /// per seeded role so later loads can tell def drift from player edits.
        public uint StableHash()
        {
            // Hash-input change is safe: templateHash has no readers yet.
            var text = string.Join("\n",
                label, autoAssign ? "1" : "0", blocker ? "1" : "0", iconPath,
                group, activeHours, string.Join("|", locations),
                minHolders.ToString(),
                string.Join("|", entries));
            return Seeding.Fnv1a(text);
        }

        /// The def's color: colorRef resolves through PaletteDef, else the
        /// inline color/hasCustomColor pair.
        public (bool has, Color color) ResolvedColor()
        {
            if (!colorRef.NullOrEmpty())
            {
                var palette = DefDatabase<PaletteDef>.GetNamedSilentFail(colorRef);
                if (palette != null) return (true, palette.color);
            }
            return (hasCustomColor, color);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var error in base.ConfigErrors())
                yield return error;
            if (!colorRef.NullOrEmpty()
                && DefDatabase<PaletteDef>.GetNamedSilentFail(colorRef) == null)
                yield return $"unknown colorRef '{colorRef}'";
        }
    }
}
