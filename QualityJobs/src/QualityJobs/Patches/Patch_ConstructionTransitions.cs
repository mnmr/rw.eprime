using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Blueprint -> frame: retarget the plan onto the created frame
    /// (Blueprint.cs:45-92). Runs in the synced construction job.
    [HarmonyPatch(typeof(Blueprint), nameof(Blueprint.TryReplaceWithSolidThing))]
    public static class Patch_ConstructionTransitions_BlueprintToFrame
    {
        public static void Postfix(Blueprint __instance, bool __result, Thing createdThing)
        {
            if (!__result || createdThing == null) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.plans.Count == 0) return;
            ConstructionPlan? plan = store.FindPlan(__instance);
            if (plan == null) return;
            if (createdThing is Frame)
                store.RetargetPlan(plan, createdThing, plan.state);
            else
                store.RemovePlan(plan); // non-frame solid thing: nothing to gate
        }
    }

    /// Frame -> blueprint on vanilla construction failure (Frame.cs:408-437):
    /// keep managing the re-placed blueprint.
    [HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]
    public static class Patch_ConstructionTransitions_FailConstruction
    {
        public struct FailState
        {
            public ConstructionPlan? plan;
            public Map? map;
            public IntVec3 position;
            public ThingDef? blueprintDef;
        }

        public static void Prefix(Frame __instance, out FailState __state)
        {
            __state = default;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.plans.Count == 0) return;
            ConstructionPlan? plan = store.FindPlan(__instance);
            if (plan == null) return;
            __state.plan = plan;
            __state.map = __instance.Map;
            __state.position = __instance.Position;
            __state.blueprintDef = __instance.def.entityDefToBuild?.blueprintDef;
        }

        public static void Postfix(FailState __state)
        {
            ConstructionPlan? plan = __state.plan;
            if (plan == null) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            Thing? blueprint = __state.map != null && __state.blueprintDef != null
                ? __state.map.thingGrid.ThingAt(__state.position, __state.blueprintDef)
                : null;
            if (blueprint != null)
            {
                store.RetargetPlan(plan, blueprint, ConstructionPlanState.Active);
            }
            else
            {
                store.RemovePlan(plan); // no blueprint re-placed — plan dies
            }
        }
    }

    /// Building deconstructed while AwaitingRebuild: place a fresh blueprint
    /// with the same def/stuff/rotation/style (spec §10 retry loop). Any other
    /// destruction of a tracked target drops the plan.
    [HarmonyPatch(typeof(Building), nameof(Building.Destroy))]
    public static class Patch_ConstructionTransitions_Rebuild
    {
        public struct DestroyState
        {
            public ConstructionPlan? plan;
            public Map? map;
            public IntVec3 position;
            public Rot4 rotation;
            public ThingDef? def;
            public ThingDef? stuff;
            public Precept_ThingStyle? styleSource;
            public ThingStyleDef? styleDef;
            public Faction? faction;
        }

        public static void Prefix(Building __instance, DestroyMode mode, out DestroyState __state)
        {
            __state = default;
            // Frame IS a Building: its destruction during CompleteConstruction
            // (Frame.cs:280) and FailConstruction (Frame.cs:411) flows through
            // this patch. Frames are owned by the gate/fail patches, which
            // retarget the plan themselves; removing it here would orphan the
            // plan mid-transition (lost building on retry, unmanaged blueprint
            // on failure). Untracked frame deaths (cancel, fire) belong to the
            // scan sweep.
            if (__instance is Frame) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.plans.Count == 0) return;
            ConstructionPlan? plan = store.FindPlan(__instance);
            if (plan == null) return;
            __state.plan = plan;
            if (plan.state == ConstructionPlanState.AwaitingRebuild
                && mode == DestroyMode.Deconstruct && __instance.Spawned)
            {
                __state.map = __instance.Map;
                __state.position = __instance.Position;
                __state.rotation = __instance.Rotation;
                __state.def = __instance.def;
                __state.stuff = __instance.Stuff;
                __state.styleSource = __instance.StyleSourcePrecept;
                __state.styleDef = __instance.StyleDef;
                __state.faction = __instance.Faction;
            }
        }

        public static void Postfix(DestroyState __state)
        {
            ConstructionPlan? plan = __state.plan;
            if (plan == null) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;

            if (__state.map == null || __state.def == null)
            {
                // Destroyed outside the tracked deconstruct path (burned,
                // damage, minified, non-rebuild state): plan dies with it.
                store.RemovePlan(plan);
                return;
            }

            // Suppress the spawn hook for this blueprint: PlaceBlueprintForBuild
            // spawns synchronously (firing Patch_BlueprintSpawn before it returns),
            // but this blueprint is owned by the existing AwaitingRebuild plan we
            // retarget below — the hook must not create a second plan for it. The
            // flag is set/cleared inside synced Building.Destroy, deterministic on
            // all clients.
            Blueprint_Build blueprint;
            Patch_BlueprintSpawn.SuppressForRebuild = true;
            try
            {
                blueprint = GenConstruct.PlaceBlueprintForBuild(
                    __state.def, __state.position, __state.map, __state.rotation,
                    __state.faction ?? Faction.OfPlayer, __state.stuff,
                    __state.styleSource, __state.styleDef);
            }
            finally
            {
                Patch_BlueprintSpawn.SuppressForRebuild = false;
            }
            store.RetargetPlan(plan, blueprint, ConstructionPlanState.Active);
        }
    }
}
