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

        // Skill gates (recommendations and the Fix My Colony coverage pass).
        // A minimum gate defines the "full" role of a training path; the pawn must
        // reach the level — or be best in colony, which always passes. A maximum
        // gate defines the training role: recommended only below the level, and
        // (when gateNeedsPassion) only with a passion in the skill.
        public string gateSkill;
        public int gateMinLevel;
        public int gateMaxLevel;
        public bool gateNeedsPassion;

        /// Blocker role: its jobs are never done and are vetoed in all later roles.
        public bool blocker;

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

        /// Stable fingerprint of the def's substance (label, flags, entries).
        /// Colors and gates are excluded: gates are read live from the def, and
        /// color drift shouldn't read as role drift. Computed on demand, never
        /// stored in XML; saves stamp it per seeded role so later loads can tell
        /// def drift from player edits.
        public uint StableHash()
        {
            var text = string.Join("\n",
                label, autoAssign ? "1" : "0", blocker ? "1" : "0", iconPath,
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
