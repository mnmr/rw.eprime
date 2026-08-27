using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Cached resource and resource-pool tree used to assign group tokens.
    internal sealed class GroupResourceTreeBody
    {
        private const float RowH = 24f;

        private Vector2 scroll;
        private readonly HashSet<string> expanded = new HashSet<string>();
        private bool poolExpanded;
        private int expansionRevision;

        private struct ResourceRenderRow
        {
            internal TreeRow Row;
            internal ThingDef? Def;
            internal bool InGroup;
            internal bool Tinted;
            internal int GroupId;
        }

        private struct PoolRenderRow
        {
            internal PoolAssignmentTreeRow Row;
            internal ThingDef? IconDef;
            internal bool InGroup;
            internal bool Selected;
            /// Root only: the selected slot token is a pool, so the collapsed
            /// branch shows the same containment highlight categories use.
            internal bool Tinted;
            internal int GroupId;
        }

        // Cache contract:
        // Owner: this dialog body and one ReadoutStore.
        // Key: store identity.
        // Value: immutable flattened resource and pool rows with resolved defs.
        // Dependencies: GroupsVersion, selected group/token, pool snapshot
        // identity, shared filter revision, expansion revision, and language.
        // Refresh policy: immediate when visible after a dependency change.
        // Equality policy: unchanged dependencies preserve row-array identity.
        // Teardown: Reset releases store/snapshot/row/def references.
        private ResourceRenderRow[]? resourceRows;
        private PoolRenderRow[]? poolRows;
        private ReadoutStore? builtStore;
        private int builtGroupsVersion = -1;
        private int builtFilterRevision = -1;
        private int builtExpansionRevision = -1;
        private int builtGroupId = -1;
        private int builtLanguageVersion = -1;
        private string? builtCanonical;
        private PoolSnapshot? builtPools;

        internal bool Draw(Rect rect, Dialog_ReadoutConfig owner,
            ItemPickerState filters, int filterRevision)
        {
            EnsureRows(owner, filters, filterRevision);
            int poolCount = poolRows?.Length ?? 0;
            int resourceCount = resourceRows?.Length ?? 0;
            int rowCount = poolCount + resourceCount;
            if (rowCount == 0) return false;

            var viewRect = new Rect(0f, 0f, rect.width - 16f, rowCount * RowH);
            Widgets.BeginScrollView(rect, ref scroll, viewRect);
            try
            {
                var visible = UniformViewportRange.Calculate(
                    rowCount, RowH, 0f, scroll.y, rect.height);
                for (int i = visible.Start; i < visible.EndExclusive; i++)
                {
                    var rowRect = new Rect(0f, i * RowH, viewRect.width, RowH);
                    if (i < poolCount)
                        DrawPoolRow(poolRows![i], rowRect, owner);
                    else
                        DrawResourceRow(resourceRows![i - poolCount], rowRect, owner);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
            return true;
        }

        private void DrawPoolRow(PoolRenderRow data, Rect rect,
            Dialog_ReadoutConfig owner)
        {
            PoolAssignmentTreeRow row = data.Row;
            if (row.IsRoot)
            {
                var arrowRect = new Rect(rect.x, rect.y + 3f, 18f, 18f);
                GUI.DrawTexture(
                    arrowRect, row.Expanded ? TexButton.Collapse : TexButton.Reveal);
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = data.Tinted
                    ? EprStyle.SelectionTint : EprStyle.PoolTint;
                Widgets.Label(new Rect(
                    rect.x + 22f, rect.y, rect.width - 22f, rect.height), row.Label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(rect))
                {
                    poolExpanded = !poolExpanded;
                    expansionRevision++;
                }
                return;
            }

            float x = rect.x + 12f;
            if (data.Selected) Widgets.DrawHighlightSelected(rect);
            else if (!data.InGroup && Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);

            if (data.InGroup) GUI.color = EprStyle.AssignedTint;
            if (data.IconDef != null)
                Widgets.ThingIcon(new Rect(x, rect.y + 2f, 20f, 20f), data.IconDef);
            Text.Anchor = TextAnchor.MiddleLeft;
            if (!data.InGroup && data.Selected) GUI.color = EprStyle.SelectionTint;
            Widgets.Label(new Rect(
                x + 24f, rect.y, rect.width - x - 44f, rect.height), row.Label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (data.InGroup)
            {
                DrawAssignedRemoval(rect, data.GroupId, row.Token, owner);
                return;
            }
            if (data.GroupId < 0) return;

            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            EprDrag.ObserveSource(controlId, rect);
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && rect.Contains(Event.current.mousePosition))
            {
                int groupId = data.GroupId;
                string token = row.Token;
                EprDrag.OnPressToken(
                    controlId, token, -1, -1,
                    () => AppendToken(groupId, token, owner));
                Event.current.Use();
            }
        }

        private void DrawResourceRow(ResourceRenderRow data, Rect rect,
            Dialog_ReadoutConfig owner)
        {
            TreeRow row = data.Row;
            float x = rect.x + row.Indent * 12f;
            if (row.IsCategory)
            {
                var arrowRect = new Rect(x, rect.y + 3f, 18f, 18f);
                GUI.DrawTexture(
                    arrowRect, row.Expanded ? TexButton.Collapse : TexButton.Reveal);
                if (data.Tinted) GUI.color = EprStyle.SelectionTint;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(
                    x + 22f, rect.y, rect.width - x - 22f, rect.height), row.Label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(rect))
                {
                    if (!expanded.Remove(row.Id)) expanded.Add(row.Id);
                    expansionRevision++;
                }
                return;
            }

            if (data.Def == null) return;
            if (data.InGroup) GUI.color = EprStyle.AssignedTint;
            Widgets.ThingIcon(new Rect(x, rect.y + 2f, 20f, 20f), data.Def);
            Text.Anchor = TextAnchor.MiddleLeft;
            if (!data.InGroup && data.Tinted) GUI.color = EprStyle.SelectionTint;
            Widgets.Label(new Rect(
                x + 24f, rect.y, rect.width - x - 44f, rect.height), row.Label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (data.InGroup)
            {
                DrawAssignedRemoval(rect, data.GroupId, row.DefName, owner);
                return;
            }
            if (data.GroupId < 0) return;
            if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);

            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            EprDrag.ObserveSource(controlId, rect);
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && rect.Contains(Event.current.mousePosition))
            {
                int groupId = data.GroupId;
                string defName = row.DefName;
                EprDrag.OnPressToken(
                    controlId, defName, -1, -1,
                    () => AppendToken(groupId, defName, owner));
                Event.current.Use();
            }
        }

        private static void DrawAssignedRemoval(Rect rect, int groupId,
            string token, Dialog_ReadoutConfig owner)
        {
            var checkRect = new Rect(rect.xMax - 20f, rect.y + 3f, 18f, 18f);
            GUI.DrawTexture(checkRect, Widgets.CheckboxOnTex);
            if (Event.current.type != EventType.MouseDown
                || !rect.Contains(Event.current.mousePosition)
                || (Event.current.button != 1
                    && (Event.current.button != 0
                        || !checkRect.Contains(Event.current.mousePosition))))
                return;

            ReadoutGroup? group = ReadoutStore.Current?.Model.GroupById(groupId);
            if (group != null)
            {
                List<List<string>> tiers = TierOps.Clone(group.Tiers);
                if (TierOps.Remove(tiers, token))
                    ReadoutCommands.SetGroupLayout(
                        group.Id, TierBlobCodec.Encode(tiers));
            }
            if (string.Equals(
                owner.selectedCanonical,
                SlotToken.Canonical(token),
                StringComparison.Ordinal))
                owner.selectedCanonical = null;
            Event.current.Use();
        }

        private static void AppendToken(int groupId, string token,
            Dialog_ReadoutConfig owner)
        {
            ReadoutGroup? group = ReadoutStore.Current?.Model.GroupById(groupId);
            if (group == null) return;

            List<List<string>> tiers = TierOps.Clone(group.Tiers);
            int tier = tiers.Count == 0 ? 0 : tiers.Count - 1;
            if (tier < tiers.Count
                && tiers[tier].Count >= TierOps.MaxSlotsPerTier)
                tier++;
            if (!TierOps.Add(tiers, token, tier, -1)) return;

            ReadoutCommands.SetGroupLayout(groupId, TierBlobCodec.Encode(tiers));
            owner.selectedCanonical = SlotToken.Canonical(token);
        }

        private void EnsureRows(Dialog_ReadoutConfig owner,
            ItemPickerState filters, int filterRevision)
        {
            ReadoutStore? store = ReadoutStore.Current;
            int groupsVersion = store?.GroupsVersion ?? -1;
            if (resourceRows != null
                && poolRows != null
                && ReferenceEquals(store, builtStore)
                && groupsVersion == builtGroupsVersion
                && filterRevision == builtFilterRevision
                && expansionRevision == builtExpansionRevision
                && owner.selectedGroupId == builtGroupId
                && string.Equals(
                    owner.selectedCanonical, builtCanonical, StringComparison.Ordinal)
                && ReferenceEquals(owner.PoolsSnapshot, builtPools)
                && UiVersion.LanguageCurrent == builtLanguageVersion)
                return;

            ReadoutGroup? selected = store?.Model.GroupById(owner.selectedGroupId);
            List<TreeRow> flat = ResourceTreeFlattener.Flatten(
                GameResourceTree.GetRoots(filters.Type),
                expanded,
                new ItemTreeFilter(filters.Query, filters.Type, filters.SourceId),
                GameResourceCatalog.Instance);
            var builtResources = new ResourceRenderRow[flat.Count];
            for (int i = 0; i < flat.Count; i++)
            {
                TreeRow row = flat[i];
                var data = new ResourceRenderRow
                {
                    Row = row,
                    GroupId = selected?.Id ?? -1,
                };
                if (row.IsCategory)
                {
                    data.Tinted = IsCategoryTinted(
                        row.Id, owner.selectedCanonical);
                }
                else
                {
                    data.Def = DefDatabase<ThingDef>.GetNamedSilentFail(row.DefName);
                    data.InGroup = selected != null
                        && TierOps.Contains(selected.Tiers, row.DefName);
                    data.Tinted = IsResourceTinted(
                        row.DefName, owner.selectedCanonical);
                }
                builtResources[i] = data;
            }

            PoolAssignmentTreeRow[] logicalPools = PoolAssignmentTree.Build(
                owner.PoolsSnapshot,
                poolExpanded,
                new ItemTreeFilter(filters.Query, filters.Type, filters.SourceId),
                UiText.Get("EPR.Pools"));
            var builtPoolRows = new PoolRenderRow[logicalPools.Length];
            for (int i = 0; i < logicalPools.Length; i++)
            {
                PoolAssignmentTreeRow row = logicalPools[i];
                ThingDef? iconDef = !row.IsRoot && !string.IsNullOrEmpty(row.IconDefName)
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(row.IconDefName)
                    : null;
                builtPoolRows[i] = new PoolRenderRow
                {
                    Row = row,
                    IconDef = iconDef,
                    InGroup = !row.IsRoot && selected != null
                        && TierOps.Contains(selected.Tiers, row.Token),
                    Selected = !row.IsRoot && string.Equals(
                        owner.selectedCanonical,
                        row.Token,
                        StringComparison.Ordinal),
                    Tinted = row.IsRoot
                        && owner.selectedCanonical != null
                        && SlotToken.IsPoolRef(owner.selectedCanonical),
                    GroupId = selected?.Id ?? -1,
                };
            }

            resourceRows = builtResources;
            poolRows = builtPoolRows;
            builtStore = store;
            builtGroupsVersion = groupsVersion;
            builtFilterRevision = filterRevision;
            builtExpansionRevision = expansionRevision;
            builtGroupId = owner.selectedGroupId;
            builtCanonical = owner.selectedCanonical;
            builtPools = owner.PoolsSnapshot;
            builtLanguageVersion = UiVersion.LanguageCurrent;
        }

        private static bool IsResourceTinted(string defName, string? canonical)
        {
            if (canonical == null
                || SlotToken.IsPool(canonical)
                || SlotToken.IsPoolRef(canonical))
                return false;
            return defName == SlotToken.MemberName(canonical);
        }

        private static bool IsCategoryTinted(string categoryId, string? canonical)
        {
            if (canonical == null
                || SlotToken.IsPool(canonical)
                || SlotToken.IsPoolRef(canonical))
                return false;
            string memberName = SlotToken.MemberName(canonical);
            IReadOnlyList<string> members =
                GameResourceCatalog.Instance.CountedDefsIn(categoryId);
            for (int i = 0; i < members.Count; i++)
                if (members[i] == memberName) return true;
            return false;
        }

        internal void OnFilterChanged()
        {
            scroll.y = 0f;
        }

        internal void Reset()
        {
            resourceRows = null;
            poolRows = null;
            builtStore = null;
            builtGroupsVersion = -1;
            builtFilterRevision = -1;
            builtExpansionRevision = -1;
            builtGroupId = -1;
            builtLanguageVersion = -1;
            builtCanonical = null;
            builtPools = null;
            expanded.Clear();
            poolExpanded = false;
            expansionRevision = 0;
            scroll = Vector2.zero;
        }
    }
}
