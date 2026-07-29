using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Center panel: the vanilla resource category tree with all counted
    /// resources. Rows are flattened by Core (cached) and drawn virtualized.
    public sealed class ResourceTreeView
    {
        private const float RowH = 24f;
        private const float FilterH = 28f;

        /// Right-side width the dialog reserves in the header strip (toggle
        /// button); the fold-clickable region shrinks to stay clear of it.
        public float HeaderReservedRight;

        private Vector2 scroll;
        private string filter = "";
        private readonly HashSet<string> expanded = new HashSet<string>();
        private List<TreeRow> rows;
        private int stamp;
        private int builtStamp = -1;

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            var settings = EPrimeReadoutsMod.Settings;

            // Section header with fold toggle
            bool folded = settings.helpResourcesFolded;
            float clickableW = HeaderReservedRight > 0f
                ? rect.width - HeaderReservedRight : -1f;
            float headerUsed = EprStyle.SectionHeader(rect.x, rect.y, rect.width,
                "EPR.Resources".Translate(), "EPR.HelpResources".Translate(), ref folded,
                clickableW);
            if (folded != settings.helpResourcesFolded)
                EPrimeReadoutsMod.Persist(s => s.helpResourcesFolded = folded);

            // Filter box below header
            var filterRect = new Rect(rect.x, rect.y + headerUsed, rect.width - 20f, 24f);
            string newFilter = Widgets.TextField(filterRect, filter);
            if (newFilter != filter)
            {
                filter = newFilter;
                stamp++;
            }
            if (!filter.NullOrEmpty()
                && Widgets.ButtonImage(new Rect(rect.xMax - 18f, rect.y + headerUsed + 4f, 16f, 16f),
                    TexButton.CloseXSmall))
            {
                filter = "";
                stamp++;
            }

            EnsureRows();

            // Guard against a squeezed-to-nothing panel (Calculate rejects a
            // non-positive viewport extent).
            float listH = rect.height - headerUsed - FilterH;
            if (listH <= 0f) return;
            var outRect = new Rect(rect.x, rect.y + headerUsed + FilterH, rect.width, listH);
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, rows.Count * RowH);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            var visible = UniformViewportRange.Calculate(rows.Count, RowH, 0f, scroll.y, outRect.height);
            for (int i = visible.Start; i < visible.EndExclusive; i++)
                DrawRow(rows[i], new Rect(0f, i * RowH, viewRect.width, RowH), owner);
            Widgets.EndScrollView();
        }

        private void DrawRow(TreeRow row, Rect rect, Dialog_ReadoutConfig owner)
        {
            float x = rect.x + row.Indent * 12f;
            if (row.IsCategory)
            {
                var arrowRect = new Rect(x, rect.y + 3f, 18f, 18f);
                GUI.DrawTexture(arrowRect, row.Expanded ? TexButton.Collapse : TexButton.Reveal);

                // Check if category row should be tinted (selected slot is in this category)
                bool categoryTinted = IsCategoryTinted(row.Id, owner);
                if (categoryTinted) GUI.color = EprStyle.SelectionTint;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(x + 22f, rect.y, rect.width - x - 22f, rect.height), row.Label);
                Text.Anchor = TextAnchor.UpperLeft;
                if (categoryTinted) GUI.color = Color.white;

                // Plain browser: no pool copy-icon buttons; categories only expand/collapse.
                if (Widgets.ButtonInvisible(rect))
                {
                    if (!expanded.Remove(row.Id)) expanded.Add(row.Id);
                    stamp++;
                }
                return;
            }

            var def = DefDatabase<ThingDef>.GetNamedSilentFail(row.DefName);
            if (def == null) return;
            var selected = owner.SelectedGroup;
            bool inGroup = selected != null && TierOps.Contains(selected.Tiers, row.DefName);

            // Selection tint on label when this row matches the selected slot
            bool rowTinted = IsResourceTinted(row.DefName, owner);

            // Tint when def is a member of the selected pool (owner.selectedPoolId)
            if (!rowTinted && owner.selectedPoolId >= 0 && owner.PoolsSnapshot != null)
            {
                owner.PoolsSnapshot.TryGet(owner.selectedPoolId, out var poolMembers, out _, out _);
                if (poolMembers != null)
                    for (int m = 0; m < poolMembers.Count; m++)
                        if (poolMembers[m] == row.DefName) { rowTinted = true; break; }
            }

            if (inGroup) GUI.color = new Color(1f, 1f, 1f, 0.4f);
            Widgets.ThingIcon(new Rect(x, rect.y + 2f, 20f, 20f), def);
            Text.Anchor = TextAnchor.MiddleLeft;
            if (!inGroup && rowTinted) GUI.color = EprStyle.SelectionTint;
            Widgets.Label(new Rect(x + 24f, rect.y, rect.width - x - 44f, rect.height), def.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (inGroup)
            {
                var checkRect = new Rect(rect.xMax - 20f, rect.y + 3f, 18f, 18f);
                GUI.DrawTexture(checkRect, Widgets.CheckboxOnTex);
                // Remove via: shift-click anywhere on the row, right-click
                // anywhere on the row, or a plain click on the checkmark.
                if (selected != null
                    && Event.current.type == EventType.MouseDown
                    && rect.Contains(Event.current.mousePosition)
                    && (Event.current.button == 1
                        || (Event.current.button == 0
                            && (Event.current.shift
                                || checkRect.Contains(Event.current.mousePosition)))))
                {
                    var tiers = TierOps.Clone(selected.Tiers);
                    if (TierOps.Remove(tiers, row.DefName))
                        ReadoutCommands.SetGroupLayout(selected.Id, TierBlobCodec.Encode(tiers));
                    if (owner.selectedCanonical == row.DefName) owner.selectedCanonical = null;
                    Event.current.Use();
                }
                return;
            }
            if (selected == null) return;
            if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);

            int rowControlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            EprDrag.ObserveSource(rowControlId, rect);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && !Event.current.shift && rect.Contains(Event.current.mousePosition))
            {
                int groupId = selected.Id;
                string defName = row.DefName;
                EprDrag.OnPressToken(rowControlId, defName, -1, -1, () =>
                {
                    // Click: append to last tier that has items (tier 0 if empty)
                    var g = ReadoutStore.Current?.Model.GroupById(groupId);
                    if (g == null) return;
                    var tiers = TierOps.Clone(g.Tiers);
                    int tier = tiers.Count == 0 ? 0 : tiers.Count - 1;
                    if (TierOps.Add(tiers, defName, tier, -1))
                    {
                        ReadoutCommands.SetGroupLayout(groupId, TierBlobCodec.Encode(tiers));
                        // Auto-select the added resource so the Options panel opens.
                        owner.selectedCanonical = defName;
                    }
                });
                Event.current.Use();
            }
        }

        /// Returns true if the resource row's def should receive a selection tint.
        /// Non-pool selection: tint when defName matches member name of selected canonical.
        private static bool IsResourceTinted(string defName, Dialog_ReadoutConfig owner)
        {
            if (owner.selectedCanonical == null) return false;
            if (SlotToken.IsPool(owner.selectedCanonical)) return false;
            return defName == SlotToken.MemberName(owner.selectedCanonical);
        }

        /// Returns true if a category row should receive a selection tint.
        /// Tint when: selected canonical (non-pool member) is a counted def within this category.
        private static bool IsCategoryTinted(string categoryId, Dialog_ReadoutConfig owner)
        {
            if (owner.selectedCanonical == null) return false;
            if (SlotToken.IsPool(owner.selectedCanonical)) return false;
            string memberName = SlotToken.MemberName(owner.selectedCanonical);
            var members = GameResourceCatalog.Instance.CountedDefsIn(categoryId);
            for (int i = 0; i < members.Count; i++)
                if (members[i] == memberName) return true;
            return false;
        }

        private void EnsureRows()
        {
            var roots = GameResourceTree.GetRoots();
            if (rows != null && builtStamp == stamp) return;
            rows = ResourceTreeFlattener.Flatten(roots, expanded, filter, GameResourceCatalog.Instance);
            builtStamp = stamp;
        }
    }
}
