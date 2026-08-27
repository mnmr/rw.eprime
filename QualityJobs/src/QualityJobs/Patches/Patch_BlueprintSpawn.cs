using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Fix 3: auto-create and copy application moved to a deterministic spawn
    /// hook. When a Blueprint_Build for a CompQuality thing spawns during play,
    /// we apply either the synced pending-copy settings or the store's
    /// construction defaults, reading ONLY synced store state.
    ///
    /// MP determinism argument:
    ///   RimWorld-Multiplayer syncs build designators at the REPLAY model:
    ///   Designator_Build.DesignateSingleCell and Blueprint_Build.SpawnSetup run
    ///   on ALL clients during synced command replay. This hook therefore runs on
    ///   every client with identical timing. It reads ONLY the synced store
    ///   (pendingCopy* fields set via [SyncMethod], and the scribed construction
    ///   defaults) and mutates the store directly through PlanOps.Apply — which is
    ///   deterministic, exactly like the gate/scan already mutate the store during
    ///   synced replay. No client-local state is consulted, so no divergence is
    ///   possible. (The old DesignateSingleCell postfix consulted a client-local
    ///   static that only the initiator had armed, which desynced; that postfix
    ///   and its static are removed.)
    ///
    /// SpawnSetup signature verified at Decompiled\Verse\Thing.cs line 803:
    ///   public override void SpawnSetup(Map map, bool respawningAfterLoad)
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

            // Application order, both via PlanOps.Apply (direct store mutation;
            // deterministic in synced replay, reads only synced store):
            if (store.pendingCopyActive)
            {
                // Copy path: apply the synced source plan settings.
                PlanOps.Apply(store, bp.thingIDNumber,
                    store.pendingCopyMinSkill,
                    store.pendingCopyInspired,
                    store.pendingCopySpecialist,
                    store.pendingCopyQuality,
                    store.pendingCopyAutoBest);
            }
            else if (store.manageNewConstructionDefault
                && (store.constructionMinSkillDefault > 0
                    || store.constructionRequireInspiredDefault
                    || store.constructionRequireSpecialistDefault
                    || store.constructionTargetQualityDefault > 0
                    || store.constructionAutoBestDefault))
            {
                // Auto-create path: apply the store's construction defaults.
                PlanOps.Apply(store, bp.thingIDNumber,
                    store.constructionMinSkillDefault,
                    store.constructionRequireInspiredDefault,
                    store.constructionRequireSpecialistDefault,
                    store.constructionTargetQualityDefault,
                    store.constructionAutoBestDefault);
            }
        }
    }
}
