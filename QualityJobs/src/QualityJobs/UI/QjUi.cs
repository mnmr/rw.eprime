using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// Shared drawing idioms for QualityJobs panels.
    public static class QjUi
    {
        /// Warning tint for status lines (stalled pipeline and similar).
        public static readonly Color WarnColor = new Color(1f, 0.62f, 0.25f);

        /// Compact row ADVANCE for the odds/status data panels: rows step 20f
        /// but every label must draw in a TightRowDrawH rect — a 20f rect clips
        /// small-font descenders (g, y, p).
        public const float TightRowH = 20f;

        /// Full small-font line height for the DRAW rect of compact rows.
        /// Ascenders never reach the row top, so the 2px overlap into the next
        /// row is invisible while descenders stay unclipped.
        public const float TightRowDrawH = 22f;

        /// <summary>Status names line: up to three best-first names, then "+N".
        /// Refresh path only (never per frame), so plain concat is fine.</summary>
        public static string NamesLine(List<Pawn> pawns)
        {
            string names = pawns[0].LabelShort;
            int shown = pawns.Count < 3 ? pawns.Count : 3;
            for (int i = 1; i < shown; i++) names = names + ", " + pawns[i].LabelShort;
            if (pawns.Count > shown) names = names + " +" + (pawns.Count - shown);
            return names;
        }

        // Verified against Verse.Widgets.DrawLineHorizontal(float x, float y, float length)
        // signature confirmed in Decompiled\Verse\Listing.cs line 80:
        //   Widgets.DrawLineHorizontal(curX, y, ColumnWidth);

        /// Section mini-header: small dimmed label with a faint rule beneath
        /// (mirrors the WorkRoles Options-panel header style). The rule sits
        /// directly under the label glyphs (y + 21f; glyphs end around y + 19f).
        /// Returns the y below the header block (rule + 6f). Saves/restores
        /// GUI.color.
        /// IMPORTANT: coordinates must be group-relative when called inside a
        /// GUI group (i.e. after Listing.Begin or Widgets.BeginGroup).
        public static float MiniHeader(float x, float y, float width, string label)
        {
            Color prev = GUI.color;
            var labelRect = new Rect(x, y, width, 22f);
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(labelRect, label);
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            Widgets.DrawLineHorizontal(x, y + 21f, width);
            GUI.color = prev;
            return y + 27f;
        }
    }
}
