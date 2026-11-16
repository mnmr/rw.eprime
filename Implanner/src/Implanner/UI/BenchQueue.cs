using System.Collections.Generic;
using System.Text;
using Implanner.Core;
using RimShared.UiLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    /// One Implanner bill on a designated bench, resolved for drawing.
    internal sealed class BenchQueueRow
    {
        internal string BillId = "";
        internal Texture Icon = BaseContent.BadTex;
        internal Color IconColor = Color.white;
        internal string Label = "";
        /// The full label when Label was shortened to fit.
        internal string? LabelTip;
        internal string Status = "";
        internal Color StatusColor = Color.white;
        internal float StatusWidth;
        /// Work done on the unfinished item, or -1 when nothing is started.
        internal float Progress = -1f;
        /// Caption/value pairs. A wide pair (plan, implant) has a line to
        /// itself; the others sit two per line. Line and column per pair.
        internal string[] Captions = System.Array.Empty<string>();
        internal string[] Values = System.Array.Empty<string>();
        internal string?[] ValueTips = System.Array.Empty<string?>();
        internal int[] Lines = System.Array.Empty<int>();
        internal int[] Columns = System.Array.Empty<int>();
        internal float[] ValueWidths = System.Array.Empty<float>();
        internal float Y;
        internal float Height;
    }

    internal sealed class BenchQueueSnapshot
    {
        internal BenchQueueRow[] Rows = System.Array.Empty<BenchQueueRow>();
        internal string Title = "";
        internal string Details = "";
        internal string Empty = "";
        internal string Footer = "";
        internal string FooterTip = "";
        internal float TitleHeight;
        internal float SmallLine;
        internal float TinyLine;
        internal float CaptionWidth;
        internal float ValueWidth;
        internal float ColumnWidth;
        internal float TextWidth;
        internal float DetailsWidth;
        internal float EmptyHeight;
        internal float FooterHeight;
        internal float ListHeight;
    }

    /// The Bills tab of a bench the player handed to Implanner: a header
    /// with the toggle, one row per Implanner bill, and a footer counting
    /// the player's own bills that wait while the bench is Implanner's.
    /// Replaces the vanilla tab (and Nice Bill Tab's) for that bench only.
    internal static class BenchQueue
    {
        internal static readonly Vector2 TabSize = new Vector2(480f, 480f);

        private const float Pad = 10f;
        private const float RowPad = 6f;
        private const float IconSize = 40f;
        private const float IconGap = 8f;
        private const float CaptionGap = 6f;
        private const float ColumnGap = 10f;
        private const float ButtonGap = 8f;
        private const float ButtonHeight = 24f;
        private const float ToggleHeight = 29f;
        private const float ToggleWidth = 190f;
        private const float ProgressHeight = 4f;
        private const float ScrollGutter = 16f;
        private const int RefreshTicks = 204;

        // Cache contract:
        // Owner: process, one entry (the bench whose tab is shown).
        // Key: bench thing id, store identity.
        // Value: an immutable BenchQueueSnapshot: rows with resolved icon,
        //   texts, measured widths and row geometry. Holds bill load ids,
        //   strings and stable game textures, never a bill, pawn or bench.
        // Dependencies: the store's Version (bill records, their age,
        //   target and quality promise, plans, assignments, designations);
        //   the bench's bill stack, each bill's worker and unfinished item
        //   and the game tick, all sampled every 204 game ticks and only
        //   while the tab is drawn (ticks do not advance while paused; a
        //   synced change still rebuilds at once through Version);
        //   UiRevision.Current
        //   (UI scale, tiny-font preference, language).
        // Refresh policy: gated rebuild at the start of the tab's draw
        //   when a key or dependency moved; tick-throttled for game state.
        // Equality policy: none; a rebuild replaces the snapshot.
        // Teardown: Reset on world teardown.
        private static BenchQueueSnapshot? snapshot;
        private static int benchId = -1;
        private static World? world;
        private static ImplannerStore? store;
        private static int version = -1;
        private static int tickBucket = -1;
        private static int uiStamp = -1;
        private static Vector2 scroll;

        internal static void Reset()
        {
            snapshot = null;
            benchId = -1;
            world = null;
            store = null;
            version = tickBucket = uiStamp = -1;
            scroll = default;
        }

        private static BenchQueueSnapshot? Current(Building_WorkTable bench)
        {
            if (!ReferenceEquals(world, Find.World))
            {
                Reset();
                world = Find.World;
                store = ImplannerStore.Current;
            }
            if (store == null) return null;
            UiRevision.ObserveCurrentMetrics();
            int bucket = Find.TickManager.TicksGame / RefreshTicks;
            if (snapshot != null && benchId == bench.thingIDNumber
                && version == store.Version && tickBucket == bucket
                && uiStamp == UiRevision.Current)
                return snapshot;
            if (benchId != bench.thingIDNumber) scroll = default;
            benchId = bench.thingIDNumber;
            version = store.Version;
            tickBucket = bucket;
            uiStamp = UiRevision.Current;
            using (GuiStateScope.Capture())
                snapshot = Build(bench, store.Model);
            return snapshot;
        }

        // ------------------------------------------------------- builder

        private static BenchQueueSnapshot Build(Building_WorkTable bench, PlannerModel model)
        {
            var built = new BenchQueueSnapshot
            {
                Title = "IMP_QueueTitle".Translate(),
                Details = "Details".Translate(),
                Empty = "IMP_QueueEmpty".Translate(),
            };
            Text.WordWrap = false;
            Text.Font = GameFont.Medium;
            built.TitleHeight = Mathf.Ceil(Text.LineHeight);
            Text.Font = GameFont.Small;
            built.SmallLine = Mathf.Ceil(Text.LineHeight);
            built.DetailsWidth = WrText.MeasureFitWidth(built.Details) + 16f;
            built.TinyLine = TinyText.MeasureMetrics(false).LineHeight;

            float inner = TabSize.x - Pad * 2f - ScrollGutter;
            built.TextWidth = inner - IconSize - IconGap - ButtonGap - built.DetailsWidth;
            built.ColumnWidth = Mathf.Floor((built.TextWidth - ColumnGap) / 2f);

            string plan = "IMP_QueuePlan".Translate();
            string target = "IMP_QueueFor".Translate();
            string age = "IMP_QueueAge".Translate();
            string ageTip = "IMP_QueueAgeTip".Translate();
            string quality = "IMP_QueueQuality".Translate();
            string neededFor = "IMP_QueueNeededFor".Translate();
            string worker = "IMP_QueueWorker".Translate();
            using (TinyText.UseFont())
            {
                float widest = 0f;
                widest = Mathf.Max(widest, WrText.MeasureFitWidth(plan));
                widest = Mathf.Max(widest, WrText.MeasureFitWidth(target));
                widest = Mathf.Max(widest, WrText.MeasureFitWidth(age));
                widest = Mathf.Max(widest, WrText.MeasureFitWidth(quality));
                widest = Mathf.Max(widest, WrText.MeasureFitWidth(neededFor));
                widest = Mathf.Max(widest, WrText.MeasureFitWidth(worker));
                built.CaptionWidth = Mathf.Min(widest, built.ColumnWidth * 0.5f);
            }
            built.ValueWidth = built.ColumnWidth - built.CaptionWidth - CaptionGap;

            Dictionary<Bill, Pawn>? crafting = null;
            int now = Find.TickManager.TicksGame;
            var rows = new List<BenchQueueRow>();
            int paused = 0;
            StringBuilder? pausedLabels = null;
            var captions = new List<string>(6);
            var values = new List<string>(6);
            var wide = new List<bool>(6);
            float wideValue = built.TextWidth - built.CaptionWidth - CaptionGap;
            BillStack stack = bench.BillStack;
            float y = 0f;
            for (int i = 0; i < stack.Count; i++)
            {
                Bill bill = stack[i];
                string billId = bill.GetUniqueLoadID();
                if (!model.OwnedProductionBills.TryGetValue(billId, out string? itemName))
                {
                    paused++;
                    (pausedLabels ??= new StringBuilder()).Append(paused > 1 ? "\n" : "")
                        .Append(bill.LabelCap);
                    continue;
                }
                crafting ??= CraftingNow(bench.Map);
                ThingDef? item = bill.recipe.ProducedThingDef
                    ?? DefDatabase<ThingDef>.GetNamedSilentFail(itemName);
                var row = new BenchQueueRow { BillId = billId };
                if (item != null)
                {
                    row.Icon = item.uiIcon != null ? item.uiIcon : BaseContent.BadTex;
                    row.IconColor = item.uiIconColor;
                }

                crafting.TryGetValue(bill, out Pawn? crafter);
                UnfinishedThing? unfinished = (bill as Bill_ProductionWithUft)?.BoundUft;
                if (unfinished != null)
                {
                    float total = bill.GetWorkAmount(unfinished);
                    row.Progress = total > 0f && unfinished.workLeft >= 0f
                        ? Mathf.Clamp01(1f - unfinished.workLeft / total) : 0f;
                }
                if (crafter != null)
                {
                    row.Status = row.Progress >= 0f
                        ? "IMP_QueueStatusCraftingAt".Translate(row.Progress.ToStringPercent())
                        : "IMP_QueueStatusCrafting".Translate();
                    row.StatusColor = PlannerStyle.ActiveText;
                }
                else if (row.Progress >= 0f)
                {
                    row.Status = "IMP_QueueStatusStarted".Translate(row.Progress.ToStringPercent());
                    row.StatusColor = PlannerStyle.PendingText;
                }
                else
                {
                    row.Status = "IMP_QueueStatusWaiting".Translate();
                    row.StatusColor = PlannerStyle.CaptionText;
                }
                Text.Font = GameFont.Small;
                row.StatusWidth = WrText.MeasureFitWidth(row.Status);
                string label = item != null ? (string)item.LabelCap : (string)bill.LabelCap;
                row.Label = WrText.Truncate(label,
                    built.TextWidth - row.StatusWidth - ColumnGap);
                if (!ReferenceEquals(row.Label, label) && row.Label != label) row.LabelTip = label;

                captions.Clear();
                values.Clear();
                wide.Clear();
                int ageIndex = -1;
                ProductionBillOrigin origin = model.ProductionBillOriginOf(billId);
                if (origin.HasTarget)
                {
                    Core.Plan? assigned =model.AssignedPlan(origin.PawnId);
                    if (assigned != null) { captions.Add(plan); values.Add(assigned.Name); wide.Add(true); }
                    if (GoalKeys.TryParseImplantSlot(origin.GoalKey, out _, out string implantName, out _))
                    {
                        ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(implantName);
                        if (entry != null && entry.Item != item)
                        {
                            captions.Add(neededFor);
                            values.Add(entry.Label.CapitalizeFirst());
                            wide.Add(true);
                        }
                    }
                    Pawn? pawn = FindPawn(origin.PawnId);
                    if (pawn != null) { captions.Add(target); values.Add(pawn.LabelShortCap); wide.Add(false); }
                }
                if (origin.CreatedTick >= 0)
                {
                    captions.Add(age);
                    int queued = Mathf.Max(0, now - origin.CreatedTick);
                    ageIndex = values.Count;
                    values.Add(queued < GenDate.TicksPerHour / 10
                        ? (string)"IMP_QueueAgeNow".Translate()
                        : queued.ToStringTicksToPeriod(false));
                    wide.Add(false);
                }
                int promise = model.ProductionBillQualityOf(billId);
                if (promise != ProductionQueue.UnknownQuality)
                {
                    captions.Add(quality);
                    values.Add("IMP_QueueQualityValue".Translate(
                        ((QualityCategory)promise).GetLabel().CapitalizeFirst()));
                    wide.Add(false);
                }
                Pawn? assignedWorker = crafter
                    ?? (bill as Bill_ProductionWithUft)?.BoundWorker ?? bill.PawnRestriction;
                if (assignedWorker != null)
                {
                    captions.Add(worker);
                    values.Add(assignedWorker.LabelShortCap);
                    wide.Add(false);
                }

                row.Captions = captions.ToArray();
                row.Values = new string[values.Count];
                row.ValueTips = new string?[values.Count];
                row.Lines = new int[values.Count];
                row.Columns = new int[values.Count];
                row.ValueWidths = new float[values.Count];
                int lines = 0;
                int column = 0;
                using (TinyText.UseFont())
                    for (int v = 0; v < values.Count; v++)
                    {
                        if (wide[v] && column == 1) { lines++; column = 0; }
                        row.Lines[v] = lines;
                        row.Columns[v] = column;
                        row.ValueWidths[v] = wide[v] ? wideValue : built.ValueWidth;
                        if (wide[v] || column == 1) { lines++; column = 0; }
                        else column = 1;
                        row.Values[v] = WrText.Truncate(values[v], row.ValueWidths[v]);
                        if (row.Values[v] != values[v]) row.ValueTips[v] = values[v];
                    }
                if (column == 1) lines++;
                // The age counts game time, which its value alone does not say.
                if (ageIndex >= 0)
                    row.ValueTips[ageIndex] = row.ValueTips[ageIndex] == null
                        ? ageTip : row.ValueTips[ageIndex] + "\n\n" + ageTip;
                float text = built.SmallLine + lines * built.TinyLine;
                float icon = IconSize + (row.Progress >= 0f ? ProgressHeight + 3f : 0f);
                row.Y = y;
                row.Height = Mathf.Max(text, icon) + RowPad * 2f;
                y += row.Height;
                rows.Add(row);
            }
            built.Rows = rows.ToArray();
            built.ListHeight = y;

            Text.Font = GameFont.Small;
            Text.WordWrap = true;
            built.EmptyHeight = Mathf.Ceil(Text.CalcHeight(built.Empty, TabSize.x - Pad * 4f));
            if (paused > 0)
            {
                built.Footer = paused == 1
                    ? "IMP_QueuePausedOne".Translate()
                    : "IMP_QueuePausedMany".Translate(paused);
                built.FooterTip = pausedLabels!.ToString();
                built.FooterHeight = TinyText.CalcHeight(built.Footer, TabSize.x - Pad * 2f);
            }
            return built;
        }

        /// The colony pawn working each bill at the map right now.
        private static Dictionary<Bill, Pawn> CraftingNow(Map? map)
        {
            var crafting = new Dictionary<Bill, Pawn>();
            if (map == null) return crafting;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int p = 0; p < pawns.Count; p++)
            {
                Bill? bill = pawns[p].CurJob?.bill;
                if (bill != null && !crafting.ContainsKey(bill)) crafting.Add(bill, pawns[p]);
            }
            return crafting;
        }

        private static Pawn? FindPawn(int pawnId)
        {
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                IReadOnlyList<Pawn> pawns = maps[m].mapPawns.AllPawnsSpawned;
                for (int p = 0; p < pawns.Count; p++)
                    if (pawns[p].thingIDNumber == pawnId) return pawns[p];
            }
            return null;
        }

        // ---------------------------------------------------------- draw

        /// Draws the tab in tab-local coordinates (the vanilla tab's group).
        /// False when there is no store to read, so the caller falls back.
        internal static bool Draw(Building_WorkTable bench)
        {
            BenchQueueSnapshot? data = Current(bench);
            if (data == null) return false;
            using (GuiStateScope.Capture())
            {
                float width = TabSize.x - Pad * 2f;
                Text.WordWrap = false;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Medium;
                GUI.color = Color.white;
                Widgets.Label(new Rect(Pad, Pad, width, data.TitleHeight), data.Title);

                float y = Pad + data.TitleHeight + 4f;
                Patches.Patch_BillsTabBench.DrawToggle(
                    new Rect(Pad, y, ToggleWidth, ToggleHeight), bench, true);
                y += ToggleHeight + 6f;
                GUI.color = PlannerStyle.CaptionText;
                WrText.LineHorizontal(Pad, y, width);
                y += 1f;

                float bottom = TabSize.y - Pad;
                if (data.FooterHeight > 0f)
                {
                    bottom -= data.FooterHeight;
                    var footer = new Rect(Pad, bottom, width, data.FooterHeight);
                    Text.WordWrap = true;
                    GUI.color = PlannerStyle.CaptionText;
                    TinyText.Label(footer, data.Footer);
                    Text.WordWrap = false;
                    if (Mouse.IsOver(footer)) TooltipHandler.TipRegion(footer, data.FooterTip);
                    bottom -= 6f;
                    WrText.LineHorizontal(Pad, bottom, width);
                }

                var list = new Rect(Pad, y, width, bottom - y);
                if (data.Rows.Length == 0)
                {
                    Text.Font = GameFont.Small;
                    Text.WordWrap = true;
                    Text.Anchor = TextAnchor.UpperCenter;
                    GUI.color = PlannerStyle.CaptionText;
                    Widgets.Label(new Rect(Pad * 2f, list.y + 24f,
                        TabSize.x - Pad * 4f, data.EmptyHeight), data.Empty);
                    return true;
                }
                DrawRows(list, data, bench);
            }
            return true;
        }

        private static void DrawRows(Rect list, BenchQueueSnapshot data, Building_WorkTable bench)
        {
            var view = new Rect(0f, 0f, list.width - ScrollGutter, data.ListHeight);
            Widgets.BeginScrollView(list, ref scroll, view);
            try
            {
                float top = scroll.y;
                float end = top + list.height;
                float textX = IconSize + IconGap;
                float buttonX = view.width - data.DetailsWidth;
                for (int i = 0; i < data.Rows.Length; i++)
                {
                    BenchQueueRow row = data.Rows[i];
                    if (row.Y + row.Height < top || row.Y > end) continue;
                    if (i % 2 == 1)
                        Widgets.DrawLightHighlight(new Rect(0f, row.Y, view.width, row.Height));
                    float y = row.Y + RowPad;

                    GUI.color = row.IconColor;
                    GUI.DrawTexture(new Rect(0f, y, IconSize, IconSize), row.Icon,
                        ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                    if (row.Progress >= 0f)
                        PlannerStyle.ProgressBar(new Rect(0f, y + IconSize + 3f,
                            IconSize, ProgressHeight), row.Progress);

                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    var label = new Rect(textX, y,
                        data.TextWidth - row.StatusWidth - ColumnGap, data.SmallLine);
                    Widgets.Label(label, row.Label);
                    if (row.LabelTip != null && Mouse.IsOver(label))
                        TooltipHandler.TipRegion(label, row.LabelTip);
                    GUI.color = row.StatusColor;
                    Text.Anchor = TextAnchor.UpperRight;
                    Widgets.Label(new Rect(textX + data.TextWidth - row.StatusWidth, y,
                        row.StatusWidth, data.SmallLine), row.Status);

                    using (TinyText.UseFont())
                        for (int p = 0; p < row.Values.Length; p++)
                        {
                            float x = textX + row.Columns[p] * (data.ColumnWidth + ColumnGap);
                            float lineY = y + data.SmallLine + row.Lines[p] * data.TinyLine;
                            GUI.color = PlannerStyle.CaptionText;
                            Text.Anchor = TextAnchor.UpperRight;
                            Widgets.Label(new Rect(x, lineY, data.CaptionWidth, data.TinyLine),
                                row.Captions[p]);
                            GUI.color = Color.white;
                            Text.Anchor = TextAnchor.UpperLeft;
                            var value = new Rect(x + data.CaptionWidth + CaptionGap, lineY,
                                row.ValueWidths[p], data.TinyLine);
                            Widgets.Label(value, row.Values[p]);
                            if (row.ValueTips[p] != null && Mouse.IsOver(value))
                                TooltipHandler.TipRegion(value, row.ValueTips[p]);
                        }

                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    if (Widgets.ButtonText(new Rect(buttonX,
                            row.Y + (row.Height - ButtonHeight) / 2f,
                            data.DetailsWidth, ButtonHeight), data.Details))
                        OpenDetails(bench, row.BillId);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// The game's own bill dialog, opened the way the vanilla row does.
        private static void OpenDetails(Building_WorkTable bench, string billId)
        {
            BillStack stack = bench.BillStack;
            for (int i = 0; i < stack.Count; i++)
                if (stack[i] is Bill_Production bill && bill.GetUniqueLoadID() == billId)
                {
                    Find.WindowStack.Add(new Dialog_BillConfig(bill, bench.Position));
                    return;
                }
        }
    }
}
