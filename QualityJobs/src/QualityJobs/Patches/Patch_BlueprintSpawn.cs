using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Deterministic spawn lifecycle: apply this exact placement command's
    /// copied settings, or the per-save defaults for every other blueprint.
    /// Local selection/pending input is never consulted here.
    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    public static class Patch_BlueprintSpawn
    {
        /// Set true by Patch_ConstructionTransitions_Rebuild around its
        /// GenConstruct.PlaceBlueprintForBuild call. That call spawns the rebuild
        /// blueprint SYNCHRONOUSLY (SpawnSetup fires before it returns) and then
        /// the rebuild postfix retargets its existing AwaitingRebuild plan onto
        /// the new blueprint — so at spawn time FindPlan(bp) is still null and the
        /// AwaitingRebuild plan's old target is already despawned (an
        /// OccupiedRect check can't see it). This flag is the reliable exclusion:
        /// while set, the spawn hook creates no plan for the rebuild blueprint.
        ///
        /// MP-safe: set/cleared ONLY inside Building.Destroy (synced simulation)
        /// on every client, so it is true during the hook identically everywhere.
        public static bool SuppressForRebuild;

        // The postfix intentionally patches the BASE Thing.SpawnSetup and fires at
        // that boundary (before CompQuality's own SpawnSetup init). It only reads
        // def-level data (entityDefToBuild.HasComp) and Position/Map, all set by
        // the base method, so running before comp init is safe.
        public static void Postfix(Thing __instance, bool respawningAfterLoad)
        {
            // respawningAfterLoad: the blueprint already existed and its plan (if
            // any) is scribed — do not re-apply anything on load.
            if (respawningAfterLoad) return;
            if (SuppressForRebuild) return; // rebuild blueprint: owned by its plan
            if (!(__instance is Blueprint_Build bp)) return;

            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;

            // Only act on blueprints for CompQuality things.
            if (!(bp.def.entityDefToBuild is ThingDef tdef) || !tdef.HasComp(typeof(CompQuality)))
                return;

            // Already tracked (e.g. copy applied earlier this tick, or a prior
            // pass) — nothing to do.
            if (store.FindPlan(bp) != null) return;

            // Copy scope exists only around its native synchronized placement.
            // All-neutral copied settings deliberately suppress the defaults.
            if (CopyPlacement.TryApply(store, bp)) return;
            if (store.manageNewConstructionDefault)
            {
                // Auto-create path: apply the store's construction defaults.
                store.ApplyPlanSettings(bp.thingIDNumber,
                    store.constructionMinSkillDefault,
                    store.constructionRequireInspiredDefault,
                    store.constructionRequireSpecialistDefault,
                    store.constructionTargetQualityDefault,
                    store.constructionAutoBestDefault);
            }
        }
    }
}
