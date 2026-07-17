using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;
using WorkRoles.UI;

namespace WorkRoles.Patches
{
    /// Vanilla tooltips hard-wrap at 260px (ActiveTip.MaxWidth, a private
    /// const). The editor's skill-fact tips are pre-formatted line lists that
    /// must not wrap, so texts registered here size to their content; every
    /// other tooltip keeps vanilla sizing. Registered TipModels additionally
    /// take over drawing (see Patch_ActiveTip_DrawInner).
    [HarmonyPatch(typeof(ActiveTip), "TipRect", MethodType.Getter)]
    public static class Patch_ActiveTip_TipRect
    {
        private const float SafetyMaxWidth = 500f;

        // Keyed by tip text: composed tips are long and unique, so a
        // byte-identical collision with a foreign tooltip is theoretical (and
        // would merely widen it). Cleared on language switch with the tip caches
        // that re-register on rebuild.
        private static readonly HashSet<string> wide = new HashSet<string>();
        private static readonly Dictionary<string, TipModel> models = new Dictionary<string, TipModel>();

        internal static void Clear()
        {
            wide.Clear();
            models.Clear();
        }

        internal static string RegisterWide(string text)
        {
            wide.Add(text);
            return text;
        }

        /// The returned plain text is the TipSignal text: it draws structured
        /// while registered and degrades to readable plain text otherwise.
        internal static string Register(TipModel model)
        {
            string text = model.ToPlainText();
            models[text] = model;
            return text;
        }

        internal static bool TryGetModel(string text, out TipModel model)
        {
            model = null;
            return text != null && models.TryGetValue(text, out model);
        }

        [HarmonyPrefix]
        public static bool Prefix(TipSignal ___signal, ref Rect __result)
        {
            string text = ___signal.text?.TrimEnd();
            if (text == null) return true;
            if (models.TryGetValue(text, out var model))
            {
                Vector2 modelSize = WrTipUI.Measure(model, SafetyMaxWidth);
                __result = new Rect(0f, 0f, modelSize.x, modelSize.y);
                return false;
            }
            if (!wide.Contains(text)) return true;
            var oldFont = Text.Font;
            Text.Font = GameFont.Small;
            Vector2 size = Text.CalcSize(text);
            if (size.x > SafetyMaxWidth)
            {
                size.x = SafetyMaxWidth;
                size.y = Text.CalcHeight(text, size.x);
            }
            Text.Font = oldFont;
            __result = new Rect(0f, 0f, size.x, size.y).ContractedBy(-4f).RoundedCeil();
            return false;
        }
    }

    /// Registered models draw themselves (atlas background + WrTipUI); every
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
}
