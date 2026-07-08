using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Preview of pending role changes, grouped per colonist and rendered with role
    /// chips; nothing happens unless the user hits Apply, and individual colonists
    /// can be deselected (plus a select-all toggle). The game keeps running
    /// (MP-friendly): at apply time the plan is recomputed, and if the colony changed
    /// in the meantime the request is dropped with a notification instead of
    /// applying a stale plan.
    public class Dialog_ChangesPreview : Window
    {
        public enum ChipState
        {
            Kept,     // stays assigned: dimmed like an already-assigned chip
            Added,    // new: normal chip
            Removed   // dropped: normal chip struck corner-to-corner
        }

        /// One preview line: chips with per-chip states and reason tooltips.
        public class Line
        {
            public List<(Role role, ChipState state, string tip)> chips =
                new List<(Role role, ChipState state, string tip)>();
        }

        public class PawnPreview
        {
            public Pawn pawn;
            public List<Line> lines = new List<Line>();
            public bool included = true;
        }

        private const float TitleH = 38f;
        private const float SelectRowH = 26f;
        private const float PawnRowH = 24f;
        private const float LineGap = 4f;
        private const float GroupGap = 8f;
        private const float ChipGap = 4f;
        private const float ButtonW = 120f;
        private const float ButtonH = 32f;

        private readonly string title;
        private readonly List<PawnPreview> entries;
        private readonly Action<HashSet<Pawn>> onApply;
        private readonly Func<List<PawnPreview>> rebuild;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(560f, 620f);

        public Dialog_ChangesPreview(string title, List<PawnPreview> entries,
            Action<HashSet<Pawn>> onApply, Func<List<PawnPreview>> rebuild)
        {
            this.title = title;
            this.entries = entries;
            this.onApply = onApply;
            this.rebuild = rebuild;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        private static bool SamePlan(List<PawnPreview> a, List<PawnPreview> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].pawn != b[i].pawn || a[i].lines.Count != b[i].lines.Count) return false;
                for (int j = 0; j < a[i].lines.Count; j++)
                {
                    var la = a[i].lines[j];
                    var lb = b[i].lines[j];
                    if (la.chips.Count != lb.chips.Count) return false;
                    for (int k = 0; k < la.chips.Count; k++)
                        if (la.chips[k].role.id != lb.chips[k].role.id
                            || la.chips[k].state != lb.chips[k].state) return false;
                }
            }
            return true;
        }

        private static void DrawStateChip(Rect rect, Role role, ChipState state, string tip, bool draw)
        {
            if (!draw) return;
            var style = state == ChipState.Kept ? ChipStyle.Subtle : ChipStyle.Normal;
            RoleChipUI.Draw(rect, role, style, showRemove: false, dragSource: null, onClick: null,
                interactive: false);
            if (state == ChipState.Removed)
                RoleChipUI.DrawRemovedOutline(rect);
            if (tip != null && Mouse.IsOver(rect))
                TooltipHandler.TipRegion(rect, tip);
        }

        /// Draws (or, with draw=false, measures) one line of wrapped chips.
        /// Returns the height consumed.
        private static float DrawLine(Line line, float x0, float y, float width, bool draw)
        {
            float xMax = x0 + width;
            float x = x0;
            float curY = y;

            void Wrap(float needed)
            {
                if (x + needed > xMax && x > x0)
                {
                    x = x0;
                    curY += RoleChipUI.Height + LineGap;
                }
            }

            foreach (var (role, state, tip) in line.chips)
            {
                float w = RoleChipUI.WidthFor(role, showRemove: false);
                Wrap(w);
                DrawStateChip(new Rect(x, curY, w, RoleChipUI.Height), role, state, tip, draw);
                x += w + ChipGap;
            }

            return curY + RoleChipUI.Height - y;
        }

        private static float DrawEntries(List<PawnPreview> entries, float width, bool draw)
        {
            float y = 0f;
            foreach (var entry in entries)
            {
                float top = y;
                if (draw)
                {
                    Widgets.Checkbox(new Vector2(0f, y), ref entry.included, 20f);
                    Widgets.Label(new Rect(26f, y, width - 26f, PawnRowH), entry.pawn.LabelShortCap);
                }
                y += PawnRowH;
                foreach (var line in entry.lines)
                    y += DrawLine(line, 26f, y, width - 26f, draw) + LineGap;
                if (draw && !entry.included)
                    Widgets.DrawBoxSolid(new Rect(24f, top, width - 24f, y - top),
                        new Color(0f, 0f, 0f, 0.55f));
                y += GroupGap;
            }
            return y;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), title);
            Text.Font = GameFont.Small;

            float listTop = inRect.y + TitleH;
            if (entries.Count > 0)
            {
                // Select-all toggle above the list.
                bool all = entries.All(e => e.included);
                bool toggled = all;
                Widgets.CheckboxLabeled(new Rect(inRect.x, listTop, 160f, 24f),
                    "WR_SelectAll".Translate(), ref toggled);
                if (toggled != all)
                    foreach (var entry in entries) entry.included = toggled;
                listTop += SelectRowH;
            }

            var listRect = new Rect(inRect.x, listTop, inRect.width, inRect.yMax - listTop - ButtonH - 8f);
            float rowW = listRect.width - 16f;
            float contentH = entries.Count == 0 ? PawnRowH : DrawEntries(entries, rowW, draw: false);

            Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, rowW, contentH));
            if (entries.Count == 0)
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(0f, 0f, rowW, PawnRowH), "WR_PreviewNoChanges".Translate());
                GUI.color = Color.white;
            }
            else
            {
                DrawEntries(entries, rowW, draw: true);
            }
            Widgets.EndScrollView();

            float btnY = inRect.yMax - ButtonH;
            var applyRect = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, ButtonH);
            var cancelRect = new Rect(applyRect.x - 8f - ButtonW, btnY, ButtonW, ButtonH);
            if (Widgets.ButtonText(cancelRect, "WR_Cancel".Translate()))
                Close();
            bool canApply = entries.Any(e => e.included);
            if (Widgets.ButtonText(applyRect, "WR_Apply".Translate(), active: canApply) && canApply)
            {
                if (SamePlan(entries, rebuild()))
                    onApply?.Invoke(entries.Where(e => e.included).Select(e => e.pawn).ToHashSet());
                else
                    Messages.Message("WR_PreviewStale".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                Close();
            }
        }
    }
}
