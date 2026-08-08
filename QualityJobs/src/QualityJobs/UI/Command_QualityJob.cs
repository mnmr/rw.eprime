using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

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
    /// All commands share label "Quality Job" and the same icon, so they group
    /// and one click opens a dialog operating on all selected eligible things.
    ///
    /// GizmoOnGUI signature verified against Decompiled\Verse\Command.cs line 96:
    ///   public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    /// Gizmo height constant verified against Decompiled\Verse\Gizmo.cs line 17:
    ///   public const float Height = 75f;
    /// GetWidth(maxWidth) verified as returning 75f (Command.cs line 93).
    ///
    /// Click semantics (Command_Toggle-style: the two-state icon IS the toggle):
    ///   LEFT-click  → toggles quality management for all selected eligible things.
    ///                 Enabling applies the construction defaults; disabling clears
    ///                 the plan(s). State follows the primary thing's plan (the same
    ///                 thing the Enabled/Disabled icon reflects).
    ///   RIGHT-click → opens the fold-out Dialog_ConstructionPlanConfig panel for
    ///                 fine-tuning (matching vanilla's "right-click = more options").
    ///
    /// Right-click routing: GizmoGridDrawer (GizmoGridDrawer.cs line 433) treats
    /// a right-click on a gizmo with no RightClickFloatMenuOptions as an ordinary
    /// Interacted event, so ProcessInput receives ev.button == 1. We override
    /// ProcessInput to route on ev.button.
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
            // action is not used by our ProcessInput override, but keeping it
            // set lets base-class code paths (e.g. tooltip rendering) behave
            // normally.
            action = OpenDialog;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            // Rect construction mirrors Command.GizmoOnGUI (Command.cs:96-98);
            // Gizmo.Height = 75f (Gizmo.cs:17).
            lastGizmoRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Gizmo.Height);
            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }

        /// Routes clicks: right → open the panel (via base → action = OpenDialog);
        /// left → toggle management for all selected things with checkbox feedback.
        public override void ProcessInput(Event ev)
        {
            if (ev.button == 1)
            {
                base.ProcessInput(ev); // plays activate sound + action (OpenDialog)
                return;
            }

            // Left-click: symmetric toggle. Flip the state the icon is showing
            // (based on the primary thing's plan) for every selected thing.
            if (CurrentlyEnabled())
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                foreach (Thing t in _things)
                    Commands.RemovePlan(t.thingIDNumber);
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                ApplyDefaults(); // creates plans from the construction defaults
            }
        }

        /// Enabled = the primary (first) selected thing has a plan — matching the
        /// Enabled/Disabled icon set in ConstructionGizmos.Append.
        private bool CurrentlyEnabled()
        {
            if (_things.Count == 0) return false;
            return QualityJobsStore.Active?.FindPlanById(_things[0].thingIDNumber) != null;
        }

        private void OpenDialog()
        {
            // A gizmo cannot be clicked without having been drawn, so
            // lastGizmoRect is always populated here (Rect.zero would center).
            Find.WindowStack.Add(new Dialog_ConstructionPlanConfig(_things, lastGizmoRect));
        }

        /// Applies the user's construction default options to all eligible
        /// selected things via the existing synced Commands.ApplyPlanSettings.
        /// Eligibility matches the set the dialog operates on (_things).
        /// Reads from the per-save store when a game is loaded (dual-pattern);
        /// falls back to global Settings when the store is unavailable.
        private void ApplyDefaults()
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            int minSkill;
            bool reqInspired, reqSpecialist;
            int targetQuality;
            bool autoBest;
            if (store != null)
            {
                minSkill      = store.constructionMinSkillDefault;
                reqInspired   = store.constructionRequireInspiredDefault;
                reqSpecialist = store.constructionRequireSpecialistDefault;
                targetQuality = store.constructionTargetQualityDefault;
                autoBest      = store.constructionAutoBestDefault;
            }
            else
            {
                QualityJobsSettings s = QualityJobsMod.Settings;
                minSkill      = s.defaultConstructionMinSkill;
                reqInspired   = s.defaultConstructionRequireInspired;
                reqSpecialist = s.defaultConstructionRequireSpecialist;
                targetQuality = s.defaultConstructionTargetQuality;
                autoBest      = s.defaultConstructionAutoBest;
            }
            foreach (Thing t in _things)
            {
                Commands.ApplyPlanSettings(
                    t.thingIDNumber,
                    minSkill, reqInspired, reqSpecialist, targetQuality, autoBest);
            }
        }
    }
}
