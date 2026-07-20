using HarmonyLib;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.UI;

namespace WorkRoles.Patches
{
    /// Activated TipModels size to their structured content and take over
    /// drawing (see Patch_ActiveTip_DrawInner); every other tooltip keeps the
    /// vanilla path.
    [HarmonyPatch(typeof(ActiveTip), "TipRect", MethodType.Getter)]
    public static class Patch_ActiveTip_TipRect
    {
        private static readonly OwnerGenerationRegistry<object, string, string, TipModel> models =
            new OwnerGenerationRegistry<object, string, string, TipModel>();
        private static bool generationActive;
        private static int registryEpoch;

        internal static bool HasModels => models.Count > 0;
        internal static int CurrentRegistryEpoch => registryEpoch;

        internal static void Clear()
        {
            models.Clear();
            generationActive = false;
            registryEpoch++;
        }

        internal static void BeginGeneration(object owner)
        {
            models.Begin(owner);
            generationActive = true;
        }

        internal static void EndGeneration(object owner)
        {
            if (!generationActive) return;
            models.End(owner);
            generationActive = false;
        }

        internal static void ReleaseOwner(object owner)
        {
            models.Release(owner);
        }

        internal static void Activate(StructuredTip tip)
        {
            if (!generationActive || tip == null
                || tip.RegistryEpoch != registryEpoch) return;
            models.Touch(tip.StableKey, tip.PlainText, tip.Model);
        }

        internal static void FlushRetired()
        {
            models.FlushRetired();
        }

        internal static bool TryGetModel(string text, out TipModel model)
        {
            model = null;
            return text != null && models.TryGet(text, out model);
        }

        [HarmonyPrefix]
        public static bool Prefix(TipSignal ___signal, ref Rect __result)
        {
            if (!HasModels) return true;
            string text = ___signal.text?.TrimEnd();
            if (text == null) return true;
            if (models.TryGet(text, out var model))
            {
                Vector2 modelSize = WrTipUI.Measure(model, WrTipUI.MaxContentWidth);
                __result = new Rect(0f, 0f, modelSize.x, modelSize.y);
                return false;
            }
            return true;
        }
    }

    /// Activated models draw themselves (atlas background + WrTipUI); every
    /// other tooltip keeps the vanilla single-label path.
    [HarmonyPatch(typeof(ActiveTip), "DrawInner")]
    public static class Patch_ActiveTip_DrawInner
    {
        // Vanilla's private background atlas; fetched lazily. A miss (field
        // renamed) leaves atlas null and vanilla draws the plain-text fallback.
        private static Texture2D atlas;
        private static bool atlasTried;

        [HarmonyPrefix]
        public static bool Prefix(Rect bgRect, string label)
        {
            if (!Patch_ActiveTip_TipRect.HasModels) return true;
            if (!Patch_ActiveTip_TipRect.TryGetModel(label?.TrimEnd(), out var model)) return true;
            if (!atlasTried)
            {
                atlasTried = true;
                atlas = AccessTools.Field(typeof(ActiveTip), "TooltipBGAtlas")?.GetValue(null) as Texture2D;
            }
            if (atlas == null) return true;
            Widgets.DrawAtlas(bgRect, atlas);
            WrTipUI.Draw(bgRect, model);
            return false;
        }
    }

    /// Retired models remain available through vanilla's ActiveTip draw, then
    /// disappear only after the tooltip GUI has finished with the old signal.
    [HarmonyPatch(typeof(TooltipHandler), "DoTooltipGUI")]
    public static class Patch_TooltipHandler_DoTooltipGUI
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            Patch_ActiveTip_TipRect.FlushRetired();
        }
    }
}
