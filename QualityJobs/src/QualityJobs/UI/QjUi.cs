using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// Shared drawing idioms for QualityJobs panels.
    public static class QjUi
    {
        // Verified against Verse.Widgets.DrawLineHorizontal(float x, float y, float length)
        // signature confirmed in Decompiled\Verse\Listing.cs line 80:
        //   Widgets.DrawLineHorizontal(curX, y, ColumnWidth);

        /// Section mini-header: small dimmed label with a faint rule beneath
        /// (mirrors the WorkRoles Options-panel header style). Returns the y
        /// below the header block. Saves/restores GUI.color.
        /// IMPORTANT: coordinates must be group-relative when called inside a
        /// GUI group (i.e. after Listing.Begin or Widgets.BeginGroup).
        public static float MiniHeader(float x, float y, float width, string label)
        {
            Color prev = GUI.color;
            var labelRect = new Rect(x, y, width, 22f);
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(labelRect, label);
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            Widgets.DrawLineHorizontal(x, y + 24f, width);
            GUI.color = prev;
            return y + 30f;
        }
    }
}
