using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WorkRoles
{
    /// Named progression container: paths are skill-agnostic; stored roleIds order
    /// is the role assignment order. Geometry per SkillProgressionMath.
    /// Mutate via RoleCommands.
    public class TrainingPath : IExposable
    {
        public int id;
        public string name;
        public List<int> roleIds = new List<int>();
        public List<int> bandMins = new List<int>();
        public List<int> bandMaxes = new List<int>();
        /// Assignment anchor: unlocked members slot into a pawn's assignment
        /// list in path order, before or after this role. -1 = none.
        public int anchorRoleId = -1;
        public bool anchorBefore = true;
        /// Display color override; without one the highest-band role colors the path.
        public bool hasCustomColor;
        public Color color = Color.white;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Collections.Look(ref roleIds, "roleIds", LookMode.Value);
            Scribe_Collections.Look(ref bandMins, "bandMins", LookMode.Value);
            Scribe_Collections.Look(ref bandMaxes, "bandMaxes", LookMode.Value);
            Scribe_Values.Look(ref anchorRoleId, "anchorRoleId", -1);
            Scribe_Values.Look(ref anchorBefore, "anchorBefore", true);
            Scribe_Values.Look(ref hasCustomColor, "hasCustomColor");
            Scribe_Values.Look(ref color, "color", Color.white);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                name ??= "";
                roleIds ??= new List<int>();
                bandMins ??= new List<int>();
                bandMaxes ??= new List<int>();
            }
        }
    }
}
