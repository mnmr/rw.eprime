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
    }
}
