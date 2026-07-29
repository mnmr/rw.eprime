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
        private int builtVersion = -1;

        // Cached row data (rebuilt only when store.Version changes)
        private struct PoolRow
        {
            public int Id;
            public string Name;
            public ThingDef IconDef; // resolved from snapshot; null when unresolvable
        }

        private List<PoolRow> cachedRows;
        private string pendingSelectName;

        // DesiredHeight caching
        private float cachedDesiredHeight = -1f;
        private bool cachedFoldState;
        private float cachedDesiredHeightWidth = -1f;

        /// Returns the desired height for this panel: header (including caption when
        /// unfolded) + min(rowCount, 8) * RowH + FooterH. Recomputed only when the
        /// fold state or available width changes; the row count comes from the already-
        /// cached row list so there is no extra work in steady state.
        public float DesiredHeight(float availableWidth)
        {
            var settings = EPrimeReadoutsMod.Settings;
            bool folded = settings.helpPoolsFolded;
            if (cachedDesiredHeight < 0f
                || cachedFoldState != folded
                || cachedDesiredHeightWidth != availableWidth)
            {
                float h = 28f; // SectionHeader baseline
                if (!folded)
                {
                    Text.Font = GameFont.Tiny;
                    h += Text.CalcHeight("EPR.HelpPools".Translate(), availableWidth) + 4f;
                    Text.Font = GameFont.Small;
                }
                int rowCount = cachedRows != null ? cachedRows.Count : 0;
                h += Mathf.Min(rowCount, 8) * RowH;
                h += FooterH;
                cachedDesiredHeight = h;
                cachedFoldState = folded;
                cachedDesiredHeightWidth = availableWidth;
            }
            return cachedDesiredHeight;
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

            // Rebuild cached rows when store version changes
            if (builtVersion != store.Version)
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
            builtVersion = store.Version;
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
            // Invalidate the desired-height cache so the next call recomputes with the new row count
            cachedDesiredHeight = -1f;
        }
    }
}
