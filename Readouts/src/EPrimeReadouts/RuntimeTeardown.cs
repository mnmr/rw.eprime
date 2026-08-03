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
            LevelStacks.Reset();
            GameResourceCatalog.Reset();
            GameResourceTree.Reset();
            ReadoutPanel.Reset();
            IconTips.Reset();
            WrText.Reset();
            PanelCellMetrics.Reset();
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
        private bool graphicsInitialized;

        // Map components are constructed while save data is deserialized on a
        // LongEventHandler worker thread. Keep this constructor free of Unity
        // API calls; graphics initialization belongs in the main-thread update.
        // The map-set bump is a plain counter increment, safe off-thread.
        public ReadoutRenderMapComponent(Map map) : base(map)
        {
            LevelStacks.BumpMapSet();
        }

        public override void MapComponentUpdate()
        {
            if (!graphicsInitialized)
            {
                ReadoutTextures.EnsureOwned();
                graphicsInitialized = true;
            }
            IconScaleCache.ProcessPending();
        }

        public override void MapRemoved()
        {
            LevelStacks.BumpMapSet();
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
