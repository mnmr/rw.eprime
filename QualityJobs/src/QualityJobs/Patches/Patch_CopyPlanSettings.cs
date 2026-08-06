using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Fix 4: when the player invokes the vanilla build-copy command on a managed
    /// thing, placed copies receive the same plan settings.
    ///
    /// The actual apply happens at blueprint spawn (Patch_BlueprintSpawn), reading
    /// the synced pending-copy state on the store. This helper only wraps the
    /// vanilla copy command so that clicking it ARMS that synced state via the
    /// Commands.SetPendingCopy [SyncMethod]. There is no longer any client-local
    /// copy static and no DesignateSingleCell postfix: both would diverge across
    /// clients during synced designator replay.
    ///
    /// Vanilla research (BuildCopyCommandUtility.cs):
    ///   BuildCopyCommand returns a Command_Action whose hotKey is
    ///   KeyBindingDefOf.Misc11 (line 65). We identify the copy command by
    ///   type == Command_Action AND hotKey == Misc11 — stable and locale-independent.
    public static class CopyPlanPending
    {
        /// Wraps the vanilla copy command's action for a given source thing's plan.
        /// Returns the same gizmo with a wrapped action if it is the copy command,
        /// or the original gizmo unchanged.
        ///
        /// The wrapped action, after invoking the original (which selects the
        /// build designator on the initiator), issues the synced SetPendingCopy
        /// command carrying the SOURCE plan's settings. The command replicates to
        /// all clients so the spawn hook applies identical settings everywhere.
        public static Gizmo WrapIfCopyCommand(Gizmo g, ConstructionPlan plan)
        {
            if (g is not Command_Action ca) return g;
            if (ca.hotKey != KeyBindingDefOf.Misc11) return g;

            // Capture the source plan's settings by value now: the plan object may
            // change before the action fires, and the synced command needs the
            // exact values that were on-screen when the player chose to copy.
            int minSkill = plan.minSkill;
            bool inspired = plan.requireInspired;
            bool specialist = plan.requireSpecialist;
            int quality = plan.minQuality;
            bool autoBest = plan.autoBest;

            System.Action? originalAction = ca.action;
            ca.action = () =>
            {
                originalAction?.Invoke();
                Commands.SetPendingCopy(minSkill, inspired, specialist, quality, autoBest);
            };
            return g;
        }
    }

    /// Fix 4: best-effort clearing of the synced pending-copy state when the
    /// player stops using a build designator. Deselect is the point vanilla drops
    /// the active designator (verified Decompiled\Verse\DesignatorManager.cs
    /// line 63: public void Deselect()). We clear via the synced ClearPendingCopy
    /// command so all clients agree.
    ///
    /// NOTE: even a STALE pending-copy is DESYNC-SAFE — every client reads the
    /// same synced value, so at worst an extra blueprint gets copy settings it
    /// should not (a minor UX issue), never a divergence. Imperfect clearing is
    /// therefore acceptable.
    [HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.Deselect))]
    public static class Patch_CopyPlanPending_Deselect
    {
        public static void Postfix()
        {
            var s = QualityJobsStore.Active;
            if (s != null && s.pendingCopyActive) Commands.ClearPendingCopy();
        }
    }
}
