using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace EPrimeReadouts.UI
{
    /// Right-side bottom panel (visible when a pool is selected): tri-state
    /// category/resource picker tree for configuring pool members.
    public sealed class PoolEditorView
    {
        private const float RowH = 24f;
        private const float CheckboxW = 24f;
        private const string FilterControl = "EPR.PoolPickerFilter";

        private Vector2 scroll;
        private readonly ItemPickerState filterState = new ItemPickerState();
        private readonly HashSet<string> expanded = new HashSet<string>();
        private readonly System.Action filterChanged;
        private int expandStamp;

        public PoolEditorView()
        {
            filterChanged = OnFilterChanged;
        }

        // Caching fields
        private int builtPoolsVersion = -1;
        private int builtPoolId = -1;
        private int builtExpandStamp = -1;
        private int builtLanguageVersion = -1;
        private ReadoutStore? builtStore;

        // Cache contract:
        // Owner: this dialog view and one ReadoutStore.
        // Key: store identity, pool id, PoolsVersion, expansion/query/type/source
        // stamp, and language revision.
        // Value: immutable flattened editor rows with resolved ThingDefs/state.
        // Dependencies: selected pool raw members/icon, tree expansion and language.
        // Refresh policy: immediate on any dependency change.
        // Equality policy: unchanged dependencies preserve row-array identity.
        // Teardown: Reset releases rows/store/def references on dialog close.
        private EditorRow[]? cachedRows;

        private struct EditorRow
        {
            public bool IsCategory;
            public int Indent;
            public string Id;        // category defName (categories)
            public string DefName;   // resource defName (defs)
            public string Label;
            public bool Expanded;    // categories
            public TriState State;   // derived from pool members
            public ThingDef Def;
            public bool IsCurrentIcon;
            public IReadOnlyList<string> MatchingDefNames;
        }

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;

            // This explanatory caption remains permanently attached to the
            // Configure Resource Pool section; it has no separate Help foldout.
            bool folded = false;
            float headerUsed = EprStyle.SectionHeader(rect.x, rect.y, rect.width,
                UiText.Get("EPR.ConfigurePool"), UiText.Get("EPR.HelpPoolEditor"),
                ref folded, foldable: false);

            ItemPickerFilterBar.Draw(
                new Rect(rect.x, rect.y + headerUsed, rect.width, 24f),
                filterState, FilterControl, filterChanged);
            headerUsed += ItemPickerFilterBar.Height;

            // Rebuild cached rows when needed
            if (NeedsRebuild(store, owner.selectedPoolId))
                Rebuild(store, owner.selectedPoolId);
            if (cachedRows == null || builtPoolId < 0) return;

            int rowCount = cachedRows.Length;

            // The panel can be squeezed to nothing (small dialog, unfolded
            // captions); a non-positive viewport must not reach Calculate.
            float listH = rect.height - headerUsed;
            if (listH <= 0f) return;
            var outRect = new Rect(rect.x, rect.y + headerUsed, rect.width, listH);
            if (rowCount == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = EprStyle.CaptionText;
                Widgets.Label(outRect, UiText.Get("EPR.NoMatchingItems"));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, rowCount * RowH);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            try
            {
            var vr = UniformViewportRange.Calculate(rowCount, RowH, 0f, scroll.y, outRect.height);
            for (int i = vr.Start; i < vr.EndExclusive; i++)
                DrawEditorRow(cachedRows[i], i, viewRect.width);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private bool NeedsRebuild(ReadoutStore store, int poolId)
        {
            if (cachedRows == null) return true;
            if (!ReferenceEquals(store, builtStore)) return true;
            if (store.PoolsVersion != builtPoolsVersion) return true;
            if (poolId != builtPoolId) return true;
            if (expandStamp != builtExpandStamp) return true;
            if (UiVersion.LanguageCurrent != builtLanguageVersion) return true;
            return false;
        }

        private void Rebuild(ReadoutStore store, int poolId)
        {
            ResourcePool? pool = store.Model.PoolById(poolId);
            bool poolChanged = poolId != builtPoolId;
            builtStore = store;
            builtPoolsVersion = store.PoolsVersion;
            builtPoolId = poolId;
            builtLanguageVersion = UiVersion.LanguageCurrent;
            if (pool == null)
            {
                cachedRows = System.Array.Empty<EditorRow>();
                return;
            }

            var members = pool.Members;
            var roots = GameResourceTree.GetRoots(filterState.Type);
            if (poolChanged)
            {
                expanded.Clear();
                foreach (var root in roots)
                    ExpandSelected(root, members);
                expandStamp++;
            }
            builtExpandStamp = expandStamp;

            var flat = ResourceTreeFlattener.Flatten(
                roots, expanded,
                new ItemTreeFilter(filterState.Query, filterState.Type, filterState.SourceId),
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
                            members, row.MatchingDefNames, GameResourceCatalog.Instance),
                    };
                }
                else
                {
                    ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(row.DefName);
                    builtRows[i] = new EditorRow
                    {
                        Indent = row.Indent,
                        DefName = row.DefName,
                        Label = row.Label,
                        State = PoolTriState.IsSelected(
                            members, row.DefName, GameResourceCatalog.Instance)
                                ? TriState.On : TriState.Off,
                        Def = def,
                        IsCurrentIcon = pool.IconDefName == row.DefName,
                    };
                }
            }
            cachedRows = builtRows;

            // Scroll so the first partially selected category sits at the top
            // of the view (fallback: first fully selected one; else the top).
            if (poolChanged)
            {
                int firstPartial = -1, firstOn = -1;
                for (int i = 0; i < cachedRows.Length; i++)
                {
                    if (!cachedRows[i].IsCategory) continue;
                    if (cachedRows[i].State == TriState.Partial) { firstPartial = i; break; }
                    if (firstOn < 0 && cachedRows[i].State == TriState.On) firstOn = i;
                }
                int target = firstPartial >= 0 ? firstPartial : firstOn;
                scroll.y = target > 0 ? target * RowH : 0f;
            }
        }

        /// Preserves the established pool-switch behavior: reveal every
        /// branch containing a selected member, including ancestors in ASI's
        /// broader storage hierarchy.
        private bool ExpandSelected(ResourceTreeNode node, List<string> members)
        {
            bool selected = PoolTriState.CategoryState(
                members, node.Id, GameResourceCatalog.Instance) != TriState.Off;
            foreach (var child in node.Children)
                if (ExpandSelected(child, members)) selected = true;
            if (selected) expanded.Add(node.Id);
            return selected;
        }

        private void DrawEditorRow(EditorRow row, int index, float viewW)
        {
            var rect = new Rect(0f, index * RowH, viewW, RowH);
            float x = rect.x + row.Indent * 12f;

            if (row.IsCategory)
            {
                // Expand arrow
                var arrowRect = new Rect(x, rect.y + 3f, 18f, 18f);
                GUI.DrawTexture(arrowRect, row.Expanded ? TexButton.Collapse : TexButton.Reveal);

                // Tri-state checkbox
                var cbRect = new Rect(x + 20f, rect.y + (RowH - CheckboxW) / 2f, CheckboxW, CheckboxW);
                bool adds = row.State != TriState.On;
                if (MultiCheckboxClicked(cbRect, row.State, adds))
                {
                    var pool = ReadoutStore.Current?.Model.PoolById(builtPoolId);
                    if (pool != null)
                    {
                        var newMembers = PoolTriState.ToggleCategoryScope(
                            pool.Members, row.Id, row.MatchingDefNames,
                            GameResourceCatalog.Instance);
                        ReadoutCommands.SetPoolMembers(pool.Id, PoolMembersCodec.Encode(newMembers));
                    }
                }

                // Label (clickable to expand/collapse)
                var labelRect = new Rect(x + 20f + CheckboxW + 4f, rect.y,
                    viewW - x - 20f - CheckboxW - 4f, RowH);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, row.Label);
                Text.Anchor = TextAnchor.UpperLeft;

                // Arrow or label click → expand/collapse (exclude checkbox area)
                var clickRect = new Rect(rect.x, rect.y, cbRect.x - rect.x, RowH);
                if (Widgets.ButtonInvisible(clickRect))
                {
                    if (!expanded.Remove(row.Id)) expanded.Add(row.Id);
                    expandStamp++;
                }
                var labelClickRect = new Rect(labelRect.x, labelRect.y, labelRect.width, labelRect.height);
                if (Widgets.ButtonInvisible(labelClickRect))
                {
                    if (!expanded.Remove(row.Id)) expanded.Add(row.Id);
                    expandStamp++;
                }
            }
            else
            {
                // Resource row
                // Tri-state checkbox
                var cbRect = new Rect(x, rect.y + (RowH - CheckboxW) / 2f, CheckboxW, CheckboxW);
                bool adds = row.State != TriState.On;
                if (MultiCheckboxClicked(cbRect, row.State, adds))
                {
                    var pool = ReadoutStore.Current?.Model.PoolById(builtPoolId);
                    if (pool != null)
                    {
                        var newMembers = PoolTriState.ToggleDef(pool.Members, row.DefName,
                            GameResourceCatalog.Instance);
                        ReadoutCommands.SetPoolMembers(pool.Id, PoolMembersCodec.Encode(newMembers));
                    }
                }

                // Icon
                Widgets.ThingIcon(new Rect(x + CheckboxW + 4f, rect.y + 2f, 20f, 20f), row.Def);

                // Label — clicking sets pool icon; tint when this IS the current explicit icon
                if (row.IsCurrentIcon) GUI.color = EprStyle.SelectionTint;
                Text.Anchor = TextAnchor.MiddleLeft;
                float labelX = x + CheckboxW + 4f + 22f;
                var labelRect = new Rect(labelX, rect.y, viewW - labelX, RowH);
                Widgets.Label(labelRect, row.Label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;

                // Icon area + label area: clicking sets pool icon
                var iconClickRect = new Rect(x + CheckboxW + 4f, rect.y, viewW - x - CheckboxW - 4f, RowH);
                if (Widgets.ButtonInvisible(iconClickRect))
                    ReadoutCommands.SetPoolIcon(builtPoolId, row.DefName);

                if (Mouse.IsOver(iconClickRect))
                    WrTips.Key("EPR.HelpPoolEditor").Region(iconClickRect);
            }
        }

        internal void Reset()
        {
            builtStore = null;
            builtPoolsVersion = -1;
            builtPoolId = -1;
            builtExpandStamp = -1;
            builtLanguageVersion = -1;
            cachedRows = null;
            expanded.Clear();
            expandStamp = 0;
            filterState.Query = "";
            filterState.Type = ItemPickerType.Resources;
            filterState.SourceId = ItemSourceIds.All;
            scroll = Vector2.zero;
        }

        internal bool HandleEscape() => DialogInputFocus.TryHandleEscape(
            FilterControl, filterState.Query, () =>
            {
                filterState.Query = "";
                OnFilterChanged();
            });

        internal void Unfocus() => DialogInputFocus.Unfocus(FilterControl);

        private void OnFilterChanged()
        {
            expandStamp++;
            scroll.y = 0f;
        }

        /// WorkRoles-style MultiCheckboxClicked: draws the appropriate checkbox
        /// texture and returns true on click, playing the correct sound.
        private static bool MultiCheckboxClicked(Rect rect, TriState state, bool adds)
        {
            Texture2D tex;
            if (state == TriState.On) tex = Widgets.CheckboxOnTex;
            else if (state == TriState.Off) tex = Widgets.CheckboxOffTex;
            else tex = Widgets.CheckboxPartialTex;

            if (!Widgets.ButtonImage(rect, tex)) return false;
            (adds ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
            return true;
        }
    }
}
