using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace EPrimeReadouts.UI
{
    /// Cached tri-state resource tree used to edit the selected pool.
    internal sealed class PoolResourceTreeBody
    {
        private const float RowH = 24f;
        private const float CheckboxW = 24f;

        private Vector2 scroll;
        private readonly HashSet<string> expanded = new HashSet<string>();
        private int expansionRevision;

        // Cache contract:
        // Owner: this dialog body and one ReadoutStore.
        // Key: store identity.
        // Value: immutable flattened editor rows with resolved ThingDefs/state.
        // Dependencies: selected pool, PoolsVersion, shared filter revision,
        // expansion revision, and language revision.
        // Refresh policy: immediate when visible after any dependency change.
        // Equality policy: unchanged dependencies preserve row-array identity.
        // Teardown: Reset releases rows/store/def references on dialog close.
        private ReadoutStore? builtStore;
        private int builtPoolsVersion = -1;
        private int builtPoolId = -1;
        private int builtFilterRevision = -1;
        private int builtExpansionRevision = -1;
        private int builtLanguageVersion = -1;
        private EditorRow[]? cachedRows;

        private struct EditorRow
        {
            internal bool IsCategory;
            internal int Indent;
            internal string Id;
            internal string DefName;
            internal string Label;
            internal bool Expanded;
            internal TriState State;
            internal ThingDef Def;
            internal bool IsCurrentIcon;
            internal IReadOnlyList<string> MatchingDefNames;
        }

        internal bool Draw(Rect rect, Dialog_ReadoutConfig owner,
            ItemPickerState filters, int filterRevision)
        {
            var store = ReadoutStore.Current;
            if (store == null) return false;

            if (NeedsRebuild(store, owner.selectedPoolId, filterRevision))
                Rebuild(store, owner.selectedPoolId, filters, filterRevision);
            if (cachedRows == null || builtPoolId < 0 || cachedRows.Length == 0)
                return false;

            var viewRect = new Rect(
                0f, 0f, rect.width - 16f, cachedRows.Length * RowH);
            Widgets.BeginScrollView(rect, ref scroll, viewRect);
            try
            {
                var visible = UniformViewportRange.Calculate(
                    cachedRows.Length, RowH, 0f, scroll.y, rect.height);
                for (int i = visible.Start; i < visible.EndExclusive; i++)
                    DrawEditorRow(cachedRows[i], i, viewRect.width);
            }
            finally
            {
                Widgets.EndScrollView();
            }
            return true;
        }

        private bool NeedsRebuild(ReadoutStore store, int poolId,
            int filterRevision)
        {
            return cachedRows == null
                || !ReferenceEquals(store, builtStore)
                || store.PoolsVersion != builtPoolsVersion
                || poolId != builtPoolId
                || filterRevision != builtFilterRevision
                || expansionRevision != builtExpansionRevision
                || UiVersion.LanguageCurrent != builtLanguageVersion;
        }

        private void Rebuild(ReadoutStore store, int poolId,
            ItemPickerState filters, int filterRevision)
        {
            ResourcePool? pool = store.Model.PoolById(poolId);
            bool poolChanged = !ReferenceEquals(store, builtStore)
                || poolId != builtPoolId;

            builtStore = store;
            builtPoolsVersion = store.PoolsVersion;
            builtPoolId = poolId;
            builtFilterRevision = filterRevision;
            builtLanguageVersion = UiVersion.LanguageCurrent;

            if (pool == null)
            {
                cachedRows = System.Array.Empty<EditorRow>();
                builtExpansionRevision = expansionRevision;
                return;
            }

            List<string> members = pool.Members;
            List<ResourceTreeNode> roots = GameResourceTree.GetRoots(filters.Type);
            if (poolChanged)
            {
                expanded.Clear();
                for (int i = 0; i < roots.Count; i++)
                    ExpandSelected(roots[i], members);
                expansionRevision++;
            }
            builtExpansionRevision = expansionRevision;

            List<TreeRow> flat = ResourceTreeFlattener.Flatten(
                roots,
                expanded,
                new ItemTreeFilter(filters.Query, filters.Type, filters.SourceId),
                GameResourceCatalog.Instance);
            var builtRows = new EditorRow[flat.Count];
            for (int i = 0; i < flat.Count; i++)
            {
                TreeRow row = flat[i];
                if (row.IsCategory)
                {
                    builtRows[i] = new EditorRow
                    {
                        IsCategory = true,
                        Indent = row.Indent,
                        Id = row.Id,
                        Label = row.Label,
                        Expanded = row.Expanded,
                        MatchingDefNames = row.MatchingDefNames,
                        State = PoolTriState.ScopeState(
                            members,
                            row.MatchingDefNames,
                            GameResourceCatalog.Instance),
                    };
                    continue;
                }

                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(row.DefName);
                builtRows[i] = new EditorRow
                {
                    Indent = row.Indent,
                    DefName = row.DefName,
                    Label = row.Label,
                    State = PoolTriState.IsSelected(
                        members, row.DefName, GameResourceCatalog.Instance)
                            ? TriState.On
                            : TriState.Off,
                    Def = def,
                    IsCurrentIcon = pool.IconDefName == row.DefName,
                };
            }
            cachedRows = builtRows;

            if (!poolChanged) return;
            int firstPartial = -1;
            int firstOn = -1;
            for (int i = 0; i < cachedRows.Length; i++)
            {
                if (!cachedRows[i].IsCategory) continue;
                if (cachedRows[i].State == TriState.Partial)
                {
                    firstPartial = i;
                    break;
                }
                if (firstOn < 0 && cachedRows[i].State == TriState.On)
                    firstOn = i;
            }
            int target = firstPartial >= 0 ? firstPartial : firstOn;
            scroll.y = target > 0 ? target * RowH : 0f;
        }

        private bool ExpandSelected(ResourceTreeNode node, List<string> members)
        {
            bool selected = PoolTriState.CategoryState(
                members, node.Id, GameResourceCatalog.Instance) != TriState.Off;
            for (int i = 0; i < node.Children.Count; i++)
                if (ExpandSelected(node.Children[i], members)) selected = true;
            if (selected) expanded.Add(node.Id);
            return selected;
        }

        private void DrawEditorRow(EditorRow row, int index, float viewWidth)
        {
            var rect = new Rect(0f, index * RowH, viewWidth, RowH);
            float x = rect.x + row.Indent * 12f;
            if (row.IsCategory)
            {
                DrawCategoryRow(row, rect, x, viewWidth);
                return;
            }
            DrawResourceRow(row, rect, x, viewWidth);
        }

        private void DrawCategoryRow(EditorRow row, Rect rect,
            float x, float viewWidth)
        {
            var arrowRect = new Rect(x, rect.y + 3f, 18f, 18f);
            GUI.DrawTexture(
                arrowRect, row.Expanded ? TexButton.Collapse : TexButton.Reveal);

            var checkboxRect = new Rect(
                x + 20f, rect.y + (RowH - CheckboxW) / 2f,
                CheckboxW, CheckboxW);
            bool adds = row.State != TriState.On;
            if (MultiCheckboxClicked(checkboxRect, row.State, adds))
            {
                ResourcePool? pool = ReadoutStore.Current?.Model.PoolById(builtPoolId);
                if (pool != null)
                {
                    List<string> newMembers = PoolTriState.ToggleCategoryScope(
                        pool.Members,
                        row.Id,
                        row.MatchingDefNames,
                        GameResourceCatalog.Instance);
                    ReadoutCommands.SetPoolMembers(
                        pool.Id, PoolMembersCodec.Encode(newMembers));
                }
            }

            var labelRect = new Rect(
                x + 20f + CheckboxW + 4f,
                rect.y,
                viewWidth - x - 20f - CheckboxW - 4f,
                RowH);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, row.Label);
            Text.Anchor = TextAnchor.UpperLeft;

            var arrowClickRect = new Rect(
                rect.x, rect.y, checkboxRect.x - rect.x, RowH);
            bool arrowClicked = Widgets.ButtonInvisible(arrowClickRect);
            bool labelClicked = Widgets.ButtonInvisible(labelRect);
            if (arrowClicked || labelClicked)
            {
                if (!expanded.Remove(row.Id)) expanded.Add(row.Id);
                expansionRevision++;
            }
        }

        private void DrawResourceRow(EditorRow row, Rect rect,
            float x, float viewWidth)
        {
            var checkboxRect = new Rect(
                x, rect.y + (RowH - CheckboxW) / 2f,
                CheckboxW, CheckboxW);
            bool adds = row.State != TriState.On;
            if (MultiCheckboxClicked(checkboxRect, row.State, adds))
            {
                ResourcePool? pool = ReadoutStore.Current?.Model.PoolById(builtPoolId);
                if (pool != null)
                {
                    List<string> newMembers = PoolTriState.ToggleDef(
                        pool.Members, row.DefName, GameResourceCatalog.Instance);
                    ReadoutCommands.SetPoolMembers(
                        pool.Id, PoolMembersCodec.Encode(newMembers));
                }
            }

            Widgets.ThingIcon(
                new Rect(x + CheckboxW + 4f, rect.y + 2f, 20f, 20f), row.Def);

            if (row.IsCurrentIcon) GUI.color = EprStyle.SelectionTint;
            Text.Anchor = TextAnchor.MiddleLeft;
            float labelX = x + CheckboxW + 26f;
            var labelRect = new Rect(
                labelX, rect.y, viewWidth - labelX, RowH);
            Widgets.Label(labelRect, row.Label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            var iconClickRect = new Rect(
                x + CheckboxW + 4f,
                rect.y + 2f,
                20f,
                20f);
            if (Widgets.ButtonInvisible(iconClickRect))
                ReadoutCommands.SetPoolIcon(builtPoolId, row.DefName);
        }

        internal void OnFilterChanged()
        {
            scroll.y = 0f;
        }

        internal void Reset()
        {
            builtStore = null;
            builtPoolsVersion = -1;
            builtPoolId = -1;
            builtFilterRevision = -1;
            builtExpansionRevision = -1;
            builtLanguageVersion = -1;
            cachedRows = null;
            expanded.Clear();
            expansionRevision = 0;
            scroll = Vector2.zero;
        }

        private static bool MultiCheckboxClicked(
            Rect rect, TriState state, bool adds)
        {
            Texture2D texture;
            if (state == TriState.On) texture = Widgets.CheckboxOnTex;
            else if (state == TriState.Off) texture = Widgets.CheckboxOffTex;
            else texture = Widgets.CheckboxPartialTex;

            if (!Widgets.ButtonImage(rect, texture)) return false;
            (adds
                ? SoundDefOf.Checkbox_TurnedOn
                : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
            return true;
        }
    }
}
