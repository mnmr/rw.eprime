using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WorkRoles.Patches
{
    /// Vanilla tooltips hard-wrap at 260px (ActiveTip.MaxWidth, a private
    /// const). The editor's skill-fact tips are pre-formatted line lists that
    /// must not wrap, so texts registered here size to their content; every
    /// other tooltip keeps vanilla sizing.
    [HarmonyPatch(typeof(ActiveTip), "TipRect", MethodType.Getter)]
    public static class Patch_ActiveTip_TipRect
    {
        private const float SafetyMaxWidth = 500f;

        private static readonly HashSet<string> wide = new HashSet<string>();

        internal static string RegisterWide(string text)
        {
            wide.Add(text);
            return text;
        }

        [HarmonyPrefix]
        public static bool Prefix(TipSignal ___signal, ref Rect __result)
        {
            string text = ___signal.text?.TrimEnd();
            if (text == null || !wide.Contains(text)) return true;
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
}
