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
    /// bottom of the gizmo button. Gizmo construction allocates per GetGizmos
    /// call — vanilla behavior for selection-time UI, not a per-frame path.
    ///
    /// The icon reflects plan presence: GizmoEnabled when a plan exists, GizmoDisabled
    /// otherwise. Gizmos are rebuilt each selection frame so the state icon follows
    /// plan existence naturally without any caching.
    ///
    /// Multi-select (Fix 5): Command.GroupsWith (Command.cs:275) merges commands
    /// with matching hotKey+Label+icon+groupKey. All our commands share label
    /// "Quality job" and the same icon, so they group and one click opens a dialog
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
            {
                foreach (Gizmo g in gizmos) yield return g;
                yield break;
            }
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null)
            {
                foreach (Gizmo g in gizmos) yield return g;
                yield break;
            }

            ConstructionPlan? plan = store.FindPlan(thing);

            // Pass gizmos through, wrapping the vanilla copy command when a plan exists
            // so that placed copies receive the same plan settings (Fix 4).
            foreach (Gizmo g in gizmos)
            {
                yield return plan != null ? CopyPlanPending.WrapIfCopyCommand(g, plan) : g;
            }

            // Multi-select: collect all currently selected eligible things of the
            // same kind (Blueprint_Build or Frame). The dialog will operate on all.
            // We allocate here at selection time — this is the GetGizmos allocation
            // budget, not a per-frame path.
            List<Thing> allSelected = CollectSelected(thing);

            yield return new Command_QualityJob(allSelected)
            {
                defaultLabel = "QJ_GizmoQualityJobLabel".Translate(),
                defaultDesc = "QJ_GizmoManageDesc".Translate(),
                icon = plan != null ? QualityJobsTex.GizmoEnabled : QualityJobsTex.GizmoDisabled,
            };
        }

        /// Builds the list of all selected eligible things for multi-select.
        /// Eligible: player-faction Blueprint_Build/Frame with a CompQuality build def,
        /// plus Buildings that already have a plan (AwaitingRebuild).
        /// The primary thing is always first. Falls back to [primary] if Selector
        /// is unavailable (e.g. during tests).
        public static List<Thing> CollectSelected(Thing primary)
        {
            var result = new List<Thing>();
            // Find.Selector may be null outside of play; guard defensively.
            if (Find.Selector == null)
            {
                result.Add(primary);
                return result;
            }
            QualityJobsStore? store = QualityJobsStore.Active;
            List<object> sel = Find.Selector.SelectedObjects;
            // Primary first so the dialog reads values from the initiating thing.
            result.Add(primary);
            for (int i = 0; i < sel.Count; i++)
            {
                object obj = sel[i];
                if (obj == primary) continue;
                if (obj is Blueprint_Build bp && IsEligibleBuildable(bp, bp.def.entityDefToBuild as ThingDef))
                    result.Add(bp);
                else if (obj is Frame fr && IsEligibleBuildable(fr, fr.BuildDef))
                    result.Add(fr);
                else if (obj is Building bld && !(obj is Frame)
                    && store != null && store.FindPlan(bld) != null)
                    result.Add(bld);
            }
            return result;
        }
    }

    [HarmonyPatch(typeof(Blueprint), nameof(Blueprint.GetGizmos))]
    public static class Patch_ConstructionGizmos_Blueprint
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Blueprint __instance)
            => __instance is Blueprint_Build
                ? ConstructionGizmos.Append(gizmos, __instance,
                    __instance.def.entityDefToBuild as ThingDef)
                : gizmos;
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
            {
                foreach (Gizmo g in gizmos) yield return g;
                yield break;
            }

            // Fast-path: skip the component lookup when there are no plans at all.
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.plans.Count == 0)
            {
                foreach (Gizmo g in gizmos) yield return g;
                yield break;
            }

            // Only offer the gizmo when this specific building already has a plan
            // (AwaitingRebuild state). Never create plans on arbitrary buildings.
            ConstructionPlan? plan = store.FindPlan(__instance);
            if (plan == null)
            {
                foreach (Gizmo g in gizmos) yield return g;
                yield break;
            }

            // Pass gizmos through, wrapping the vanilla copy command for Fix 4.
            foreach (Gizmo g in gizmos)
                yield return CopyPlanPending.WrapIfCopyCommand(g, plan);

            // Collect all selected eligible things for multi-select (Fix 5).
            List<Thing> allSelected = ConstructionGizmos.CollectSelected(__instance);

            // Reuse the same gizmo shape as Blueprint/Frame patches.
            yield return new Command_QualityJob(allSelected)
            {
                defaultLabel = "QJ_GizmoQualityJobLabel".Translate(),
                defaultDesc = "QJ_GizmoManageDesc".Translate(),
                icon = QualityJobsTex.GizmoEnabled,
            };
        }
    }
}
