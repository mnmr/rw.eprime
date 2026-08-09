using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles
{
    public class RoleDef : Def
    {
        /// Optional invariant persisted name when it intentionally differs from
        /// the readable form of defName. Never translate this field.
        public string seedLabel;
        /// Entries as "WorkType:DefName" or "WorkGiver:DefName" strings (tolerant of missing defs).
        public List<string> entries = new List<string>();
        public bool autoAssign;
        /// A PaletteDef defName ("slate-700"); preferred over the inline pair.
        public string colorRef;
        /// Inline one-off color (kept for mods that don't want a palette entry).
        public Color color = Color.white;
        public bool hasCustomColor;
        public string iconPath;

        /// Holder scale name (the invariant name derived from a ScaleDef)
        /// driving banded recommendation demand. Empty means Never.
        public string holderScale;

        /// Blocker role: its jobs are never done and are vetoed in all later roles.
        public bool blocker;

        /// Recommendation-only template policy. These values are read through
        /// templateDefName and are not persisted into saves.
        public bool preserveRecommendationOrder;
        public bool usesOccasionalRepeatChampionPenalty;
        public RecommendationSpecialRoleKind recommendationSpecialRole;

        /// Role-list group name (resolved to a RoleGroupDef invariant name); empty = Default.
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
                defName, autoAssign ? "1" : "0", blocker ? "1" : "0", iconPath,
                SeededDefIdentity.GroupIdentity(this), activeHours, string.Join("|", locations),
                SeededDefIdentity.ScaleIdentity(this),
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
            if (!holderScale.NullOrEmpty() && !DefDatabase<ScaleDef>.AllDefsListForReading
                    .Any(d => string.Equals(SeededDefIdentity.ScaleName(d), holderScale,
                            System.StringComparison.OrdinalIgnoreCase)
                        || string.Equals(d.label, holderScale,
                            System.StringComparison.OrdinalIgnoreCase)))
                yield return $"holderScale '{holderScale}' matches no ScaleDef label";
        }
    }
}
