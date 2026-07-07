using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Preview of pending role changes, grouped per colonist and rendered with role
    /// chips; nothing happens unless the user hits Apply. The game keeps running
    /// (MP-friendly): at apply time the plan is recomputed, and if the colony changed
    /// in the meantime the request is dropped with a notification instead of
    /// applying a stale plan.
    public class Dialog_ChangesPreview : Window
    {
        /// One preview line: optional leading label, chips in one style, and an
        /// optional "→ result chip" tail (used by combine steps).
        public class Line
        {
            public string label;
            public List<Role> roles = new List<Role>();
            public ChipStyle style = ChipStyle.Normal;
            public Role arrowResult;
        }

        public class PawnPreview
        {
            public Pawn pawn;
            public List<Line> lines = new List<Line>();
        }

        private const float TitleH = 38f;
        private const float PawnRowH = 24f;
        private const float LineGap = 4f;
        private const float GroupGap = 8f;
        private const float LabelW = 76f;
        private const float ChipGap = 4f;
        private const float ArrowW = 24f;
        private const float ButtonW = 120f;
        private const float ButtonH = 32f;

        private readonly string title;
        private readonly List<PawnPreview> entries;
        private readonly Action onApply;
        private readonly Func<List<PawnPreview>> rebuild;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(560f, 620f);

        public Dialog_ChangesPreview(string title, List<PawnPreview> entries, Action onApply,
            Func<List<PawnPreview>> rebuild)
        {
            this.title = title;
            this.entries = entries;
            this.onApply = onApply;
            this.rebuild = rebuild;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
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
                    if (la.label != lb.label || la.style != lb.style || la.roles.Count != lb.roles.Count) return false;
                    if ((la.arrowResult?.id ?? -1) != (lb.arrowResult?.id ?? -1)) return false;
                    for (int k = 0; k < la.roles.Count; k++)
                        if (la.roles[k].id != lb.roles[k].id) return false;
                }
            }
            return true;
        }

        /// Draws (or, with draw=false, measures) one line of wrapped chips.
        /// Returns the height consumed.
        private static float DrawLine(Line line, float x0, float y, float width, bool draw)
        {
            if (draw && !line.label.NullOrEmpty())
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(x0, y, LabelW - 4f, RoleChipUI.Height), line.label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            float lineStartX = x0 + LabelW;
            float xMax = x0 + width;
            float x = lineStartX;
            float curY = y;

            void Wrap(float needed)
            {
                if (x + needed > xMax && x > lineStartX)
                {
                    x = lineStartX;
                    curY += RoleChipUI.Height + LineGap;
                }
            }

            foreach (var role in line.roles)
            {
                float w = RoleChipUI.WidthFor(role, showRemove: false);
                Wrap(w);
                if (draw)
                    RoleChipUI.Draw(new Rect(x, curY, w, RoleChipUI.Height), role, line.style,
                        showRemove: false, dragSource: null, onClick: null, interactive: false);
                x += w + ChipGap;
            }

            if (line.arrowResult != null)
            {
                float resultW = RoleChipUI.WidthFor(line.arrowResult, showRemove: false);
                Wrap(ArrowW + resultW);
                if (draw)
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(x, curY, ArrowW, RoleChipUI.Height), "→");
                    Text.Anchor = TextAnchor.UpperLeft;
                    RoleChipUI.Draw(new Rect(x + ArrowW, curY, resultW, RoleChipUI.Height), line.arrowResult,
                        ChipStyle.Normal, showRemove: false, dragSource: null, onClick: null, interactive: false);
                }
                x += ArrowW + resultW + ChipGap;
            }

            return curY + RoleChipUI.Height - y;
        }

        private static float DrawEntries(List<PawnPreview> entries, float width, bool draw)
        {
            float y = 0f;
            foreach (var entry in entries)
            {
                if (draw)
                    Widgets.Label(new Rect(0f, y, width, PawnRowH), entry.pawn.LabelShortCap);
                y += PawnRowH;
                foreach (var line in entry.lines)
                    y += DrawLine(line, 12f, y, width - 12f, draw) + LineGap;
                y += GroupGap;
            }
            return y;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), title);
            Text.Font = GameFont.Small;

            var listRect = new Rect(inRect.x, inRect.y + TitleH, inRect.width, inRect.height - TitleH - ButtonH - 8f);
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
            bool canApply = entries.Count > 0;
            if (Widgets.ButtonText(applyRect, "WR_Apply".Translate(), active: canApply) && canApply)
            {
                if (SamePlan(entries, rebuild()))
                    onApply?.Invoke();
                else
                    Messages.Message("WR_PreviewStale".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                Close();
            }
        }
    }
}
