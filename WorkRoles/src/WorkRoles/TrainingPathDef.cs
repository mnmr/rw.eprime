using System.Collections.Generic;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// A seeded training path (label = path name), created once per save after
    /// roles land. Role references are RoleDef defNames; entries whose role is
    /// absent (DLC/mod gated) are skipped at seed time — no ConfigErrors for them.
    public class TrainingPathDef : Def
    {
        public class Entry
        {
            /// RoleDef defName; entry order is the assignment order.
            public string role;
            /// [min, max) skill band on the 0..21 axis (21 = open top).
            public int min;
            public int max = SkillProgressionMath.MaxLevel;
        }

        /// Seed order, lowest first (like RoleGroupDef).
        public int order;
        /// Assignment anchor as a RoleDef defName; null = no anchor.
        public string anchorRole;
        public bool anchorBefore = true;
        /// Optional chip color: a PaletteDef defName.
        public string colorRef;
        public List<Entry> entries = new List<Entry>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var error in base.ConfigErrors())
                yield return error;
            if (!colorRef.NullOrEmpty()
                && DefDatabase<PaletteDef>.GetNamedSilentFail(colorRef) == null)
                yield return $"unknown colorRef '{colorRef}'";
            foreach (var entry in entries)
                if (entry.role.NullOrEmpty())
                    yield return "entry without a role";
        }
    }
}
