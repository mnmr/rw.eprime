using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Preview of pending role changes, grouped per colonist; nothing happens
    /// unless the user hits Apply. The game keeps running (MP-friendly): at apply
    /// time the plan is recomputed, and if the colony changed in the meantime the
    /// request is dropped with a notification instead of applying a stale plan.
    public class Dialog_ChangesPreview : Window
    {
        public struct PawnChanges
        {
            public Pawn pawn;
            public List<string> lines;
        }

        private const float TitleH = 38f;
        private const float PawnRowH = 24f;
        private const float LineH = 22f;
        private const float GroupGap = 6f;
        private const float ButtonW = 120f;
        private const float ButtonH = 32f;

        private readonly string title;
        private readonly List<PawnChanges> changes;
        private readonly Action onApply;
        private readonly Func<List<PawnChanges>> rebuild;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(520f, 600f);

        public Dialog_ChangesPreview(string title, List<PawnChanges> changes, Action onApply,
            Func<List<PawnChanges>> rebuild)
        {
            this.title = title;
            this.changes = changes;
            this.onApply = onApply;
            this.rebuild = rebuild;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        private static bool SamePlan(List<PawnChanges> a, List<PawnChanges> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].pawn != b[i].pawn || a[i].lines.Count != b[i].lines.Count) return false;
                for (int j = 0; j < a[i].lines.Count; j++)
                    if (a[i].lines[j] != b[i].lines[j]) return false;
            }
            return true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), title);
            Text.Font = GameFont.Small;

            var listRect = new Rect(inRect.x, inRect.y + TitleH, inRect.width, inRect.height - TitleH - ButtonH - 8f);

            float contentH = 0f;
            foreach (var c in changes) contentH += PawnRowH + c.lines.Count * LineH + GroupGap;
            if (changes.Count == 0) contentH = PawnRowH;
            float rowW = listRect.width - 16f;

            Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, rowW, contentH));
            float y = 0f;
            if (changes.Count == 0)
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(0f, y, rowW, PawnRowH), "WR_PreviewNoChanges".Translate());
                GUI.color = Color.white;
            }
            foreach (var c in changes)
            {
                Widgets.Label(new Rect(0f, y, rowW, PawnRowH), c.pawn.LabelShortCap);
                y += PawnRowH;
                GUI.color = new Color(0.75f, 0.75f, 0.75f);
                foreach (var line in c.lines)
                {
                    Widgets.Label(new Rect(16f, y, rowW - 16f, LineH), line);
                    y += LineH;
                }
                GUI.color = Color.white;
                y += GroupGap;
            }
            Widgets.EndScrollView();

            float btnY = inRect.yMax - ButtonH;
            var applyRect = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, ButtonH);
            var cancelRect = new Rect(applyRect.x - 8f - ButtonW, btnY, ButtonW, ButtonH);
            if (Widgets.ButtonText(cancelRect, "WR_Cancel".Translate()))
                Close();
            bool canApply = changes.Count > 0;
            if (Widgets.ButtonText(applyRect, "WR_Apply".Translate(), active: canApply) && canApply)
            {
                if (SamePlan(changes, rebuild()))
                    onApply?.Invoke();
                else
                    Messages.Message("WR_PreviewStale".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                Close();
            }
        }
    }
}
