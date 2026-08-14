using HarmonyLib;
using QualityJobs.UI;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Per-frame hook that draws sparkle overlays for all Blueprint_Build and Frame
    /// targets with an active quality construction plan.
    ///
    /// Patch target: Verse.DynamicDrawManager.DrawDynamicThings()
    /// Verified at Decompiled\Verse\DynamicDrawManager.cs:182:
    ///   public void DrawDynamicThings()
    ///
    /// Map access: DynamicDrawManager has a private `map` field (DynamicDrawManager.cs:145:
    ///   private Map map;). Accessed via a cached AccessTools.FieldRef — one static
    ///   delegate allocation at patch init, then a field-dereference per call.
    ///
    /// Why DynamicDrawManager and not Frame/Blueprint.DrawAt:
    ///   Blueprints use DrawerType.MapMeshOnly (ThingDefGenerator_Buildings.cs:62) and
    ///   are never drawn through the dynamic draw path — DrawAt is never called for
    ///   them. Frames use RealtimeOnly and ARE on the dynamic path, but anchoring both
    ///   to one per-frame hook avoids the fragility of separate type patches.
    ///
    /// Hot-path design: QualityJobsStore publishes immutable per-map draw snapshots
    /// at plan invalidation time. The postfix performs a static fast-path check,
    /// resolves one cached map snapshot, and submits its precomputed draw models.
    ///
    /// Gravship guard: WorldComponent_GravshipController.CutsceneInProgress and
    /// GravshipRenderInProgess verified at Decompiled\Verse\WorldComponent_GravshipController.cs:85,87.
    [HarmonyPatch(typeof(DynamicDrawManager), nameof(DynamicDrawManager.DrawDynamicThings))]
    public static class Patch_SparkleOverlay
    {
        // Cached field accessor: one allocation at patch init, then a direct field
        // dereference per postfix call. Caching as static readonly satisfies the
        // AGENTS.md no-delegate-at-call-site rule.
        private static readonly AccessTools.FieldRef<DynamicDrawManager, Map> MapRef =
            AccessTools.FieldRefAccess<DynamicDrawManager, Map>("map");

        public static void Postfix(DynamicDrawManager __instance)
        {
            // Gravship cutscene guard: mirror the same guard used by Frame.DrawAt
            // (Frame.cs:441) and Blueprint.DrawAt (Blueprint.cs:96).
            if (WorldComponent_GravshipController.CutsceneInProgress
                && !WorldComponent_GravshipController.GravshipRenderInProgess)
                return;

            // Fast pre-check: one static bool read; returns immediately when no
            // plan exists (the common case). Updated wherever plans are mutated.
            if (!QualityJobsStore.AnyOverlays) return;

            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;

            // Resolve the map this DynamicDrawManager belongs to.
            Map map = MapRef(__instance);
            if (map == null) return;

            if (store.TryGetOverlayPresentation(map,
                    out SparkleOverlay.MapSnapshot? snapshot)
                && snapshot != null)
                SparkleOverlay.Draw(snapshot);
        }
    }
}
