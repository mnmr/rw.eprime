using System.Collections.Generic;
using EPrimeReadouts.Core;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Shared preview listing renderer used by both the export and import preview
    /// dialogs. Builds a flat list of display lines once (cached by list identity
    /// and store version), then draws only the rows inside the scroll viewport.
    internal static class ReadoutsPreviewUI
    {
        private const float RowH         = 22f;
        private const float HeaderH      = 26f;
        private const float BottomPad    = 8f;

        // Cache keys — rebuilt when pools/groups lists change identity or content.
        private static List<ResourcePool> s_lastPools;
        private static List<ReadoutGroup> s_lastGroups;
        private static string[] s_lines;   // null = header, non-null = row label
        private static bool[] s_isHeader;
        private static float s_contentH;

        /// Builds (cached) and draws the listing inside <paramref name="outRect"/>.
        /// <paramref name="scroll"/> is the caller's persistent scroll position.
        internal static void DrawListing(
            Rect outRect,
            List<ResourcePool> pools,
            List<ReadoutGroup> groups,
            ref Vector2 scroll)
        {
            EnsureLines(pools, groups);

            var viewRect = new Rect(0f, 0f, outRect.width - GenUI.ScrollBarWidth, s_contentH);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);

            float y = 0f;
            for (int i = 0; i < s_lines.Length; i++)
            {
                float h = s_isHeader[i] ? HeaderH : RowH;
                // Viewport cull: skip rows outside the visible strip.
                if (y + h > scroll.y - RowH && y < scroll.y + outRect.height + RowH)
                {
                    if (s_isHeader[i])
                        DrawMiniHeader(s_lines[i], 0f, y, viewRect.width);
                    else
                        DrawRow(s_lines[i], 0f, y, viewRect.width);
                }
                y += h;
            }

            Widgets.EndScrollView();
        }

        private static void EnsureLines(List<ResourcePool> pools, List<ReadoutGroup> groups)
        {
            if (ReferenceEquals(s_lastPools, pools) && ReferenceEquals(s_lastGroups, groups)
                && s_lines != null)
                return;

            s_lastPools  = pools;
            s_lastGroups = groups;

            var lines    = new List<string>();
            var isHeader = new List<bool>();

            int poolCount  = pools  != null ? pools.Count  : 0;
            int groupCount = groups != null ? groups.Count : 0;

            // ── Pools section ────────────────────────────────────────────────
            lines.Add("EPR.PreviewPoolsHeader".Translate());
            isHeader.Add(true);

            if (poolCount == 0)
            {
                lines.Add("EPR.PreviewNone".Translate());
                isHeader.Add(false);
            }
            else
            {
                for (int i = 0; i < pools.Count; i++)
                {
                    var pool = pools[i];
                    int members = pool.Members != null ? pool.Members.Count : 0;
                    lines.Add("EPR.PreviewPoolRow".Translate(pool.Name, members));
                    isHeader.Add(false);
                }
            }

            // ── Groups section ───────────────────────────────────────────────
            lines.Add("EPR.PreviewGroupsHeader".Translate());
            isHeader.Add(true);

            if (groupCount == 0)
            {
                lines.Add("EPR.PreviewNone".Translate());
                isHeader.Add(false);
            }
            else
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    int tiers = group.TierCount;
                    int slots = 0;
                    if (group.Tiers != null)
                        foreach (var tier in group.Tiers)
                            slots += tier.Count;
                    lines.Add("EPR.PreviewGroupRow".Translate(group.Name, tiers, slots));
                    isHeader.Add(false);
                }
            }

            s_lines    = lines.ToArray();
            s_isHeader = isHeader.ToArray();

            float total = 0f;
            for (int i = 0; i < s_isHeader.Length; i++)
                total += s_isHeader[i] ? HeaderH : RowH;
            s_contentH = total + BottomPad;
        }

        private static void DrawMiniHeader(string text, float x, float y, float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            Text.Font   = GameFont.Small;
            GUI.color   = EprStyle.HeaderText;
            Widgets.Label(new Rect(x, y, width, HeaderH - 4f), text);
            GUI.color   = EprStyle.HeaderRule;
            WrText.LineHorizontal(x, y + HeaderH - 4f, width);
            GUI.color   = Color.white;
        }

        private static void DrawRow(string text, float x, float y, float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            Text.Font   = GameFont.Tiny;
            GUI.color   = EprStyle.CaptionText;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x + 8f, y, width - 8f, RowH), text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color   = Color.white;
            Text.Font   = GameFont.Small;
        }

        /// Invalidates the cache so the next DrawListing call rebuilds.
        internal static void Invalidate()
        {
            s_lines    = null;
            s_lastPools  = null;
            s_lastGroups = null;
        }
    }
}
