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
        private sealed class Toast
        {
            internal Toast(string text, float expiry)
            {
                Text = text;
                Expiry = expiry;
            }

            internal string Text { get; }
            internal float Expiry { get; }
        }

        private readonly struct ToastLayoutRow
        {
            internal ToastLayoutRow(string text, float textWidth,
                float textHeight)
            {
                Text = text;
                TextWidth = textWidth;
                TextHeight = textHeight;
            }

            internal string Text { get; }
            internal float TextWidth { get; }
            internal float TextHeight { get; }

            internal bool ContentEquals(ToastLayoutRow other) =>
                Text == other.Text
                && TextWidth == other.TextWidth
                && TextHeight == other.TextHeight;
        }

        private sealed class ToastLayoutSnapshot
        {
            internal static readonly ToastLayoutSnapshot Empty =
                new ToastLayoutSnapshot(new ToastLayoutRow[0]);

            private readonly ToastLayoutRow[] rows;

            internal ToastLayoutSnapshot(ToastLayoutRow[] rows)
            {
                this.rows = rows;
            }

            internal int Count => rows.Length;
            internal ToastLayoutRow RowAt(int index) => rows[index];

            internal bool ContentEquals(ToastLayoutRow[] other)
            {
                if (other == null || rows.Length != other.Length) return false;
                for (int i = 0; i < rows.Length; i++)
                    if (!rows[i].ContentEquals(other[i])) return false;
                return true;
            }
        }

        private const float DurationSeconds = 4f;
        private const float MaxTextWidth = 420f;

        // Owner: active WorkRoles window; process-static storage is released
        // when that window closes or shared window data is torn down.
        // Key: the current toast revision, available width, and language
        // revision for the singleton active window session.
        // Value: immutable ToastLayoutSnapshot whose private row array is
        // transferred on publication and never mutated afterward.
        // Dependencies: ordered toast text, GameFont.Small metrics, available
        // width, and LanguageChangeCoordinator.Revision.
        // Refresh: toast additions and WindowUpdate expiry removal advance the
        // source revision; Repaint immediately rebuilds after the gate fires.
        // Equality: an equal rebuild retains the published snapshot identity.
        // Teardown: Clear drops source toasts, the snapshot, and every stamp.
        private static readonly List<Toast> toasts = new List<Toast>();
        private static int toastRevision;
        private static int layoutToastRevision = -1;
        private static float layoutMaxWidth = -1f;
        private static int layoutLanguageRevision = -1;
        private static ToastLayoutSnapshot publishedSnapshot =
            ToastLayoutSnapshot.Empty;

        internal static void Clear()
        {
            toasts.Clear();
            toastRevision = 0;
            layoutToastRevision = -1;
            layoutMaxWidth = -1f;
            layoutLanguageRevision = -1;
            publishedSnapshot = ToastLayoutSnapshot.Empty;
        }

        public static void Show(string text, MessageTypeDef fallbackType)
        {
            if (text.NullOrEmpty()) return;
            if (Find.WindowStack?.IsOpen<MainTabWindow_WorkRoles>() == true)
            {
                toasts.Add(new Toast(text,
                    Time.realtimeSinceStartup + DurationSeconds));
                toastRevision++;
            }
            else
            {
                Messages.Message(text, fallbackType, historical: false);
            }
        }

        /// Expires local presentation state outside OnGUI. Remaining order is
        /// preserved because removal walks backward through the private list.
        internal static void Update()
        {
            if (toasts.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            bool changed = false;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                if (now < toasts[i].Expiry) continue;
                toasts.RemoveAt(i);
                changed = true;
            }
            if (changed) toastRevision++;
        }

        /// Rebuilds measured render data only after a declared dependency
        /// changes. Called on the window's Repaint refresh boundary before
        /// Draw consumes the published snapshot.
        internal static void RefreshLayout(float availableWidth)
        {
            float maxWidth = Mathf.Min(MaxTextWidth, availableWidth - 56f);
            int languageRevision = LanguageChangeCoordinator.Revision;
            if (layoutToastRevision == toastRevision
                && layoutMaxWidth == maxWidth
                && layoutLanguageRevision == languageRevision)
                return;

            if (toasts.Count == 0)
            {
                publishedSnapshot = ToastLayoutSnapshot.Empty;
                layoutToastRevision = toastRevision;
                layoutMaxWidth = maxWidth;
                layoutLanguageRevision = languageRevision;
                return;
            }

            var rows = new ToastLayoutRow[toasts.Count];
            GameFont oldFont = Text.Font;
            bool oldWordWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                for (int i = 0; i < toasts.Count; i++)
                {
                    Toast toast = toasts[i];
                    float textWidth = Mathf.Min(
                        Text.CalcSize(toast.Text).x, maxWidth);
                    float textHeight = Text.CalcHeight(
                        toast.Text, textWidth + 1f);
                    rows[i] = new ToastLayoutRow(
                        toast.Text, textWidth, textHeight);
                }
            }
            finally
            {
                Text.Font = oldFont;
                Text.WordWrap = oldWordWrap;
            }

            if (!publishedSnapshot.ContentEquals(rows))
                publishedSnapshot = new ToastLayoutSnapshot(rows);
            layoutToastRevision = toastRevision;
            layoutMaxWidth = maxWidth;
            layoutLanguageRevision = languageRevision;
        }

        /// Called last in the window's draw so the already-published toast rows
        /// paint on top. This method performs no expiry, measurement, or layout
        /// mutation.
        public static void Draw(Rect inRect)
        {
            ToastLayoutSnapshot published = publishedSnapshot;
            if (published.Count == 0
                || Event.current.type != EventType.Repaint)
                return;

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWordWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.WordWrap = true;
                GUI.color = Color.white;
                float y = inRect.y + 4f;
                for (int i = 0; i < published.Count; i++)
                {
                    ToastLayoutRow toast = published.RowAt(i);
                    var panel = new Rect(
                        inRect.x
                            + (inRect.width - toast.TextWidth - 24f) / 2f,
                        y,
                        toast.TextWidth + 24f,
                        toast.TextHeight + 12f);
                    Widgets.DrawBoxSolidWithOutline(
                        panel, WrStyle.PanelBackground, WrStyle.PanelOutline);
                    Widgets.Label(new Rect(panel.x + 12f, panel.y + 6f,
                        toast.TextWidth, toast.TextHeight), toast.Text);
                    y += panel.height + 4f;
                }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWordWrap;
                GUI.color = oldColor;
            }
        }
    }
}
