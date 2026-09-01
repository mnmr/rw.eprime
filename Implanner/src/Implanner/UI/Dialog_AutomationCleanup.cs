using System.Collections.Generic;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    /// The automation hand-back dialog, opened when the player clicks the
    /// master switch off while Implanner still owns bills. Automation stays
    /// ON until OK: only then are the pause command and the synced cleanup
    /// (checked bill ids) issued; Cancel or ESC aborts the switch entirely.
    /// Labels and the body measurement resolve once at construction
    /// (language cannot change while a dialog is open); the draw pass
    /// renders prebuilt rows only.
    internal sealed class Dialog_AutomationCleanup : Window
    {
        private sealed class BillRow
        {
            internal string BillId = "";
            internal string Label = "";
            internal bool Remove = true;
        }

        private const float RowH = 26f;
        private const float GroupHeaderH = 26f;
        private const float TitleH = 34f;
        private const float BtnW = 120f;
        private const float BtnH = 32f;
        private const float Gap = 10f;
        private const float DialogWidth = 560f;
        private const float MaxListH = 320f;

        private readonly List<BillRow> surgeryRows = new List<BillRow>();
        private readonly List<BillRow> productionRows = new List<BillRow>();
        private readonly string title;
        private readonly string body;
        private readonly string surgeryHeader;
        private readonly string productionHeader;
        private readonly string okLabel;
        private readonly string cancelLabel;
        private readonly float bodyHeight;
        private readonly float listHeight;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(DialogWidth,
            TitleH + bodyHeight + Gap + Mathf.Min(listHeight, MaxListH)
            + Gap + BtnH + Margin * 2f);

        /// Opens the dialog when Implanner owns bills. With none there is
        /// nothing to decide: automation pauses directly and any held
        /// reservations release with it.
        internal static void ShowToTurnOffAutomation(ImplannerStore store)
        {
            var dialog = new Dialog_AutomationCleanup(store);
            if (dialog.surgeryRows.Count + dialog.productionRows.Count == 0)
            {
                PlannerCommands.SetAutomationPaused(true);
                if (store.Model.Reservations.Count > 0)
                    PlannerCommands.CleanupAutomation("");
                return;
            }
            Find.WindowStack.Add(dialog);
        }

        private Dialog_AutomationCleanup(ImplannerStore store)
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;

            title = "IMP_CleanupTitle".Translate().ToString();
            body = "IMP_CleanupBody".Translate().ToString();
            surgeryHeader = "IMP_OptSurgery".Translate().ToString();
            productionHeader = "IMP_OptProduction".Translate().ToString();
            okLabel = "OK".Translate().ToString();
            cancelLabel = "IMP_Cancel".Translate().ToString();

            BuildRows(store);
            int headers = (surgeryRows.Count > 0 ? 1 : 0)
                + (productionRows.Count > 0 ? 1 : 0);
            listHeight = headers * GroupHeaderH
                + (surgeryRows.Count + productionRows.Count) * RowH;

            // Resolved once per open, welcome-dialog style: the body wraps
            // at the dialog's fixed content width.
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                bodyHeight = Mathf.Ceil(
                    Text.CalcHeight(body, DialogWidth - Margin * 2f));
            }
        }

        /// One row per live owned bill, resolved from the records once:
        /// surgery bills on planable colonists' operation lists, production
        /// bills on colonist worktables. Records whose bill object is gone
        /// produce no row (the command drops stale records regardless).
        private void BuildRows(ImplannerStore store)
        {
            var surgeryIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (KeyValuePair<int, IReadOnlyDictionary<string, string>> pawn
                in store.Model.OwnedBills)
                foreach (KeyValuePair<string, string> record in pawn.Value)
                    surgeryIds.Add(record.Value);
            List<Pawn> pawns = ColonyScope.AllPlanableColonists();
            for (int i = 0; i < pawns.Count; i++)
            {
                BillStack? stack = pawns[i].BillStack;
                if (stack == null) continue;
                for (int b = 0; b < stack.Count; b++)
                {
                    Bill bill = stack[b];
                    if (!surgeryIds.Contains(bill.GetUniqueLoadID())) continue;
                    surgeryRows.Add(new BillRow
                    {
                        BillId = bill.GetUniqueLoadID(),
                        Label = pawns[i].LabelShortCap + ": " + bill.LabelCap,
                    });
                }
            }

            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Building> buildings =
                    maps[m].listerBuildings.allBuildingsColonist;
                for (int b = 0; b < buildings.Count; b++)
                {
                    if (!(buildings[b] is Building_WorkTable bench)) continue;
                    BillStack bills = bench.BillStack;
                    for (int i = 0; i < bills.Count; i++)
                    {
                        if (!(bills[i] is Bill_Production bill)
                            || !store.Model.OwnedProductionBills.ContainsKey(
                                bill.GetUniqueLoadID()))
                            continue;
                        string label = bench.LabelShortCap + ": " + bill.LabelCap;
                        if (bill.repeatMode == BillRepeatModeDefOf.RepeatCount)
                            label += " x" + bill.repeatCount;
                        productionRows.Add(new BillRow
                        {
                            BillId = bill.GetUniqueLoadID(),
                            Label = label,
                        });
                    }
                }
            }
            surgeryRows.Sort(ByLabel);
            productionRows.Sort(ByLabel);
        }

        private static readonly System.Comparison<BillRow> ByLabel =
            static (a, b) => string.Compare(a.Label, b.Label,
                System.StringComparison.OrdinalIgnoreCase);

        public override void DoWindowContents(Rect inRect)
        {
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f),
                    title);
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                Widgets.Label(new Rect(inRect.x, inRect.y + TitleH,
                    inRect.width, bodyHeight), body);

                Rect outer = new Rect(inRect.x, inRect.y + TitleH + bodyHeight + Gap,
                    inRect.width, inRect.yMax - BtnH - Gap
                        - (inRect.y + TitleH + bodyHeight + Gap));
                bool scrolls = listHeight > outer.height;
                Rect inner = new Rect(0f, 0f,
                    scrolls ? outer.width - 16f : outer.width, listHeight);
                Text.WordWrap = false;
                Widgets.BeginScrollView(outer, ref scroll, inner);
                try
                {
                    float y = 0f;
                    y += DrawGroup(inner.width, y, surgeryHeader, surgeryRows);
                    y += DrawGroup(inner.width, y, productionHeader, productionRows);
                }
                finally
                {
                    Widgets.EndScrollView();
                }
                Text.WordWrap = true;

                var cancelRect = new Rect(inRect.x, inRect.yMax - BtnH, BtnW, BtnH);
                var okRect = new Rect(inRect.xMax - BtnW, inRect.yMax - BtnH,
                    BtnW, BtnH);
                if (Widgets.ButtonText(cancelRect, cancelLabel)) Close();
                if (Widgets.ButtonText(okRect, okLabel)) Apply();
            }
        }

        private static float DrawGroup(float width, float y, string header,
            List<BillRow> rows)
        {
            if (rows.Count == 0) return 0f;
            float used = 0f;
            using (GuiStateScope.Capture())
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = PlannerStyle.HeaderText;
                Widgets.Label(new Rect(0f, y, width, GroupHeaderH), header);
            }
            used += GroupHeaderH;
            for (int i = 0; i < rows.Count; i++)
            {
                Widgets.CheckboxLabeled(new Rect(8f, y + used, width - 8f, RowH),
                    rows[i].Label, ref rows[i].Remove);
                used += RowH;
            }
            return used;
        }

        /// OK: pause automation, then one synced command carrying every
        /// checked bill id (reservation release rides along inside it
        /// either way). Cancel never reaches here, so automation stays on.
        private void Apply()
        {
            var ids = new System.Text.StringBuilder();
            AppendChecked(ids, surgeryRows);
            AppendChecked(ids, productionRows);
            PlannerCommands.SetAutomationPaused(true);
            PlannerCommands.CleanupAutomation(ids.ToString());
            Close();
        }

        private static void AppendChecked(
            System.Text.StringBuilder ids, List<BillRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].Remove) continue;
                if (ids.Length > 0) ids.Append('\n');
                ids.Append(rows[i].BillId);
            }
        }
    }
}
