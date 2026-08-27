using System.Collections.Generic;
using HarmonyLib;
using QualityJobs.UI;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Per-instance opt-in (spec §10): a single Command_QualityJob gizmo for
    /// player-faction, CompQuality builds that appears always (whether or not a
    /// plan exists). Clicking opens Dialog_ConstructionPlanConfig anchored to the
    /// bottom of the gizmo button. Commands are cached by thing ID and reuse a
    /// non-allocating append enumerator around vanilla's gizmo sequence.
    ///
    /// The icon reflects plan presence: GizmoEnabled when a plan exists, GizmoDisabled
    /// otherwise. The cached command's presentation fields refresh from the
    /// published plan snapshot without rebuilding the command.
    ///
    /// Multi-select (Fix 5): Command.GroupsWith (Command.cs:275) merges commands
    /// with matching hotKey+Label+icon+groupKey. All our commands share label
    /// "Quality Job" and the same icon, so they group and one click opens a dialog
    /// that captures all selected eligible things at open time.
    public static class ConstructionGizmos
    {
        /// Checks whether a thing (Blueprint_Build or Frame) is eligible for a
        /// quality plan gizmo: player-faction, backed by a CompQuality ThingDef.
        public static bool IsEligibleBuildable(Thing thing, ThingDef? buildDef)
        {
            if (buildDef == null || !buildDef.HasComp(typeof(CompQuality))) return false;
            if (thing.Faction != Faction.OfPlayer) return false;
            return true;
        }

        public static IEnumerable<Gizmo> Append(IEnumerable<Gizmo> gizmos, Thing thing, ThingDef? buildDef)
        {
            if (!IsEligibleBuildable(thing, buildDef))
                return gizmos;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null)
                return gizmos;

            bool hasPlan = store.TryGetPlanPresentation(
                thing.thingIDNumber, out _);
            return store.ConstructionCommandFor(
                thing.thingIDNumber, hasPlan).AppendTo(gizmos);
        }

        /// Builds the list of all selected eligible things for multi-select.
        /// Eligible: player-faction Blueprint_Build/Frame with a CompQuality build def,
        /// plus Buildings that already have a plan (AwaitingRebuild).
        /// The primary thing is always first. Falls back to [primary] if Selector
        /// is unavailable (e.g. during tests).
        public static List<int> CollectSelectedIds(int primaryThingId)
        {
            var result = new List<int> { primaryThingId };
            // Find.Selector may be null outside of play; guard defensively.
            if (Find.Selector == null) return result;
            QualityJobsStore? store = QualityJobsStore.Active;
            List<object> sel = Find.Selector.SelectedObjects;
            // Primary first so the dialog reads values from the initiating thing.
            for (int i = 0; i < sel.Count; i++)
            {
                object obj = sel[i];
                if (!(obj is Thing thing) || thing.thingIDNumber == primaryThingId)
                    continue;
                if (obj is Blueprint_Build bp && IsEligibleBuildable(bp, bp.def.entityDefToBuild as ThingDef))
                    result.Add(bp.thingIDNumber);
                else if (obj is Frame fr && IsEligibleBuildable(fr, fr.BuildDef))
                    result.Add(fr.thingIDNumber);
                else if (obj is Building bld && !(obj is Frame)
                    && store != null
                    && store.TryGetPlanPresentation(bld.thingIDNumber, out _))
                    result.Add(bld.thingIDNumber);
            }
            return result;
        }

        public static Map? SelectedMapFor(int thingId)
        {
            if (Find.Selector == null) return null;
            List<object> selected = Find.Selector.SelectedObjects;
            for (int i = 0; i < selected.Count; i++)
                if (selected[i] is Thing thing && thing.thingIDNumber == thingId)
                    return thing.MapHeld;
            return null;
        }
    }

    /// Patches Blueprint_Build.GetGizmos (the DERIVED class), NOT Blueprint.
    /// The vanilla build-copy command is yielded by Blueprint_Build.GetGizmos
    /// (Blueprint_Build.cs:87) AFTER base.GetGizmos() returns — so a postfix on
    /// the base Blueprint.GetGizmos never sees it and could not wrap it (that was
    /// the "copying a blueprint drops quality settings" bug). Patching the derived
    /// method gives us the full gizmo list including the copy command.
    [HarmonyPatch(typeof(Blueprint_Build), nameof(Blueprint_Build.GetGizmos))]
    public static class Patch_ConstructionGizmos_Blueprint
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Blueprint_Build __instance)
            => ConstructionGizmos.Append(gizmos, __instance,
                __instance.def.entityDefToBuild as ThingDef);
    }

    [HarmonyPatch(typeof(Frame), nameof(Frame.GetGizmos))]
    public static class Patch_ConstructionGizmos_Frame
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Frame __instance)
            => ConstructionGizmos.Append(gizmos, __instance, __instance.BuildDef);
    }

    /// Postfix on Building.GetGizmos (line 401 of Decompiled\Verse\Building.cs):
    ///   public override IEnumerable<Gizmo> GetGizmos()
    /// Adds the Quality-job gizmo to buildings that are AwaitingRebuild (i.e. they
    /// have a plan and are waiting for a deconstruct-rebuild cycle). Frames already
    /// have their own patch; Frame is a Building so we skip it here. We never offer
    /// plan creation on arbitrary completed buildings — only expose the gizmo when
    /// a plan already exists for this specific building.
    [HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
    public static class Patch_ConstructionGizmos_Building
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Building __instance)
        {
            // Skip Frames — they are Buildings but have their own patch above.
            if (__instance is Frame)
                return gizmos;

            // Fast-path: skip the component lookup when there are no plans at all.
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null)
                return gizmos;

            // Only offer the gizmo when this specific building already has a plan
            // (AwaitingRebuild state). Never create plans on arbitrary buildings.
            if (!store.TryGetPlanPresentation(__instance.thingIDNumber, out _))
                return gizmos;

            // Reuse the same gizmo shape as Blueprint/Frame patches.
            return store.ConstructionCommandFor(
                __instance.thingIDNumber, enabled: true).AppendTo(gizmos);
        }
    }
}
