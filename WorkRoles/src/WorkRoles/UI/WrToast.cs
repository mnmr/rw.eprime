using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Mod-owned notification toasts: dark panels top-center of the WorkRoles
    /// window (vanilla's floating top-left text is hard to read there). Falls
    /// back to Messages.Message while the window is closed, so feedback is
    /// never lost. Local-only UI: synced command bodies may call Show — each
    /// client renders its own copy, no sim state is touched.
    public static class WrToast
    {
        private class Toast
        {
            public string text;
            public float expiry;
        }

        private const float DurationSeconds = 4f;
        private const float MaxTextWidth = 420f;

        // The mod's panel idiom (role editor top box, Options panels).

        private static readonly List<Toast> toasts = new List<Toast>();

        public static void Show(string text, MessageTypeDef fallbackType)
        {
            if (text.NullOrEmpty()) return;
            if (Find.WindowStack?.IsOpen<MainTabWindow_WorkRoles>() == true)
                toasts.Add(new Toast { text = text, expiry = Time.realtimeSinceStartup + DurationSeconds });
            else
                Messages.Message(text, fallbackType, historical: false);
        }

        /// Called last in the window's draw so toasts paint on top; expiry is
        /// checked against realtime on draw — event-driven, no polling.
        public static void Draw(Rect inRect)
        {
            if (toasts.Count == 0) return;
            toasts.RemoveAll(t => Time.realtimeSinceStartup >= t.expiry);
            Text.Font = GameFont.Small;
            float y = inRect.y + 4f;
            foreach (var toast in toasts)
            {
                float textW = Mathf.Min(Text.CalcSize(toast.text).x, Mathf.Min(MaxTextWidth, inRect.width - 56f));
                float textH = Text.CalcHeight(toast.text, textW + 1f);
                var panel = new Rect(inRect.x + (inRect.width - textW - 24f) / 2f, y, textW + 24f, textH + 12f);
                Widgets.DrawBoxSolidWithOutline(
                    panel, WrStyle.PanelBackground, WrStyle.PanelOutline);
                var oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(panel.x + 12f, panel.y + 6f, textW, textH), toast.text);
                Text.Anchor = oldAnchor;
                y += panel.height + 4f;
            }
        }
    }
}
