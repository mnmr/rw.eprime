using EPrimeReadouts.Patches;
using EPrimeReadouts.UI;
using HarmonyLib;
using Verse;
using Verse.Profile;

namespace EPrimeReadouts
{
    /// <summary>Central, idempotent release for process-static runtime state.</summary>
    internal static class RuntimeTeardown
    {
        internal static void ResetAll()
        {
            GameRenderData.Reset();
            GameResourceCatalog.Reset();
            GameResourceTree.Reset();
            ReadoutPanel.Reset();
            IconTips.Reset();
            WrText.Reset();
            UiText.Reset();
            EprStyle.Reset();
            EprDrag.Cancel();
            Patch_ActiveTip_TipRect.Clear();
            IconScaleCache.Reset();
            ReadoutTextures.ResetOwned();
        }
    }

    /// <summary>Releases the per-map render entry as soon as a map is removed.</summary>
    public sealed class ReadoutRenderMapComponent : MapComponent
    {
        public ReadoutRenderMapComponent(Map map) : base(map)
        {
            ReadoutTextures.EnsureOwned();
        }

        public override void MapComponentUpdate()
        {
            IconScaleCache.ProcessPending();
        }

        public override void MapRemoved()
        {
            GameRenderData.Remove(map);
            ReadoutPanel.ReleaseMap(map);
            base.MapRemoved();
        }
    }

    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
    internal static class Patch_MemoryUtility_ClearAllMapsAndWorld
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            RuntimeTeardown.ResetAll();
        }
    }
}
