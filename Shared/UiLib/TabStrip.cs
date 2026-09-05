using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimShared.UiLib
{
    /// A window's tab strip: vanilla TabDrawer/TabRecord.Draw geometry with
    /// one rendering fix. Vanilla draws each tab as three atlas pieces (left
    /// cap, stretched middle, right cap) whose middle-piece pixel snap
    /// assumes the GUI origin sits on an integer framebuffer pixel; a window
    /// at a fractional physical origin (any position where windowRect.x times
    /// the UI scale is not whole) defeats the snap and a one-pixel sliver of
    /// background shows between cap and middle, the "black stripe" on the
    /// strip at fractional UI scales. Here the middle piece is drawn FIRST
    /// and extended under each cap's fully opaque inner edge, so the caps
    /// paint over the boundary and no seam can open at any sub-pixel phase.
    ///
    /// Stacking also deliberately differs from vanilla: vanilla paints
    /// strictly left-to-right, so every tab overlaps its left neighbor no
    /// matter what is selected. Here z-order falls with distance from the
    /// selected tab, like physical file tabs: each neighbor cascades under
    /// the tab nearer the selection. Allocation-free; hover, click, and the
    /// bottom border on unselected tabs otherwise mirror vanilla.
    ///
    /// The atlas is the vanilla "UI/Widgets/TabAtlas" texture; the caller
    /// resolves it from its own [StaticConstructorOnStartup] holder and
    /// passes it in unchanged.
    public static class TabStrip
    {
        public const float TabHeight = 32f;
        private const float TabOverlap = 10f;
        private const float MaxTabWidth = 200f;
        private const float TabEndWidth = 30f;

        /// How far the middle piece extends under each cap. The cap art is
        /// fully opaque this close to its inner edge, so the overlap is
        /// invisible while covering the worst-case sub-pixel gap.
        private const float CapOverlap = 2f;

        /// Width of one tab for a strip of the given content width.
        public static float TabWidth(float contentWidth, int tabCount)
        {
            if (tabCount <= 0) return 0f;
            float tabWidth = (contentWidth + (tabCount - 1) * TabOverlap)
                / tabCount;
            return tabWidth > MaxTabWidth ? MaxTabWidth : tabWidth;
        }

        /// Vanilla leaves the menu-section top border visible under the
        /// active tab. Overpaints its span with the section fill so the
        /// active tab connects seamlessly to the content below. Inset 2px per
        /// side so the rounded tab corners keep their border pixel.
        public static void DrawActiveTabSeam(Rect content, int activeIndex,
            int tabCount)
        {
            if (activeIndex < 0 || activeIndex >= tabCount) return;
            float tabWidth = TabWidth(content.width, tabCount);
            float activeTabX = content.x + activeIndex * (tabWidth - TabOverlap);
            Widgets.DrawBoxSolid(
                new Rect(activeTabX + 2f, content.y, tabWidth - 4f, 2f),
                Widgets.MenuSectionBGFillColor);
        }

        public static void Draw(Rect baseRect, List<TabRecord> tabs,
            Texture2D atlas)
        {
            if (tabs.Count == 0) return;
            float tabWidth = TabWidth(baseRect.width, tabs.Count);

            var strip = new Rect(baseRect);
            strip.y -= TabHeight;
            strip.height = 9999f;
            Widgets.BeginGroup(strip);
            try
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Small;

                int selected = -1;
                for (int i = 0; i < tabs.Count; i++)
                    if (tabs[i].Selected)
                    {
                        selected = i;
                        break;
                    }

                // The stack anchor: the selected tab, or the first tab when
                // nothing is selected (a physical stack still fans from its
                // front-most tab).
                int anchor = selected >= 0 ? selected : 0;
                int maxDist = anchor > tabs.Count - 1 - anchor
                    ? anchor : tabs.Count - 1 - anchor;

                // Mouse pass in top-to-bottom stack order (anchor first, then
                // outward by distance) so overlapped edges resolve to the
                // visually topmost tab; ButtonInvisible consumes the click.
                TabRecord? clicked = null;
                if (HandleMouse(TabRect(anchor, tabWidth)))
                    clicked = tabs[anchor];
                for (int d = 1; d <= maxDist; d++)
                {
                    int left = anchor - d;
                    if (left >= 0 && HandleMouse(TabRect(left, tabWidth))
                        && clicked == null)
                        clicked = tabs[left];
                    int right = anchor + d;
                    if (right < tabs.Count
                        && HandleMouse(TabRect(right, tabWidth))
                        && clicked == null)
                        clicked = tabs[right];
                }

                // Draw pass in bottom-to-top stack order: outermost tabs
                // first, the anchor last so it overlaps its neighbors and
                // each tab overlaps the next one farther from the anchor.
                for (int d = maxDist; d >= 1; d--)
                {
                    int left = anchor - d;
                    if (left >= 0)
                        DrawTab(TabRect(left, tabWidth), tabs[left], -1, atlas);
                    int right = anchor + d;
                    if (right < tabs.Count)
                        DrawTab(TabRect(right, tabWidth), tabs[right], 1, atlas);
                }
                DrawTab(TabRect(anchor, tabWidth), tabs[anchor], 0, atlas);

                if (clicked != null && !clicked.Selected)
                {
                    SoundDefOf.RowTabSelect.PlayOneShotOnCamera();
                    clicked.clickedAction?.Invoke();
                }
            }
            finally
            {
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.EndGroup();
            }
        }

        private static Rect TabRect(int index, float tabWidth) =>
            new Rect(index * (tabWidth - TabOverlap), 1f, tabWidth, TabHeight);

        private static bool HandleMouse(Rect rect)
        {
            MouseoverSounds.DoRegion(rect, SoundDefOf.Mouseover_Tab);
            return Widgets.ButtonInvisible(rect);
        }

        /// side: -1 for tabs left of the stack anchor (right edge sits under
        /// the neighbor nearer the anchor), +1 for tabs right of it (left
        /// edge covered instead), 0 for the anchor itself (fully visible).
        private static void DrawTab(Rect rect, TabRecord tab, int side,
            Texture2D atlas)
        {
            var capLeft = new Rect(rect.x, rect.y, TabEndWidth, rect.height);
            var capRight = new Rect(rect.xMax - TabEndWidth, rect.y,
                TabEndWidth, rect.height);
            var middle = new Rect(rect.x + TabEndWidth - CapOverlap, rect.y,
                rect.width - 2f * (TabEndWidth - CapOverlap), rect.height);
            middle.xMin = UIScaling.AdjustCoordToUIScalingFloor(middle.xMin);
            middle.xMax = UIScaling.AdjustCoordToUIScalingCeil(middle.xMax);
            Rect middleUv = new Rect(30f, 0f, 4f, atlas.height)
                .ToUVRect(new Vector2(atlas.width, atlas.height));

            Widgets.DrawTexturePart(middle, middleUv, atlas);
            Widgets.DrawTexturePart(capLeft, new Rect(0f, 0f, 15f / 32f, 1f), atlas);
            Widgets.DrawTexturePart(capRight, new Rect(17f / 32f, 0f, 15f / 32f, 1f), atlas);

            // Label with the vanilla hover treatment (hover only tints; the
            // vanilla offset never applied to the drawn label either). The
            // hover region is the tab's visible surface: the overlap edge a
            // higher-stacked neighbor covers does not tint this tab.
            GUI.color = tab.labelColor ?? Color.white;
            var hoverRect = rect;
            if (side < 0) hoverRect.width -= TabOverlap;
            else if (side > 0) hoverRect.xMin += TabOverlap;
            if (Mouse.IsOver(hoverRect)) GUI.color = Color.yellow;
            Text.WordWrap = false;
            Widgets.Label(rect, tab.label);
            Text.WordWrap = true;
            GUI.color = Color.white;

            if (!tab.Selected)
            {
                var bottom = new Rect(rect.x, rect.y + rect.height - 1f,
                    rect.width, 1f);
                Widgets.DrawTexturePart(bottom,
                    new Rect(0.5f, 0.01f, 0.01f, 0.01f), atlas);
            }
        }
    }
}
