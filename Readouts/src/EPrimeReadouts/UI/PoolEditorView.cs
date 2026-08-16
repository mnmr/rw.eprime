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

        private Vector2 scroll;
        private readonly HashSet<string> expanded = new HashSet<string>();
        private int expandStamp;

        // Caching fields
        private int builtPoolsVersion = -1;
        private int builtPoolId = -1;
        private int builtExpandStamp = -1;
        private int builtLanguageVersion = -1;
        private ReadoutStore? builtStore;

        // Cache contract:
        // Owner: this dialog view and one ReadoutStore.
        // Key: store identity, pool id, PoolsVersion, expansion stamp, language revision.
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
        }

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            var store = ReadoutStore.Current;
            var settings = EPrimeReadoutsMod.Settings;
            if (store == null) return;

            // Section header with fold toggle
            bool folded = settings.helpPoolEditorFolded;
            float headerUsed = EprStyle.SectionHeader(rect.x, rect.y, rect.width,
                UiText.Get("EPR.ConfigurePool"), UiText.Get("EPR.HelpPoolEditor"), ref folded);
            if (folded != settings.helpPoolEditorFolded)
                EPrimeReadoutsMod.Persist(s => s.helpPoolEditorFolded = folded);

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
            var roots = GameResourceTree.GetRoots();

            // Selecting a different pool reveals its members: collapse the
            // whole tree, then expand every category that is Partial or On so
            // the selection is reachable by scrolling alone.
            if (poolChanged)
            {
                expanded.Clear();
                foreach (var root in roots)
                    ExpandSelected(root, members);
                expandStamp++;
            }
            builtExpandStamp = expandStamp;

            var builtRows = new List<EditorRow>();
            foreach (var root in roots)
                AddNode(root, 0, members, pool.IconDefName, builtRows);
            cachedRows = builtRows.ToArray();

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

        /// Recursively expands categories whose tri-state is Partial or On.
        private void ExpandSelected(ResourceTreeNode node, List<string> members)
        {
            var state = PoolTriState.CategoryState(members, node.Id, GameResourceCatalog.Instance);
            if (state != TriState.Off) expanded.Add(node.Id);
            foreach (var child in node.Children)
                ExpandSelected(child, members);
        }

        private void AddNode(ResourceTreeNode node, int indent, List<string> members,
            string? iconDefName, List<EditorRow> into)
        {
            bool open = expanded.Contains(node.Id);
            var state = PoolTriState.CategoryState(members, node.Id, GameResourceCatalog.Instance);

            into.Add(new EditorRow
            {
                IsCategory = true,
                Indent = indent,
                Id = node.Id,
                Label = node.Label,
                Expanded = open,
                State = state,
            });

            if (!open) return;

            foreach (var child in node.Children)
                AddNode(child, indent + 1, members, iconDefName, into);

            foreach (var defName in node.DefNames)
            {
                bool selected = PoolTriState.IsSelected(members, defName, GameResourceCatalog.Instance);
                var label = GameResourceCatalog.Instance.LabelOf(defName);
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null) continue;
                into.Add(new EditorRow
                {
                    IsCategory = false,
                    Indent = indent + 1,
                    DefName = defName,
                    Label = label,
                    State = selected ? TriState.On : TriState.Off,
                    Def = def,
                    IsCurrentIcon = iconDefName == defName,
                });
            }
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
                        var newMembers = PoolTriState.ToggleCategory(pool.Members, row.Id,
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
                    TooltipHandler.TipRegion(iconClickRect,
                        (TaggedString)UiText.Get("EPR.HelpPoolEditor"));
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
            scroll = Vector2.zero;
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
