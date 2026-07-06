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
