using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Fix 4: When the player invokes the vanilla build-copy command on a managed
    /// thing, placed copies receive the same plan settings.
    ///
    /// Vanilla research (BuildCopyCommandUtility.cs):
    ///   BuildCopyCommand returns a Command_Action (line 35).
    ///   hotKey = KeyBindingDefOf.Misc11 (line 65; only when allowHotKey=true,
    ///     which BuildCopyCommand passes as true).
    ///   The action calls Find.DesignatorManager.Select(des) where des is a
    ///   Designator_Build (line 39).
    ///
    /// Designator_Build.DesignateSingleCell (line 455, Decompiled\RimWorld\Designator_Build.cs):
    ///   When godMode is off and work-to-build > 0, calls
    ///   GenConstruct.PlaceBlueprintForBuild which returns a Blueprint_Build (line 522).
    ///   The blueprint is then findable via map.thingGrid.ThingAt<Blueprint_Build>(c)
    ///   (ThingGrid.ThingAt<T> verified at Decompiled\Verse\ThingGrid.cs line 170).
    ///
    /// PlacingDef: Designator_Build.PlacingDef => entDef (line 32).
    ///
    /// Implementation:
    ///   (A) In gizmo-append helpers (Blueprint, Frame, Building), when the source
    ///       thing has a plan, we scan the yielded gizmo sequence for the vanilla
    ///       copy command (identified by its hotKey == KeyBindingDefOf.Misc11 AND
    ///       type == Command_Action, which is stable and locale-independent). We wrap
    ///       its action to arm a static pending record after the original action
    ///       selects the designator.
    ///
    ///   (B) Postfix on Designator_Build.DesignateSingleCell: if the pending record
    ///       is armed and matches the current designator instance (reference identity),
    ///       locate the just-placed Blueprint_Build at cell c via thingGrid and call
    ///       the synced ApplyPlanSettings command for it.
    ///
    /// Lifecycle:
    ///   - Pending is armed when the copy gizmo is clicked (client-local UI state).
    ///   - Pending remains armed for multi-place: each cell placement applies settings.
    ///   - Pending clears itself when designator reference no longer matches (the
    ///     player switches to a different designator).
    ///   - Save/load: pending is a static field — it does not persist across saves.
    ///     After a load, if the player had clicked copy before saving, the pending
    ///     is gone. The new blueprint placed after load would not get settings, which
    ///     is correct: the session that set up the copy intent is gone.
    ///   - MP: the pending arm is client-local UI state. The actual plan write happens
    ///     via the synced Commands.ApplyPlanSettings, which executes identically on all
    ///     clients because its parameters are primitive.
    ///
    /// NOTE: The gizmo-sequence wrapping approach (A) is implemented in a shared
    /// helper called from Patch_ConstructionGizmos; the pending state and the
    /// DesignateSingleCell postfix live here.
    public static class CopyPlanPending
    {
        /// Armed when the player clicks a wrapped copy command.
        /// All fields are zero/null when not armed.
        private static Designator_Build? _pendingDesignator;
        private static int   _pendingMinSkill;
        private static bool  _pendingRequireInspired;
        private static bool  _pendingRequireSpecialist;
        private static int   _pendingMinQuality;

        public static bool IsArmed => _pendingDesignator != null;

        /// Arms the pending record. Called from the wrapped copy-command action.
        public static void Arm(Designator_Build des, ConstructionPlan plan)
        {
            _pendingDesignator        = des;
            _pendingMinSkill          = plan.minSkill;
            _pendingRequireInspired   = plan.requireInspired;
            _pendingRequireSpecialist = plan.requireSpecialist;
            _pendingMinQuality        = plan.minQuality;
        }

        /// Clears the pending record. Called when the designator is abandoned or
        /// settings have been applied (but we keep it armed for multi-place).
        public static void Clear()
        {
            _pendingDesignator        = null;
            _pendingMinSkill          = 0;
            _pendingRequireInspired   = false;
            _pendingRequireSpecialist = false;
            _pendingMinQuality        = 0;
        }

        /// Applies settings to the blueprint placed at cell c, if the pending
        /// record is armed and matches the given designator instance.
        public static void TryApply(Designator_Build des, IntVec3 c, Map map)
        {
            if (_pendingDesignator == null) return;
            if (!ReferenceEquals(_pendingDesignator, des)) return;

            // Locate the just-placed Blueprint_Build at c.
            // ThingGrid.ThingAt<T> verified at Decompiled\Verse\ThingGrid.cs line 170.
            Blueprint_Build? bp = map.thingGrid.ThingAt<Blueprint_Build>(c);
            if (bp == null) return;

            // Check if this blueprint is for a CompQuality thing before applying.
            if (!(bp.def.entityDefToBuild is ThingDef tdef) || !tdef.HasComp(typeof(CompQuality)))
                return;

            // Apply via synced command (MP-safe). Parameters are primitive.
            // ApplyPlanSettings handles create-or-overwrite and neutral-remove.
            Commands.ApplyPlanSettings(bp.thingIDNumber,
                _pendingMinSkill, _pendingRequireInspired,
                _pendingRequireSpecialist, _pendingMinQuality);
        }

        /// Wraps the vanilla copy command's action for a given source thing's plan.
        /// Returns the same gizmo with a wrapped action if it is the copy command
        /// (Command_Action with hotKey == KeyBindingDefOf.Misc11), or the original
        /// gizmo unchanged.
        ///
        /// Identification: hotKey == KeyBindingDefOf.Misc11 is verified from
        ///   BuildCopyCommandUtility.BuildCommand line 65 (Decompiled) and is
        ///   stable regardless of locale. We also check the type is Command_Action
        ///   (not a subclass) to avoid matching other commands that might share the key.
        public static Gizmo WrapIfCopyCommand(Gizmo g, ConstructionPlan plan)
        {
            if (g is not Command_Action ca) return g;
            if (ca.hotKey != KeyBindingDefOf.Misc11) return g;

            System.Action? originalAction = ca.action;
            ca.action = () =>
            {
                originalAction?.Invoke();
                // After invoking, the designator manager has selected the Designator_Build.
                if (Find.DesignatorManager.SelectedDesignator is Designator_Build db)
                    Arm(db, plan);
            };
            return g;
        }
    }

    /// Postfix on Designator_Build.DesignateSingleCell: applies pending plan
    /// settings to the just-placed Blueprint_Build.
    ///
    /// DesignateSingleCell signature verified at
    ///   Decompiled\RimWorld\Designator_Build.cs line 455:
    ///   public override void DesignateSingleCell(IntVec3 c)
    [HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.DesignateSingleCell))]
    public static class Patch_CopyPlanSettings_DesignateSingleCell
    {
        public static void Postfix(Designator_Build __instance, IntVec3 c)
        {
            if (!CopyPlanPending.IsArmed) return;
            Map? map = __instance.Map;
            if (map == null) return;
            CopyPlanPending.TryApply(__instance, c, map);
            // Keep pending armed for additional placements (multi-place copies).
        }
    }
}
