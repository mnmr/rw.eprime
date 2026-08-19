using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
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

        private Vector2 scroll;
        private int builtPoolsVersion = -1;
        private ReadoutStore? builtStore;
        private PoolSnapshot? builtSnapshot;

        // Cached row data (rebuilt only when pools change)
        private struct PoolRow
        {
            public int Id;
            public string Name;
            public ThingDef? IconDef; // resolved from snapshot; null when unresolvable
        }

        // Cache contract:
        // Owner: this dialog view and one ReadoutStore.
        // Key: store identity, PoolsVersion, and shared PoolSnapshot identity.
        // Value: immutable row array with resolved ThingDef icons.
        // Dependencies: pool id/name/icon data.
        // Refresh policy: immediate on a dependency change.
        // Equality policy: unchanged dependencies preserve array identity.
        // Teardown: Reset releases row and snapshot references on dialog close.
        private PoolRow[]? cachedRows;
        private string? pendingSelectName;

        private readonly PoolListHeightCache heightCache = new PoolListHeightCache(
            headerHeight: 2f * EprStyle.SectionHeaderHeight
                + EprStyle.HelpCollapsedBottomMargin,
            captionGap: 2f * EprStyle.HelpPanelPadding
                + EprStyle.HelpExpandedBottomMargin,
            rowHeight: RowH,
            maxVisibleRows: 8,
            footerHeight: FooterH);
        private static readonly Func<string, float, float> measureCaptionHeight = MeasureCaptionHeight;

        /// Returns the desired height for this panel: title header, Help foldout,
        /// optional framed Help panel, up to eight rows, and the footer. Recomputed
        /// only when the fold state, width, UI metrics, or pool data changes.
        public float DesiredHeight(float availableWidth, Dialog_ReadoutConfig owner)
        {
            UiVersion.ObserveCurrentMetrics();
            var store = ReadoutStore.Current;
            var settings = EPrimeReadoutsMod.Settings;
            bool folded = settings.helpPoolsFolded;
            if (store != null) EnsureRows(store, owner);
            int poolsVersion = store != null ? store.PoolsVersion : -1;
            int rowCount = cachedRows?.Length ?? 0;
            return heightCache.GetDesiredHeight(
                store!, // compared only by reference; null owner is tolerated
                poolsVersion,
                UiVersion.Current,
                rowCount,
                folded,
                availableWidth,
                UiText.Get("EPR.HelpPools"),
                measureCaptionHeight);
        }

        /// Minimum height that keeps both headers, the complete Help foldout,
        /// and the Add footer inside this panel. Rows can use a smaller
        /// scrollable viewport, but these fixed elements cannot.
        public float MinimumHeight(float availableWidth)
        {
            var settings = EPrimeReadoutsMod.Settings;
            return EprStyle.SectionHeaderHeight
                + EprStyle.HelpGroupHeight(
                    availableWidth,
                    UiText.Get("EPR.HelpPools"),
                    settings.helpPoolsFolded)
                + FooterH;
        }

        private static float MeasureCaptionHeight(string caption, float availableWidth)
        {
            return EprStyle.CaptionHeight(
                caption,
                Mathf.Max(1f, availableWidth - 2f * EprStyle.HelpPanelPadding));
        }

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            var store = ReadoutStore.Current;
            var settings = EPrimeReadoutsMod.Settings;
            if (store == null) return;

            float headerUsed = EprStyle.SectionHeader(
                rect.x, rect.y, rect.width, UiText.Get("EPR.Pools"));

            bool folded = settings.helpPoolsFolded;
            headerUsed += EprStyle.HelpGroup(
                rect.x,
                rect.y + headerUsed,
                rect.width,
                UiText.Get("EPR.Help"),
                UiText.Get("EPR.HelpPools"),
                ref folded);
            if (folded != settings.helpPoolsFolded)
            {
                EPrimeReadoutsMod.Persist(s => s.helpPoolsFolded = folded);
                // Dialog_ReadoutConfig sized this panel before this input event
                // toggled the foldout. Let the next IMGUI pass recalculate the
                // outer rectangle before drawing any height-dependent body.
                return;
            }

            // Rebuild cached rows when pool data changes
            EnsureRows(store, owner);

            // Defensive: clear selection if selected pool is gone
            if (owner.selectedPoolId >= 0)
            {
                bool found = false;
                for (int i = 0; i < cachedRows!.Length; i++) // built by EnsureRows above
                    if (cachedRows[i].Id == owner.selectedPoolId) { found = true; break; }
                if (!found) owner.selectedPoolId = -1;
            }

            // Pending selection by name (after create) — select AND scroll the
            // new row into view (pools are name-sorted, so it can land anywhere).
            if (pendingSelectName != null)
            {
                for (int i = 0; i < (cachedRows?.Length ?? 0); i++)
                    if (cachedRows![i].Name == pendingSelectName) // loop entered only when non-null
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

            int rowCount = cachedRows?.Length ?? 0;

            float bodyHeight = rect.height - headerUsed;
            if (bodyHeight < FooterH) return;

            var listRect = new Rect(rect.x, rect.y + headerUsed,
                rect.width, bodyHeight - FooterH);
            var viewRect = new Rect(0f, 0f, listRect.width - 16f, rowCount * RowH);
            if (listRect.height > 0f)
            {
                Widgets.BeginScrollView(listRect, ref scroll, viewRect);
                try
                {
                if (rowCount > 0)
                {
                    bool useVirtual = rowCount > VirtualizeThreshold;
                    int start = 0, end = rowCount;
                    if (useVirtual)
                    {
                        var vr = UniformViewportRange.Calculate(
                            rowCount, RowH, 0f, scroll.y, listRect.height);
                        start = vr.Start;
                        end = vr.EndExclusive;
                    }

                    for (int i = start; i < end; i++)
                        DrawPoolRow(cachedRows![i], i, viewRect.width, owner); // rowCount > 0 implies rows
                }

                }
                finally
                {
                    Widgets.EndScrollView();
                }
            }

            // Footer: Add button → name dialog
            var footer = new Rect(rect.x, rect.yMax - FooterH + 4f, rect.width, FooterH - 4f);
            if (Widgets.ButtonText(footer, UiText.Get("EPR.Add")))
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

        private void DrawPoolRow(PoolRow row, int index, float viewW, Dialog_ReadoutConfig owner)
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

        private void EnsureRows(ReadoutStore store, Dialog_ReadoutConfig owner)
        {
            PoolSnapshot? snapshot = owner.PoolsSnapshot;
            if (ReferenceEquals(builtStore, store)
                && builtPoolsVersion == store.PoolsVersion
                && ReferenceEquals(builtSnapshot, snapshot)
                && cachedRows != null)
                return;

            builtStore = store;
            builtPoolsVersion = store.PoolsVersion;
            builtSnapshot = snapshot;

            var pools = store.Model.Pools;
            var built = new PoolRow[pools.Count];
            for (int i = 0; i < pools.Count; i++)
            {
                ResourcePool pool = pools[i];
                ThingDef? iconDef = null;
                if (snapshot != null && snapshot.TryGet(pool.Id, out _, out string? iconDefName, out _))
                    iconDef = !string.IsNullOrEmpty(iconDefName)
                        ? DefDatabase<ThingDef>.GetNamedSilentFail(iconDefName)
                        : null;
                built[i] = new PoolRow { Id = pool.Id, Name = pool.Name, IconDef = iconDef };
            }
            cachedRows = built;
        }

        internal void Reset()
        {
            builtStore = null;
            builtPoolsVersion = -1;
            builtSnapshot = null;
            cachedRows = null;
            pendingSelectName = null;
            heightCache.Reset();
            scroll = Vector2.zero;
        }
    }
}
