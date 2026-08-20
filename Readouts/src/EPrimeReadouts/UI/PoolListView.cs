using EPrimeReadouts.Core;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Full-height center-panel list of resource pools with create, select,
    /// rename, and delete actions.
    public sealed class PoolListView
    {
        private const float RowH = 26f;
        private const float FooterH = 30f;
        private const float IconW = 20f;
        private const int VirtualizeThreshold = 30;

        private Vector2 scroll;
        private PoolSnapshot? builtSnapshot;

        private struct PoolRow
        {
            internal int Id;
            internal string Name;
            internal ThingDef? IconDef;
        }

        // Cache contract:
        // Owner: this dialog view.
        // Key: shared PoolSnapshot identity.
        // Value: immutable pool rows with resolved icon defs.
        // Dependencies: pool snapshot id/name/icon data.
        // Refresh policy: immediate when the shared snapshot changes.
        // Equality policy: unchanged snapshot identity preserves row identity.
        // Teardown: Reset releases row, snapshot, and def references.
        private PoolRow[]? cachedRows;
        private int pendingSelectId = -1;
        private string? pendingSelectName;

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            if (ReadoutStore.Current == null) return;
            var settings = EPrimeReadoutsMod.Settings;

            bool folded = settings.helpPoolsFolded;
            float headerUsed = EprStyle.HelpGroup(
                rect.x,
                rect.y,
                rect.width,
                UiText.Get("EPR.Help"),
                UiText.Get("EPR.HelpPools"),
                ref folded);
            if (folded != settings.helpPoolsFolded)
                EPrimeReadoutsMod.Persist(s => s.helpPoolsFolded = folded);

            EnsureRows(owner.PoolsSnapshot);

            if (owner.selectedPoolId >= 0 && !Contains(owner.selectedPoolId))
                owner.selectedPoolId = -1;

            float listHeight = Mathf.Max(0f, rect.height - headerUsed - FooterH);
            if (pendingSelectId < 0 && pendingSelectName != null)
            {
                for (int i = 0; i < cachedRows!.Length; i++)
                {
                    if (!PoolNameRules.Comparer.Equals(
                        cachedRows[i].Name, pendingSelectName)) continue;
                    pendingSelectId = cachedRows[i].Id;
                    pendingSelectName = null;
                    break;
                }
            }
            if (pendingSelectId >= 0)
            {
                for (int i = 0; i < cachedRows!.Length; i++)
                {
                    if (cachedRows[i].Id != pendingSelectId) continue;
                    owner.selectedPoolId = cachedRows[i].Id;
                    float rowTop = i * RowH;
                    if (rowTop < scroll.y)
                        scroll.y = rowTop;
                    else if (rowTop + RowH > scroll.y + listHeight)
                        scroll.y = rowTop + RowH - Mathf.Max(RowH, listHeight);
                    pendingSelectId = -1;
                    pendingSelectName = null;
                    break;
                }
            }

            var listRect = new Rect(
                rect.x, rect.y + headerUsed, rect.width, listHeight);
            int rowCount = cachedRows!.Length;
            var viewRect = new Rect(
                0f, 0f, Mathf.Max(0f, listRect.width - 16f), rowCount * RowH);
            if (listRect.height > 0f)
            {
                Widgets.BeginScrollView(listRect, ref scroll, viewRect);
                try
                {
                    int start = 0;
                    int end = rowCount;
                    if (rowCount > VirtualizeThreshold)
                    {
                        var visible = UniformViewportRange.Calculate(
                            rowCount, RowH, 0f, scroll.y, listRect.height);
                        start = visible.Start;
                        end = visible.EndExclusive;
                    }
                    for (int i = start; i < end; i++)
                        DrawPoolRow(cachedRows[i], i, viewRect.width, owner);
                }
                finally
                {
                    Widgets.EndScrollView();
                }
            }

            var footer = new Rect(
                rect.x, rect.yMax - FooterH + 4f, rect.width, FooterH - 4f);
            if (Widgets.ButtonText(footer, UiText.Get("EPR.Add")))
            {
                Find.WindowStack.Add(new Dialog_NameInput(
                    "EPR.Pools", "",
                    name =>
                    {
                        ReadoutCommands.CreatePool(name);
                        ResourcePool? created =
                            ReadoutStore.Current?.Model.PoolByName(name);
                        if (created != null)
                            pendingSelectId = created.Id;
                        else
                            pendingSelectName = PoolNameRules.Normalize(name);
                    },
                    name => PoolNameProblem(name, -1)));
            }
        }

        private static void DrawPoolRow(PoolRow row, int index, float viewWidth,
            Dialog_ReadoutConfig owner)
        {
            var rect = new Rect(0f, index * RowH, viewWidth, RowH);
            if (row.Id == owner.selectedPoolId)
                Widgets.DrawHighlightSelected(rect);
            else if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            if (row.IconDef != null)
                Widgets.ThingIcon(
                    new Rect(rect.x + 2f, rect.y + 3f, IconW, IconW), row.IconDef);

            using (new GuiStateScope())
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(
                    rect.x + IconW + 6f,
                    rect.y,
                    viewWidth - IconW - 6f - 48f,
                    RowH), row.Name);
            }

            var renameRect = new Rect(rect.xMax - 46f, rect.y + 2f, 22f, 22f);
            if (Widgets.ButtonImage(renameRect, TexButton.Rename))
            {
                int capturedId = row.Id;
                string capturedName = row.Name;
                Find.WindowStack.Add(new Dialog_NameInput(
                    "EPR.Rename", capturedName,
                    name => ReadoutCommands.RenamePool(capturedId, name),
                    name => PoolNameProblem(name, capturedId)));
            }

            var deleteRect = new Rect(rect.xMax - 22f, rect.y + 2f, 22f, 22f);
            if (Widgets.ButtonText(deleteRect, "✕"))
            {
                int capturedId = row.Id;
                string capturedName = row.Name;
                Find.WindowStack.Add(new Dialog_CompactConfirm(
                    "EPR.DeletePoolConfirm".Translate(capturedName),
                    () =>
                    {
                        ReadoutCommands.DeletePool(capturedId);
                        if (owner.selectedPoolId == capturedId)
                            owner.selectedPoolId = -1;
                    },
                    destructive: true));
            }

            Event current = Event.current;
            if (current.type == EventType.MouseDown
                && current.button == 0
                && rect.Contains(current.mousePosition)
                && !renameRect.Contains(current.mousePosition)
                && !deleteRect.Contains(current.mousePosition))
            {
                owner.selectedPoolId = row.Id;
                current.Use();
            }
        }

        private bool Contains(int poolId)
        {
            for (int i = 0; i < cachedRows!.Length; i++)
                if (cachedRows[i].Id == poolId) return true;
            return false;
        }

        private static string? PoolNameProblem(string name, int exceptPoolId)
        {
            ReadoutStore? store = ReadoutStore.Current;
            if (store == null || store.Model.CanUsePoolName(name, exceptPoolId))
                return null;
            return UiText.Get("EPR.PoolNameTaken");
        }

        private void EnsureRows(PoolSnapshot? snapshot)
        {
            if (ReferenceEquals(builtSnapshot, snapshot) && cachedRows != null)
                return;

            builtSnapshot = snapshot;
            int count = snapshot?.Count ?? 0;
            var built = new PoolRow[count];
            for (int i = 0; i < count; i++)
            {
                PoolSnapshotEntry entry = snapshot!.EntryAt(i);
                ThingDef? iconDef = !string.IsNullOrEmpty(entry.IconDefName)
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(entry.IconDefName)
                    : null;
                built[i] = new PoolRow
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    IconDef = iconDef,
                };
            }
            cachedRows = built;
        }

        internal void Reset()
        {
            builtSnapshot = null;
            cachedRows = null;
            pendingSelectId = -1;
            pendingSelectName = null;
            scroll = Vector2.zero;
        }
    }
}
