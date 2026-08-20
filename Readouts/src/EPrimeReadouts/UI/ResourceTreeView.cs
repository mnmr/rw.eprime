using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// <summary>Virtualized resource picker backed by resolved cached rows.</summary>
    public sealed class ResourceTreeView
    {
        private const float RowH = 24f;
        private const string FilterControl = "EPR.ResourcePickerFilter";

        private Vector2 scroll;
        private readonly ItemPickerState filterState = new ItemPickerState();
        private readonly HashSet<string> expanded = new HashSet<string>();
        private readonly Action filterChanged;
        private int stamp;

        public ResourceTreeView()
        {
            filterChanged = OnFilterChanged;
        }

        private struct RenderRow
        {
            internal TreeRow Row;
            internal ThingDef? Def;
            internal bool InGroup;
            internal bool Tinted;
            internal int GroupId;
        }

        // Cache contract:
        // Owner: this dialog view and one ReadoutStore.
        // Key: store identity, GroupsVersion, expansion/query/type/source stamp, selected
        // group/token/pool, PoolSnapshot identity, and the language revision.
        // Value: immutable flattened rows with resolved defs and selection flags.
        // Dependencies: resource tree labels, group membership, pool membership.
        // Refresh policy: immediate on any dependency change.
        // Equality policy: unchanged dependencies preserve row-array identity.
        // Teardown: Reset drops all model/snapshot/def references on dialog close.
        private RenderRow[]? rows;
        private ReadoutStore? builtStore;
        private int builtGroupsVersion = -1;
        private int builtStamp = -1;
        private int builtGroupId = -1;
        private int builtPoolId = -1;
        private int builtLanguageVersion = -1;
        private string? builtCanonical;
        private PoolSnapshot? builtPools;

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            UiVersion.ObserveCurrentMetrics();
            var settings = EPrimeReadoutsMod.Settings;

            float headerUsed = EprStyle.SectionHeader(
                rect.x, rect.y, rect.width, UiText.Get("EPR.Resources"));

            bool folded = settings.helpResourcesFolded;
            headerUsed += EprStyle.HelpGroup(
                rect.x,
                rect.y + headerUsed,
                rect.width,
                UiText.Get("EPR.Help"),
                UiText.Get("EPR.HelpResources"),
                ref folded);
            if (folded != settings.helpResourcesFolded)
                EPrimeReadoutsMod.Persist(s => s.helpResourcesFolded = folded);

            ItemPickerFilterBar.Draw(
                new Rect(rect.x, rect.y + headerUsed, rect.width, 24f),
                filterState, FilterControl, filterChanged);

            EnsureRows(owner);
            float listH = rect.height - headerUsed - ItemPickerFilterBar.Height;
            if (listH <= 0f) return;
            var outRect = new Rect(rect.x,
                rect.y + headerUsed + ItemPickerFilterBar.Height, rect.width, listH);
            if (rows!.Length == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = EprStyle.CaptionText;
                Widgets.Label(outRect, UiText.Get("EPR.NoMatchingItems"));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, rows!.Length * RowH); // built by EnsureRows above
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            try
            {
                var visible = UniformViewportRange.Calculate(
                    rows.Length, RowH, 0f, scroll.y, outRect.height);
                for (int i = visible.Start; i < visible.EndExclusive; i++)
                    DrawRow(rows[i], new Rect(0f, i * RowH, viewRect.width, RowH), owner);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawRow(RenderRow data, Rect rect, Dialog_ReadoutConfig owner)
        {
            TreeRow row = data.Row;
            float x = rect.x + row.Indent * 12f;
            if (row.IsCategory)
            {
                var arrowRect = new Rect(x, rect.y + 3f, 18f, 18f);
                GUI.DrawTexture(arrowRect, row.Expanded ? TexButton.Collapse : TexButton.Reveal);
                if (data.Tinted) GUI.color = EprStyle.SelectionTint;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(x + 22f, rect.y,
                    rect.width - x - 22f, rect.height), row.Label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(rect))
                {
                    if (!expanded.Remove(row.Id)) expanded.Add(row.Id);
                    stamp++;
                }
                return;
            }

            if (data.Def == null) return;
            if (data.InGroup) GUI.color = new Color(1f, 1f, 1f, 0.4f);
            Widgets.ThingIcon(new Rect(x, rect.y + 2f, 20f, 20f), data.Def);
            Text.Anchor = TextAnchor.MiddleLeft;
            if (!data.InGroup && data.Tinted) GUI.color = EprStyle.SelectionTint;
            Widgets.Label(new Rect(x + 24f, rect.y,
                rect.width - x - 44f, rect.height), row.Label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (data.InGroup)
            {
                var checkRect = new Rect(rect.xMax - 20f, rect.y + 3f, 18f, 18f);
                GUI.DrawTexture(checkRect, Widgets.CheckboxOnTex);
                if (Event.current.type == EventType.MouseDown
                    && rect.Contains(Event.current.mousePosition)
                    && (Event.current.button == 1
                        || (Event.current.button == 0
                            && checkRect.Contains(Event.current.mousePosition))))
                {
                    var group = ReadoutStore.Current?.Model.GroupById(data.GroupId);
                    if (group != null)
                    {
                        var tiers = TierOps.Clone(group.Tiers);
                        if (TierOps.Remove(tiers, row.DefName))
                            ReadoutCommands.SetGroupLayout(group.Id, TierBlobCodec.Encode(tiers));
                    }
                    if (owner.selectedCanonical == row.DefName)
                        owner.selectedCanonical = null;
                    Event.current.Use();
                }
                return;
            }
            if (data.GroupId < 0) return;
            if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);

            int rowControlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            EprDrag.ObserveSource(rowControlId, rect);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && rect.Contains(Event.current.mousePosition))
            {
                int groupId = data.GroupId;
                string defName = row.DefName;
                EprDrag.OnPressToken(rowControlId, defName, -1, -1, () =>
                {
                    var group = ReadoutStore.Current?.Model.GroupById(groupId);
                    if (group == null) return;
                    var tiers = TierOps.Clone(group.Tiers);
                    int tier = tiers.Count == 0 ? 0 : tiers.Count - 1;
                    if (tier < tiers.Count
                        && tiers[tier].Count >= TierOps.MaxSlotsPerTier)
                        tier++;
                    if (TierOps.Add(tiers, defName, tier, -1))
                    {
                        ReadoutCommands.SetGroupLayout(groupId, TierBlobCodec.Encode(tiers));
                        owner.selectedCanonical = defName;
                    }
                });
                Event.current.Use();
            }
        }

        private void EnsureRows(Dialog_ReadoutConfig owner)
        {
            ReadoutStore? store = ReadoutStore.Current;
            int groupsVersion = store?.GroupsVersion ?? -1;
            if (rows != null
                && ReferenceEquals(store, builtStore)
                && groupsVersion == builtGroupsVersion
                && stamp == builtStamp
                && owner.selectedGroupId == builtGroupId
                && owner.selectedPoolId == builtPoolId
                && string.Equals(owner.selectedCanonical, builtCanonical,
                    StringComparison.Ordinal)
                && ReferenceEquals(owner.PoolsSnapshot, builtPools)
                && UiVersion.LanguageCurrent == builtLanguageVersion)
                return;

            var flat = ResourceTreeFlattener.Flatten(
                GameResourceTree.GetRoots(filterState.Type), expanded,
                new ItemTreeFilter(filterState.Query, filterState.Type, filterState.SourceId),
                GameResourceCatalog.Instance);
            ReadoutGroup? selected = store?.Model.GroupById(owner.selectedGroupId);
            IReadOnlyList<string>? selectedPoolMembers = null;
            if (owner.selectedPoolId >= 0 && owner.PoolsSnapshot != null)
                owner.PoolsSnapshot.TryGet(owner.selectedPoolId,
                    out selectedPoolMembers, out _, out _);

            var built = new RenderRow[flat.Count];
            for (int i = 0; i < flat.Count; i++)
            {
                TreeRow row = flat[i];
                var data = new RenderRow
                {
                    Row = row,
                    GroupId = selected?.Id ?? -1,
                };
                if (row.IsCategory)
                {
                    data.Tinted = IsCategoryTinted(row.Id, owner.selectedCanonical);
                }
                else
                {
                    data.Def = DefDatabase<ThingDef>.GetNamedSilentFail(row.DefName);
                    data.InGroup = selected != null
                        && TierOps.Contains(selected.Tiers, row.DefName);
                    data.Tinted = IsResourceTinted(row.DefName, owner.selectedCanonical)
                        || Contains(selectedPoolMembers, row.DefName);
                }
                built[i] = data;
            }

            rows = built;
            builtStore = store;
            builtGroupsVersion = groupsVersion;
            builtStamp = stamp;
            builtGroupId = owner.selectedGroupId;
            builtPoolId = owner.selectedPoolId;
            builtCanonical = owner.selectedCanonical;
            builtPools = owner.PoolsSnapshot;
            builtLanguageVersion = UiVersion.LanguageCurrent;
        }

        private static bool IsResourceTinted(string defName, string? canonical)
        {
            if (canonical == null || SlotToken.IsPool(canonical)) return false;
            return defName == SlotToken.MemberName(canonical);
        }

        private static bool IsCategoryTinted(string categoryId, string? canonical)
        {
            if (canonical == null || SlotToken.IsPool(canonical)) return false;
            string memberName = SlotToken.MemberName(canonical);
            var members = GameResourceCatalog.Instance.CountedDefsIn(categoryId);
            for (int i = 0; i < members.Count; i++)
                if (members[i] == memberName) return true;
            return false;
        }

        private static bool Contains(IReadOnlyList<string>? members, string defName)
        {
            if (members == null) return false;
            for (int i = 0; i < members.Count; i++)
                if (members[i] == defName) return true;
            return false;
        }

        internal void Reset()
        {
            rows = null;
            builtStore = null;
            builtGroupsVersion = -1;
            builtStamp = -1;
            builtGroupId = -1;
            builtPoolId = -1;
            builtLanguageVersion = -1;
            builtCanonical = null;
            builtPools = null;
            expanded.Clear();
            stamp = 0;
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
            stamp++;
            scroll.y = 0f;
        }
    }
}
