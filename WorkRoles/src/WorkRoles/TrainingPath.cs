using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace WorkRoles
{
    /// LEGACY load-only type: roles own their training now (Role.training*).
    /// RoleStore reads old saves' stand-alone paths through this shape and
    /// folds them into their target role at load; nothing writes it.
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

        /// Full value equality except id: name, members, bands, anchor and
        /// color. Import skip and the load-time duplicate sweep both use this.
        public bool DuplicateOf(TrainingPath other)
        {
            if (other == null || other == this) return false;
            return string.Equals(name, other.name, System.StringComparison.Ordinal)
                && roleIds.SequenceEqual(other.roleIds)
                && bandMins.SequenceEqual(other.bandMins)
                && bandMaxes.SequenceEqual(other.bandMaxes)
                && anchorRoleId == other.anchorRoleId
                && anchorBefore == other.anchorBefore
                && hasCustomColor == other.hasCustomColor
                && (!hasCustomColor || color.IndistinguishableFrom(other.color));
        }

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
