using HarmonyLib;
using UnityEngine;
using Verse;
using EPrimeReadouts.Core;
using EPrimeReadouts.UI;

namespace EPrimeReadouts.Patches
{
    /// Activated TipModels size to their structured content and take over
    /// drawing (see Patch_ActiveTip_DrawInner); every other tooltip keeps the
    /// vanilla path.
    [HarmonyPatch(typeof(ActiveTip), "TipRect", MethodType.Getter)]
    public static class Patch_ActiveTip_TipRect
    {
        // Cache contract:
        // Owner: the displayed-tooltip session (single synthetic owner).
        // Key: producer stable key and exact plain-text lookup key.
        // Value: immutable TipModel graph for the displayed tip.
        // Dependencies: display-time activation on every rendered tooltip frame.
        // Refresh policy: event-driven; a registration not re-activated within
        // one frame is dropped by the continuity check after DoTooltipGUI.
        // Equality policy: re-activated values retain identity.
        // Teardown: ReleaseDisplayed per producer reset, Clear on teardown.
        private static readonly OwnerGenerationRegistry<object, string, string, TipModel> models =
            new OwnerGenerationRegistry<object, string, string, TipModel>();
        private static readonly object displayedOwner = new object();
        private static int lastDisplayedFrame = TipContinuity.NoFrame;
        private static int registryEpoch;

        internal static bool HasModels => models.Count > 0;
        internal static int CurrentRegistryEpoch => registryEpoch;

        internal static void Clear()
        {
            models.Clear();
            lastDisplayedFrame = TipContinuity.NoFrame;
            registryEpoch++;
        }

        /// Called from a displayed tooltip's text getter: registers the model
        /// so TipRect/DrawInner take over rendering for that exact text.
        internal static void ActivateDisplayed(StructuredTip tip)
        {
            if (tip == null || tip.RegistryEpoch != registryEpoch) return;
            models.Begin(displayedOwner);
            models.Touch(tip.StableKey, tip.PlainText, tip.Model);
            models.End(displayedOwner);
            lastDisplayedFrame = Time.frameCount;
        }

        /// Vanilla never says "tooltip closed"; a >1 frame gap since the last
        /// activation stands in for it and drops the registration, so vanilla
        /// tooltips stop paying the lookup probe once our tip closes.
        internal static void RetireStaleDisplayed()
        {
            if (models.Count == 0) return;
            if (!TipContinuity.IsBroken(lastDisplayedFrame, Time.frameCount)) return;
            ReleaseDisplayed();
        }

        internal static void ReleaseDisplayed()
        {
            models.Release(displayedOwner);
            lastDisplayedFrame = TipContinuity.NoFrame;
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
            string text;
            var getter = ___signal.textGetter;
            if (getter != null)
            {
                // Only invoke getters this mod created (recognized by their
                // delegate target): this is the designed gather point, run
                // when the tip actually renders. Foreign getter tips keep the
                // untouched vanilla path.
                if (!(getter.Target is IDeferredTipSource)) return true;
                text = getter();
            }
            else
            {
                if (!HasModels) return true;
                text = ___signal.text;
            }
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
    [StaticConstructorOnStartup]
    public static class Patch_ActiveTip_DrawInner
    {
        // Resolved once at startup (StaticConstructorOnStartup runs the field
        // initializer on the main thread after assets load, and satisfies the
        // vanilla dev-mode scanner that flags static Texture2D fields). A miss
        // leaves atlas null and vanilla draws the plain-text fallback.
        // Process-owned reference to a vanilla-owned asset. It is resolved once,
        // never mutated/destroyed by this mod, and expires with the game process.
        private static readonly Texture2D atlas = ActiveTip.TooltipBGAtlas;

        [HarmonyPrefix]
        public static bool Prefix(Rect bgRect, string label)
        {
            if (!Patch_ActiveTip_TipRect.HasModels) return true;
            if (!Patch_ActiveTip_TipRect.TryGetModel(label, out var model)) return true;
            if (atlas == null) return true;
            Widgets.DrawAtlas(bgRect, atlas);
            WrTipUI.Draw(bgRect, model);
            return false;
        }
    }

    /// Retired models remain available through vanilla's ActiveTip draw, then
    /// disappear only after the tooltip GUI has finished with the old signal;
    /// a closed tip's registration is dropped via the continuity check.
    [HarmonyPatch(typeof(TooltipHandler), "DoTooltipGUI")]
    public static class Patch_TooltipHandler_DoTooltipGUI
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            Patch_ActiveTip_TipRect.FlushRetired();
            Patch_ActiveTip_TipRect.RetireStaleDisplayed();
        }
    }
}
