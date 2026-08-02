using System.Collections.Generic;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public class RoleAssignment : IExposable
    {
        public int roleId;
        public AssignmentState state = AssignmentState.Enabled;
        /// Pinned assignments are never added, removed or moved by Fix My Colony /
        /// Make It So — the player's placement wins.
        public bool pinned;

        public void ExposeData()
        {
            Scribe_Values.Look(ref roleId, "roleId");
            Scribe_Values.Look(ref state, "state", AssignmentState.Enabled);
            // Saves that predate the tri-state carry an "enabled" bool instead.
            if (Scribe.mode == LoadSaveMode.LoadingVars
                && state == AssignmentState.Enabled)
            {
                bool enabled = true;
                Scribe_Values.Look(ref enabled, "enabled", true);
                if (!enabled) state = AssignmentState.Disabled;
            }
            Scribe_Values.Look(ref pinned, "pinned");
        }
    }

    public class PawnRoleSet : IExposable
    {
        public List<RoleAssignment> assignments = new List<RoleAssignment>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref assignments, "assignments", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && assignments == null)
                assignments = new List<RoleAssignment>();
        }
    }
}
