using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Right-side top panel: list of resource pools with icon + name, add/rename/delete,
    /// drag-source into group editor tier slots.
    public sealed class PoolListView
    {
        private const float RowH = 26f;
        private const float FooterH = 30f;
        private const float IconW = 20f;
        private const int VirtualizeThreshold = 30;

        /// Right-side width the dialog reserves in the header strip (toggle
        /// button); the fold-clickable region shrinks to stay clear of it.
        public float HeaderReservedRight;

        private Vector2 scroll;
        private int builtPoolsVersion = -1;

        // Cached row data (rebuilt only when pools change)
        private struct PoolRow
        {
            public int Id;
            public string Name;
            public ThingDef IconDef; // resolved from snapshot; null when unresolvable
        }

        private List<PoolRow> cachedRows;
        private string pendingSelectName;

        private readonly PoolListHeightCache heightCache = new PoolListHeightCache(
            headerHeight: 28f,
            captionGap: 4f,
            rowHeight: RowH,
            maxVisibleRows: 8,
            footerHeight: FooterH);
        private static readonly Func<float, float> measureCaptionHeight = MeasureCaptionHeight;

        /// Returns the desired height for this panel: header (including caption when
        /// unfolded) + min(rowCount, 8) * RowH + FooterH. Recomputed only when the
        /// fold state, available width, or pool version changes. Caption measurement
        /// is cached separately, so pool edits only redo the cheap row calculation.
        public float DesiredHeight(float availableWidth)
        {
            UiVersion.ObserveCurrentMetrics();
            var store = ReadoutStore.Current;
            var settings = EPrimeReadoutsMod.Settings;
            bool folded = settings.helpPoolsFolded;
            int poolsVersion = store != null ? store.PoolsVersion : -1;
            int rowCount = store != null ? store.Model.Pools.Count : 0;
            return heightCache.GetDesiredHeight(
                poolsVersion,
                UiVersion.Current,
                rowCount,
                folded,
                availableWidth,
                measureCaptionHeight);
        }

        private static float MeasureCaptionHeight(float availableWidth)
        {
            return EprStyle.CaptionHeight("EPR.HelpPools".Translate(), availableWidth);
        }

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            var store = ReadoutStore.Current;
            var settings = EPrimeReadoutsMod.Settings;
            if (store == null) return;

            // Section header with fold toggle
            bool folded = settings.helpPoolsFolded;
            float clickableW = HeaderReservedRight > 0f
                ? rect.width - HeaderReservedRight : -1f;
            float headerUsed = EprStyle.SectionHeader(rect.x, rect.y, rect.width,
                "EPR.Pools".Translate(), "EPR.HelpPools".Translate(), ref folded,
                clickableW);
            if (folded != settings.helpPoolsFolded)
                EPrimeReadoutsMod.Persist(s => s.helpPoolsFolded = folded);

            // Rebuild cached rows when pool data changes
            if (builtPoolsVersion != store.PoolsVersion)
                Rebuild(store, owner);

            // Defensive: clear selection if selected pool is gone
            if (owner.selectedPoolId >= 0 && store.Model.PoolById(owner.selectedPoolId) == null)
                owner.selectedPoolId = -1;

            // Pending selection by name (after create) — select AND scroll the
            // new row into view (pools are name-sorted, so it can land anywhere).
            if (pendingSelectName != null)
            {
                for (int i = 0; i < (cachedRows?.Count ?? 0); i++)
                    if (cachedRows[i].Name == pendingSelectName)
                    {
                        owner.selectedPoolId = cachedRows[i].Id;
                        float visibleH = rect.height - headerUsed - FooterH;
                        float rowTop = i * RowH;
                        if (rowTop < scroll.y)
                            scroll.y = rowTop;
                        else if (rowTop + RowH > scroll.y + visibleH)
                            scroll.y = rowTop + RowH - Mathf.Max(RowH, visibleH);
                        break;
                    }
                pendingSelectName = null;
            }

            int rowCount = cachedRows != null ? cachedRows.Count : 0;

            var listRect = new Rect(rect.x, rect.y + headerUsed,
                rect.width, rect.height - headerUsed - FooterH);
            var viewRect = new Rect(0f, 0f, listRect.width - 16f, rowCount * RowH);
            Widgets.BeginScrollView(listRect, ref scroll, viewRect);

            if (rowCount > 0)
            {
                bool useVirtual = rowCount > VirtualizeThreshold;
                int start = 0, end = rowCount;
                if (useVirtual && listRect.height > 0f)
                {
                    var vr = UniformViewportRange.Calculate(rowCount, RowH, 0f, scroll.y, listRect.height);
                    start = vr.Start;
                    end = vr.EndExclusive;
                }
                else if (listRect.height <= 0f)
                {
                    end = 0;
                }

                for (int i = start; i < end; i++)
                    DrawPoolRow(cachedRows[i], i, viewRect.width, owner, store);
            }

            Widgets.EndScrollView();

            // Footer: Add button → name dialog
            var footer = new Rect(rect.x, rect.yMax - FooterH + 4f, rect.width, FooterH - 4f);
            if (Widgets.ButtonText(footer, "EPR.Add".Translate()))
            {
                Find.WindowStack.Add(new Dialog_NameInput(
                    "EPR.Pools", "",
                    name =>
                    {
                        ReadoutCommands.CreatePool(name);
                        pendingSelectName = name;
                    }));
            }
        }

        private void DrawPoolRow(PoolRow row, int index, float viewW, Dialog_ReadoutConfig owner,
            ReadoutStore store)
        {
            var rect = new Rect(0f, index * RowH, viewW, RowH);

            // Highlight selected / hover
            if (row.Id == owner.selectedPoolId) Widgets.DrawHighlightSelected(rect);
            else if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);

            // Icon
            if (row.IconDef != null)
                Widgets.ThingIcon(new Rect(rect.x + 2f, rect.y + 3f, IconW, IconW), row.IconDef);

            // Name label
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + IconW + 6f, rect.y, viewW - IconW - 6f - 48f, RowH), row.Name);
            Text.Anchor = TextAnchor.UpperLeft;

            // Rename pencil button
            var renameRect = new Rect(rect.xMax - 46f, rect.y + 2f, 22f, 22f);
            if (Widgets.ButtonImage(renameRect, TexButton.Rename))
            {
                int capturedId = row.Id;
                string capturedName = row.Name;
                Find.WindowStack.Add(new Dialog_NameInput(
                    "EPR.Rename", capturedName,
                    newName => ReadoutCommands.RenamePool(capturedId, newName)));
            }

            // Delete ✕ button
            var deleteRect = new Rect(rect.xMax - 22f, rect.y + 2f, 22f, 22f);
            if (Widgets.ButtonText(deleteRect, "✕"))
            {
                int capturedId = row.Id;
                string capturedName = row.Name;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "EPR.DeletePoolConfirm".Translate(capturedName),
                    () =>
                    {
                        ReadoutCommands.DeletePool(capturedId);
                        if (owner.selectedPoolId == capturedId) owner.selectedPoolId = -1;
                    },
                    destructive: true));
            }

            // Drag source: drag this pool into group editor tier slots.
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            EprDrag.ObserveSource(controlId, rect);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && !renameRect.Contains(Event.current.mousePosition)
                && !deleteRect.Contains(Event.current.mousePosition)
                && rect.Contains(Event.current.mousePosition))
            {
                int capturedId = row.Id;
                string poolToken = SlotToken.PoolToken(capturedId);
                EprDrag.OnPressToken(controlId, poolToken, -1, -1,
                    clickAction: () => owner.selectedPoolId = capturedId);
                Event.current.Use();
            }
        }

        private void Rebuild(ReadoutStore store, Dialog_ReadoutConfig owner)
        {
            builtPoolsVersion = store.PoolsVersion;
            var snapshot = owner.PoolsSnapshot;

            cachedRows = new List<PoolRow>(store.Model.Pools.Count);
            foreach (var pool in store.Model.Pools)
            {
                ThingDef iconDef = null;
                if (snapshot != null && snapshot.TryGet(pool.Id, out _, out string iconDefName, out _))
                    iconDef = !string.IsNullOrEmpty(iconDefName)
                        ? DefDatabase<ThingDef>.GetNamedSilentFail(iconDefName)
                        : null;
                cachedRows.Add(new PoolRow { Id = pool.Id, Name = pool.Name, IconDef = iconDef });
            }
        }
    }
}
