using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// Single gizmo for Quality Jobs construction management. Subclasses
    /// Command_Action so it inherits the standard gizmo rendering. Overrides
    /// GizmoOnGUI to capture the drawn button rect, then opens
    /// Dialog_ConstructionPlanConfig anchored to the gizmo's bottom edge.
    ///
    /// Accepts a list of Things for multi-select support (Fix 5). When multiple
    /// quality buildables are selected, GroupsWith merges gizmos with matching
    /// label+icon (verified Command.GroupsWith at Decompiled\Verse\Command.cs line 275:
    ///   hotKey == command.hotKey && Label == command.Label && icon == command.icon
    ///   && groupKey == command.groupKey).
    /// All commands share label "Quality job" and the same icon, so they group
    /// and one click opens a dialog operating on all selected eligible things.
    ///
    /// GizmoOnGUI signature verified against Decompiled\Verse\Command.cs line 96:
    ///   public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    /// Gizmo height constant verified against Decompiled\Verse\Gizmo.cs line 17:
    ///   public const float Height = 75f;
    /// GetWidth(maxWidth) verified as returning 75f (Command.cs line 93).
    public class Command_QualityJob : Command_Action
    {
        private readonly List<Thing> _things;

        // Captured on every draw pass. ProcessInput runs AFTER the gizmo grid
        // finishes drawing (GizmoGridDrawer), so a temporary action swap inside
        // GizmoOnGUI would be restored before the click ever fires; persisting
        // the rect and reading it from the permanent action is the only
        // ordering that works.
        private Rect lastGizmoRect;

        public Command_QualityJob(List<Thing> things)
        {
            _things = things;
            action = OpenDialog;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            // Rect construction mirrors Command.GizmoOnGUI (Command.cs:96-98);
            // Gizmo.Height = 75f (Gizmo.cs:17).
            lastGizmoRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Gizmo.Height);
            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }

        private void OpenDialog()
        {
            // A gizmo cannot be clicked without having been drawn, so
            // lastGizmoRect is always populated here (Rect.zero would center).
            Find.WindowStack.Add(new Dialog_ConstructionPlanConfig(_things, lastGizmoRect));
        }
    }
}
