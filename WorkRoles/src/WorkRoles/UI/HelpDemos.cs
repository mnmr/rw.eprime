using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// <summary>
    /// An embedded help-page demo: a fixed-size, self-animating vignette.
    /// Draw is called on every window pass with the reserved rect; demos are
    /// stateless presentations driven by the realtime clock, mutate nothing,
    /// and allocate nothing per frame.
    /// </summary>
    internal interface IHelpDemo
    {
        Vector2 Size { get; }
        void Draw(Rect rect);
    }

    /// <summary>
    /// Registry the help pipeline resolves "@demo:name" blocks against.
    /// </summary>
    // Owner: process (fixed set, no per-save data). Key: demo name from the
    // markdown source. Value: stateless demo instances; their translated
    // labels re-resolve behind the language revision inside each demo.
    // Dependencies: none at the registry level. Refresh: none (immutable
    // set). Teardown: none required; demos own no resources.
    internal static class HelpDemos
    {
        private static readonly Dictionary<string, IHelpDemo> demos =
            new Dictionary<string, IHelpDemo>
            {
                { "chip-drag", new ChipDragDemo() },
            };

        internal static bool TryGetSize(
            string name, out float width, out float height)
        {
            if (demos.TryGetValue(name, out IHelpDemo? demo))
            {
                width = demo.Size.x;
                height = demo.Size.y;
                return true;
            }
            width = 0f;
            height = 0f;
            return false;
        }

        internal static IHelpDemo? Get(string name) =>
            demos.TryGetValue(name, out IHelpDemo? demo) ? demo : null;
    }

    /// <summary>
    /// Looping vignette of the core interaction: a role chip is picked up,
    /// dragged in front of the first chip, and dropped, reordering the row.
    /// </summary>
    internal sealed class ChipDragDemo : IHelpDemo
    {
        private const float ChipWidth = 110f;
        private const float ChipHeight = 26f;
        private const float ChipGap = 8f;
        private const float Period = 5f;

        private static readonly Color[] ChipColors =
        {
            new Color(0.55f, 0.15f, 0.15f),   // Doctor red
            new Color(0.30f, 0.45f, 0.16f),   // Farmer green
            new Color(0.32f, 0.36f, 0.42f),   // Hauler slate
        };
        private static readonly Color ChipOutline =
            new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color EmptySlotOutline =
            new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color InsertMarker =
            new Color(1f, 0.85f, 0.45f, 0.9f);
        private static readonly Color CursorColor =
            new Color(1f, 1f, 1f, 0.9f);

        // Owner: this demo instance. Key: LanguageChangeCoordinator.Revision.
        // Value: the three translated chip labels. Dependencies: language
        // only. Refresh: immediate on observed revision change. Teardown:
        // none (strings follow the process).
        private readonly string[] labels = new string[3];
        private int labelStamp = -1;

        public Vector2 Size => new Vector2(
            3f * ChipWidth + 2f * ChipGap + 16f, 52f);

        private void ObserveLanguage()
        {
            int revision = LanguageChangeCoordinator.Revision;
            if (labelStamp == revision) return;
            labels[0] = "WR_HelpDemoChipA".Translate().ToString();
            labels[1] = "WR_HelpDemoChipB".Translate().ToString();
            labels[2] = "WR_HelpDemoChipC".Translate().ToString();
            labelStamp = revision;
        }

        public void Draw(Rect rect)
        {
            ObserveLanguage();
            float phase = Time.realtimeSinceStartup % Period / Period;

            float rowY = rect.y + 14f;
            float slot0 = rect.x + 8f;
            float SlotX(int index) => slot0 + index * (ChipWidth + ChipGap);

            // Timeline: approach, grab, drag right-to-left, drop, rest.
            const float GrabAt = 0.15f;
            const float DropStart = 0.55f;
            const float DropEnd = 0.65f;
            bool dragging = phase >= GrabAt && phase < DropStart;
            bool dropped = phase >= DropStart;
            float settle = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(DropStart, DropEnd, phase));

            // Cursor travels from below the row onto the last chip, then to
            // the insertion point in front of the first chip.
            Vector2 grabPoint = new Vector2(
                SlotX(2) + ChipWidth / 2f, rowY + ChipHeight / 2f);
            Vector2 insertPoint = new Vector2(
                slot0 - ChipGap / 2f, rowY + ChipHeight / 2f);
            Vector2 restPoint = new Vector2(
                rect.xMax - 24f, rect.yMax - 6f);
            Vector2 cursor;
            if (phase < GrabAt)
                cursor = Vector2.Lerp(restPoint, grabPoint,
                    Mathf.SmoothStep(0f, 1f, phase / GrabAt));
            else if (dragging)
                cursor = Vector2.Lerp(grabPoint, insertPoint,
                    Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(GrabAt, DropStart, phase)));
            else
                cursor = insertPoint;

            // Chip order: A B C at rest, C A B once the drop settles. During
            // the settle the remaining chips slide one slot to the right.
            if (!dropped)
            {
                DrawChip(SlotX(0), rowY, 0, 1f);
                DrawChip(SlotX(1), rowY, 1, 1f);
                if (dragging)
                {
                    DrawEmptySlot(SlotX(2), rowY);
                    // The dragged chip rides the cursor as a ghost, and the
                    // insert marker tracks the gap nearest the cursor: it
                    // starts between the second and third chips and hops
                    // left as the drag crosses each boundary.
                    DrawChip(cursor.x - ChipWidth / 2f, rowY - 6f, 2, 0.75f);
                    float nearestGap = slot0 - ChipGap / 2f;
                    float bestDistance = Mathf.Abs(cursor.x - nearestGap);
                    for (int gap = 1; gap <= 2; gap++)
                    {
                        float gapX = SlotX(gap) - ChipGap / 2f;
                        float distance = Mathf.Abs(cursor.x - gapX);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            nearestGap = gapX;
                        }
                    }
                    DrawInsertMarker(nearestGap, rowY);
                }
                else
                {
                    DrawChip(SlotX(2), rowY, 2, 1f);
                }
            }
            else
            {
                DrawChip(
                    Mathf.Lerp(insertPoint.x - ChipWidth / 2f,
                        SlotX(0), settle),
                    Mathf.Lerp(rowY - 6f, rowY, settle), 2, 1f);
                DrawChip(Mathf.Lerp(SlotX(0), SlotX(1), settle), rowY, 0, 1f);
                DrawChip(Mathf.Lerp(SlotX(1), SlotX(2), settle), rowY, 1, 1f);
            }

            if (!dropped)
            {
                GUI.color = CursorColor;
                GUI.DrawTexture(new Rect(cursor.x - 4f, cursor.y - 4f,
                    8f, 8f), WorkRolesTex.Circle);
                GUI.color = Color.white;
            }
        }

        private void DrawChip(float x, float y, int index, float alpha)
        {
            var chip = new Rect(x, y, ChipWidth, ChipHeight);
            Color fill = ChipColors[index];
            fill.a = alpha;
            Color outline = ChipOutline;
            outline.a *= alpha;
            Widgets.DrawBoxSolidWithOutline(chip, fill, outline);
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                Widgets.Label(chip, labels[index]);
            }
            finally
            {
                GUI.color = oldColor;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
            }
        }

        private static void DrawEmptySlot(float x, float y)
            => Widgets.DrawBoxSolidWithOutline(
                new Rect(x, y, ChipWidth, ChipHeight),
                new Color(0f, 0f, 0f, 0.25f), EmptySlotOutline);

        private static void DrawInsertMarker(float x, float y)
            => Widgets.DrawBoxSolid(
                new Rect(x - 1f, y - 3f, 2f, ChipHeight + 6f), InsertMarker);
    }
}
