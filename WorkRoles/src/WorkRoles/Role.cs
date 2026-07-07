using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public class Role : IExposable
    {
        public const int AllHours = 0xFFFFFF;

        public int id;
        public string label;
        public bool enabled = true;
        public bool hasCustomColor;
        public Color color = Color.white;
        public string iconPath;
        /// defName of the RoleDef this role was seeded from; null for player-created roles.
        public string templateDefName;
        public bool autoAssign;
        public int activeHours = AllHours;   // bit h set = active during local hour h
        public RoleLocation location = RoleLocation.Any;
        public List<JobEntry> entries = new List<JobEntry>();

        private List<string> scribeEntries;

        public bool HasRules => activeHours != AllHours || location != RoleLocation.Any;

        /// True when this role's entries strictly include every entry of other.
        public bool Covers(Role other)
        {
            if (other == null || other == this) return false;
            if (other.entries.Count == 0 || other.entries.Count >= entries.Count) return false;
            foreach (var entry in other.entries)
                if (!entries.Contains(entry)) return false;
            return true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref hasCustomColor, "hasCustomColor");
            Scribe_Values.Look(ref color, "color", Color.white);
            Scribe_Values.Look(ref iconPath, "iconPath");
            Scribe_Values.Look(ref templateDefName, "templateDefName");
            Scribe_Values.Look(ref autoAssign, "autoAssign");
            Scribe_Values.Look(ref activeHours, "activeHours", AllHours);
            Scribe_Values.Look(ref location, "location", RoleLocation.Any);
            if (Scribe.mode == LoadSaveMode.Saving)
                scribeEntries = entries.Select(e => e.Encode()).ToList();
            Scribe_Collections.Look(ref scribeEntries, "entries", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                entries = new List<JobEntry>();
                if (scribeEntries != null)
                    foreach (var raw in scribeEntries)
                        if (JobEntry.TryDecode(raw, out var entry))
                            entries.Add(entry);
            }
            if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.PostLoadInit)
                scribeEntries = null;
        }
    }
}
