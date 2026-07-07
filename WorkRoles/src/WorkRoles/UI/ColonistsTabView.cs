using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    public class ColonistsTabView
    {
        private Vector2 paletteScroll;
        private Vector2 tableScroll;
        private Pawn selectedPawn;

        // View-local table filters (never synced, never persisted).
        private string colonistFilter = "";
        private int roleFilterId = -1;

        private const float PaletteMaxHeight = 150f;   // palette scrolls beyond this
        private const float PalettePadding = 6f;
        private const float GroupLabelHeight = 22f;
        private const float PaletteColumnGap = 16f;
        private const float PaletteColumnMinW = 200f;
        private const float FilterRowH = 28f;
        private const float RowHeight = 36f;
        private const float PortraitSize = 30f;
        private const float NameWidth = 150f;
        private const float IconButton = 24f;
        private const float ChipGap = 4f;
        private const float StatsPanelMargin = 8f;
        private const float DefaultWidth = 1010f;
        private const float DefaultHeight = 684f;

        private const float PortraitDisplaySize = 96f;

        // Stats panel layout constants
        private const float SkillColWidth = 200f;   // two columns (reduced from 240)
        private const int   SkillCols = 2;
        private const float CellH = 20f;
        private const float StatsPadding = 12f;     // top+bottom padding inside box
        private const float ColSepWidth = 2f;       // separator width
        private const float ColSepMargin = 16f;     // space on each side of separator

        // Text colours for skill level
        private static readonly Color ColorDisabled   = new Color(0.45f, 0.45f, 0.45f);
        private static readonly Color ColorLow        = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color ColorPassMajor  = new Color(1f, 0.65f, 0.2f);
        private static readonly Color ColorPassMinor  = new Color(0.95f, 0.9f, 0.55f);

        // Recommendation cache (invalidated on pawn selection change and after assignment)
        private Pawn _recCachePawn;
        private List<Role> _recCached = new List<Role>();

        public void Reset()
        {
            paletteScroll = Vector2.zero;
            tableScroll = Vector2.zero;
            selectedPawn = null;
            colonistFilter = "";
            roleFilterId = -1;
            InvalidateRecommendationCache();
        }

        private void InvalidateRecommendationCache()
        {
            _recCachePawn = null;
            _recCached = new List<Role>();
        }

        // ----- Window sizing helpers -----

        /// <summary>Height of the stats panel for a given pawn (or generic if null).</summary>
        public static float StatsPanelHeight(Pawn pawn = null)
        {
            int lineCount = pawn != null ? SkillsTip.Lines(pawn).Count : 12;
            int rows = (lineCount + SkillCols - 1) / SkillCols;
            float portraitSection = PortraitDisplaySize + 2f + 20f; // portrait + gap + name label
            float skillSection = rows * CellH;
            float contentH = Mathf.Max(portraitSection, skillSection);
            return contentH + StatsPadding * 2f;
        }

        public static float DesiredWidth()
        {
            var store = RoleStore.Current;
            if (store == null || Find.CurrentMap == null) return DefaultWidth;

            // Fixed left columns: portrait | gap | name | gap | copy | gap | paste | gap | merge | gap | [+] | gap | trailing
            float fixedLeft = PortraitSize + 6f + NameWidth + 2f + IconButton + 2f + IconButton + 8f + IconButton + 2f + IconButton + 4f + 16f;
            float widestStrip = 0f;
            var pawns = ListedPawns();
            foreach (var pawn in pawns)
            {
                store.pawnSets.TryGetValue(pawn, out var set);
                var assignments = set?.assignments ?? new List<RoleAssignment>();
                // Measure all chips on a single line (desired width = single-line run)
                float w = 0f;
                foreach (var a in assignments)
                {
                    var role = store.RoleById(a.roleId);
                    if (role == null) continue;
                    w += RoleChipUI.WidthFor(role, showRemove: true) + ChipGap;
                }
                if (w > widestStrip) widestStrip = w;
            }
            return fixedLeft + widestStrip;
        }

        public static float DesiredHeight()
        {
            var store = RoleStore.Current;
            if (store == null || Find.CurrentMap == null) return DefaultHeight;

            float chrome = 80f;
            float paletteSection = PaletteHeight(store, DesiredWidth() - 16f) + 8f + FilterRowH + 4f;
            float statsPanel = StatsPanelHeight() + StatsPanelMargin;
            float tableContent = 0f;
            var pawns = ListedPawns();
            foreach (var pawn in pawns)
            {
                store.pawnSets.TryGetValue(pawn, out var set);
                var assignments = set?.assignments ?? new List<RoleAssignment>();
                float stripW = DefaultWidth - 250f;
                float stripH = MeasureStripHeight(stripW, assignments, store);
                tableContent += Mathf.Max(RowHeight, stripH + 8f);
            }
            return chrome + paletteSection + tableContent + statsPanel;
        }

        private static float MeasureStripHeight(float stripWidth, List<RoleAssignment> assignments, RoleStore store)
        {
            if (assignments.Count == 0) return RoleChipUI.Height;
            float x = 0f;
            int lines = 1;
            foreach (var a in assignments)
            {
                var role = store.RoleById(a.roleId);
                if (role == null) continue;
                float w = RoleChipUI.WidthFor(role, showRemove: true);
                if (x + w > stripWidth && x > 0f)
                {
                    lines++;
                    x = 0f;
                }
                x += w + ChipGap;
            }
            return lines * (RoleChipUI.Height + ChipGap) - ChipGap;
        }

        private static float LayoutChips(float stripWidth, List<RoleAssignment> assignments, RoleStore store,
            List<(RoleAssignment assignment, Rect rect, int line)> result)
        {
            float x = 0f, y = 0f;
            int line = 0;
            foreach (var a in assignments)
            {
                var role = store.RoleById(a.roleId);
                if (role == null) continue;
                float w = RoleChipUI.WidthFor(role, showRemove: true);
                if (x + w > stripWidth && x > 0f)
                {
                    line++;
                    x = 0f;
                    y += RoleChipUI.Height + ChipGap;
                }
                result.Add((a, new Rect(x, y, w, RoleChipUI.Height), line));
                x += w + ChipGap;
            }
            float totalH = y + RoleChipUI.Height;
            return totalH;
        }

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            RoleDrag.Update();

            var pawns = ListedPawns();
            if (selectedPawn == null || !pawns.Contains(selectedPawn))
                selectedPawn = pawns.Count > 0 ? pawns[0] : null;

            float statsPanelH = StatsPanelHeight(selectedPawn);
            float tableBottom = rect.yMax - statsPanelH - StatsPanelMargin;
            float paletteH = PaletteHeight(store, rect.width - 16f);
            float filterTop = rect.y + paletteH + 8f;
            float tableTop = filterTop + FilterRowH + 4f;

            DrawPalette(new Rect(rect.x, rect.y, rect.width, paletteH), store);

            // Change 1: 2px light-grey solid separator instead of white DrawLineHorizontal
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + paletteH + 4f, rect.width, 2f),
                new Color(1f, 1f, 1f, 0.25f));

            DrawFilterRow(new Rect(rect.x, filterTop, rect.width, FilterRowH), store);
            DrawTable(new Rect(rect.x, tableTop, rect.width, tableBottom - tableTop), store,
                FilteredPawns(pawns, store));
            DrawStatsPanel(new Rect(rect.x, tableBottom + StatsPanelMargin, rect.width, statsPanelH), store);

            DrawDragGhost(store);
            RoleDrag.ResolveMouseUp();
        }

        // ----- Palette -----

        /// Lays out the palette as two side-by-side labeled columns — manual roles
        /// left, auto roles (any role with rules) right — with column widths scaled
        /// to role counts but at least PaletteColumnMinW each. While one kind is
        /// absent the palette is a single full-width run (unlabeled when no auto
        /// role exists). Returns content height; separatorX is -1 with one column.
        /// Pass null lists to measure only.
        private static float LayoutPalette(RoleStore store, float rowWidth,
            List<(Role role, Rect rect)> chips, List<(string label, Rect rect)> headers, out float separatorX)
        {
            separatorX = -1f;
            var manual = new List<Role>();
            var auto = new List<Role>();
            foreach (var role in store.roles)
                (role.HasRules ? auto : manual).Add(role);

            float LayoutGroup(List<Role> roles, float x0, float colWidth, float y0)
            {
                float x = 0f, y = y0;
                foreach (var role in roles)
                {
                    float w = RoleChipUI.WidthFor(role, showRemove: false);
                    if (x + w > colWidth && x > 0f) { x = 0f; y += RoleChipUI.Height + ChipGap; }
                    chips?.Add((role, new Rect(x0 + x, y, w, RoleChipUI.Height)));
                    x += w + ChipGap;
                }
                return y + RoleChipUI.Height;
            }

            if (auto.Count == 0)
                return LayoutGroup(manual, 0f, rowWidth, 0f);

            const float HeaderChipGap = 2f;

            if (manual.Count == 0)
            {
                headers?.Add(("WR_PaletteAuto".Translate(), new Rect(0f, 0f, rowWidth, GroupLabelHeight)));
                return LayoutGroup(auto, 0f, rowWidth, GroupLabelHeight + HeaderChipGap);
            }

            float manualW = rowWidth * manual.Count / (manual.Count + auto.Count);
            manualW = Mathf.Clamp(manualW, PaletteColumnMinW, rowWidth - PaletteColumnMinW - PaletteColumnGap);
            float autoX = manualW + PaletteColumnGap;
            float autoW = rowWidth - autoX;
            separatorX = manualW + PaletteColumnGap / 2f;

            headers?.Add(("WR_PaletteManual".Translate(), new Rect(0f, 0f, manualW, GroupLabelHeight)));
            headers?.Add(("WR_PaletteAuto".Translate(), new Rect(autoX, 0f, autoW, GroupLabelHeight)));
            float manualH = LayoutGroup(manual, 0f, manualW, GroupLabelHeight + HeaderChipGap);
            float autoH = LayoutGroup(auto, autoX, autoW, GroupLabelHeight + HeaderChipGap);
            return Mathf.Max(manualH, autoH);
        }

        private static float PaletteHeight(RoleStore store, float rowWidth)
            => Mathf.Min(LayoutPalette(store, rowWidth, null, null, out _) + PalettePadding, PaletteMaxHeight);

        private void DrawPalette(Rect rect, RoleStore store)
        {
            float rowWidth = rect.width - 16f;
            var chips = new List<(Role role, Rect rect)>();
            var headers = new List<(string label, Rect rect)>();
            float contentHeight = LayoutPalette(store, rowWidth, chips, headers, out float separatorX);

            Widgets.BeginScrollView(rect, ref paletteScroll, new Rect(0f, 0f, rowWidth, contentHeight));

            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            foreach (var (label, headerRect) in headers)
                Widgets.Label(headerRect, label);
            GUI.color = Color.white;

            if (separatorX >= 0f)
                Widgets.DrawBoxSolid(new Rect(separatorX - 1f, 0f, 2f, contentHeight),
                    new Color(1f, 1f, 1f, 0.25f));

            foreach (var (role, chipRect) in chips)
            {
                int capturedId = role.id;
                var click = RoleChipUI.Draw(chipRect, role, role.enabled ? ChipStyle.Normal : ChipStyle.Disabled,
                    showRemove: false, dragSource: null,
                    onClick: () => RoleCommands.ToggleRoleGlobal(capturedId));
                if (Mouse.IsOver(chipRect))
                    TooltipHandler.TipRegion(chipRect, RoleTip(role));
            }
            Widgets.EndScrollView();
        }

        // ----- Filter row -----

        private void DrawFilterRow(Rect rect, RoleStore store)
        {
            const float SearchLabelW = 46f;
            const float SearchW = 150f;
            const float SearchH = 24f;
            const float RoleBtnW = 150f;
            float y = rect.y + (rect.height - SearchH) / 2f;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, y, SearchLabelW, SearchH), "WR_Search".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            colonistFilter = Widgets.TextField(new Rect(rect.x + SearchLabelW + 4f, y, SearchW, SearchH), colonistFilter);

            // Deleted roles drop the filter rather than filtering everyone out.
            if (roleFilterId != -1 && store.RoleById(roleFilterId) == null)
                roleFilterId = -1;

            float btnX = rect.x + SearchLabelW + 4f + SearchW + 12f;
            string btnLabel = roleFilterId == -1
                ? "WR_FilterAllRoles".Translate()
                : store.RoleById(roleFilterId).label;
            if (Widgets.ButtonText(new Rect(btnX, y, RoleBtnW, SearchH), btnLabel))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("WR_FilterAllRoles".Translate(), () => roleFilterId = -1)
                };
                foreach (var role in store.roles)
                {
                    int id = role.id;
                    options.Add(new FloatMenuOption(role.label, () => roleFilterId = id));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            if (!colonistFilter.NullOrEmpty() || roleFilterId != -1)
            {
                var clearRect = new Rect(btnX + RoleBtnW + 8f, y + (SearchH - 18f) / 2f, 18f, 18f);
                TooltipHandler.TipRegion(clearRect, "WR_ClearFilters".Translate());
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                {
                    colonistFilter = "";
                    roleFilterId = -1;
                }
            }
        }

        private bool FiltersActive => !colonistFilter.NullOrEmpty() || roleFilterId != -1;

        private List<Pawn> FilteredPawns(List<Pawn> pawns, RoleStore store)
        {
            if (!FiltersActive) return pawns;

            // The role filter also matches "included roles": roles whose entries the
            // selected role covers (picking Farmer shows Grower/Plant Cutter holders too).
            HashSet<int> matchIds = null;
            if (roleFilterId != -1)
            {
                matchIds = new HashSet<int> { roleFilterId };
                var selected = store.RoleById(roleFilterId);
                if (selected != null)
                    foreach (var role in store.roles)
                        if (selected.Covers(role))
                            matchIds.Add(role.id);
            }

            var result = new List<Pawn>();
            foreach (var pawn in pawns)
            {
                if (!colonistFilter.NullOrEmpty()
                    && pawn.LabelShortCap.IndexOf(colonistFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (matchIds != null)
                {
                    store.pawnSets.TryGetValue(pawn, out var set);
                    if (set == null || !set.assignments.Any(a => matchIds.Contains(a.roleId))) continue;
                }
                result.Add(pawn);
            }
            return result;
        }

        private static string RoleTip(Role role)
        {
            var parts = role.entries.Select(e => e.DefName);
            string state = role.enabled ? "WR_RoleTipEnabled".Translate() : "WR_RoleTipDisabled".Translate();
            return "WR_RoleTip".Translate(role.label, state, string.Join(", ", parts), "WR_RoleTipHint".Translate());
        }

        // ----- Colonist table -----

        private void DrawTable(Rect rect, RoleStore store, List<Pawn> pawns)
        {
            if (pawns.Count == 0 && FiltersActive)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(rect, "WR_NoFilterMatches".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float stripWidth = rect.width - 16f - (PortraitSize + 6f + NameWidth + 2f + IconButton + 2f + IconButton + 8f + IconButton + 2f + IconButton + 4f);
            var rowHeights = new List<float>(pawns.Count);
            float contentHeight = 0f;
            foreach (var pawn in pawns)
            {
                store.pawnSets.TryGetValue(pawn, out var set);
                var assignments = set?.assignments ?? new List<RoleAssignment>();
                float stripH = MeasureStripHeight(stripWidth, assignments, store);
                float h = Mathf.Max(RowHeight, stripH + 8f);
                rowHeights.Add(h);
                contentHeight += h;
            }

            Widgets.BeginScrollView(rect, ref tableScroll,
                new Rect(0f, 0f, rect.width - 16f, contentHeight));
            float y = 0f;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                float rowH = rowHeights[i];
                DrawRow(new Rect(0f, y, rect.width - 16f, rowH), pawn, store, stripWidth);
                y += rowH;
            }
            Widgets.EndScrollView();
        }

        /// <summary>Returns the colonist list used by the Colonists tab (no baby pawns).</summary>
        internal static List<Pawn> ListedPawns()
        {
            if (Find.CurrentMap == null) return new List<Pawn>();
            return Find.CurrentMap.mapPawns.FreeColonistsSpawned
                .Concat(Find.CurrentMap.mapPawns.SlavesOfColonySpawned)
                .Where(p => !p.DevelopmentalStage.Baby())
                .Distinct()
                .ToList();
        }

        private void DrawRow(Rect rect, Pawn pawn, RoleStore store, float stripWidth)
        {
            if (pawn == selectedPawn)
                Widgets.DrawHighlightSelected(rect);
            else if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            var portraitRect = new Rect(rect.x, rect.y + (rect.height - PortraitSize) / 2f, PortraitSize, PortraitSize);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, new Vector2(PortraitSize, PortraitSize), Rot4.South));

            var nameRect = new Rect(portraitRect.xMax + 6f, rect.y, NameWidth, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, pawn.LabelShortCap);
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonInvisible(new Rect(rect.x, rect.y, portraitRect.width + 6f + NameWidth, rect.height)))
                selectedPawn = pawn;

            var copyRect = new Rect(nameRect.xMax + 2f, rect.y + (rect.height - IconButton) / 2f, IconButton, IconButton);
            var pasteRect = new Rect(copyRect.xMax + 2f, copyRect.y, IconButton, IconButton);
            if (Widgets.ButtonImage(copyRect, TexButton.Copy))
            {
                store.pawnSets.TryGetValue(pawn, out var toCopy);
                RoleClipboard.CopyFrom(toCopy);
                Messages.Message("WR_CopiedRoles".Translate(pawn.LabelShortCap), MessageTypeDefOf.NeutralEvent, historical: false);
            }
            Color pasteColor = RoleClipboard.HasContent ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            if (Widgets.ButtonImage(pasteRect, TexButton.Paste, pasteColor) && RoleClipboard.HasContent)
                RoleCommands.PasteRoleSet(pawn, RoleClipboard.Content);

            var stripRect = new Rect(pasteRect.xMax + 8f, rect.y, stripWidth, rect.height);
            DrawChipStrip(stripRect, pawn, store, stripWidth);

            var plusRect = new Rect(rect.xMax - IconButton, rect.y + (rect.height - IconButton) / 2f, IconButton, IconButton);
            if (Widgets.ButtonImage(plusRect, TexButton.Plus))
                OpenAddMenu(pawn, store);

            var mergeRect = new Rect(plusRect.x - IconButton - 2f, plusRect.y, IconButton, IconButton);
            bool canCombine = RoleCommands.CanCombineFor(pawn);
            TooltipHandler.TipRegion(mergeRect, "WR_CombinePawnTip".Translate());
            Color mergeColor = canCombine ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            if (Widgets.ButtonImage(mergeRect, WorkRolesTex.Merge, mergeColor) && canCombine)
                RoleCommands.CombineAssignedRolesFor(pawn);
        }

        private void DrawChipStrip(Rect stripRect, Pawn pawn, RoleStore store, float stripWidth)
        {
            store.pawnSets.TryGetValue(pawn, out var set);
            var assignments = set?.assignments ?? new List<RoleAssignment>();

            var layout = new List<(RoleAssignment assignment, Rect rect, int line)>();
            float stripContentHeight = LayoutChips(stripWidth, assignments, store, layout);

            float yOffset = stripRect.y + (stripRect.height - stripContentHeight) / 2f;

            foreach (var (assignment, localRect, _) in layout)
            {
                var role = store.RoleById(assignment.roleId);
                if (role == null) continue;
                var chipRect = new Rect(stripRect.x + localRect.x, yOffset + localRect.y, localRect.width, localRect.height);

                int capturedRoleId = role.id;
                Pawn capturedPawn = pawn;
                bool chipEnabled = role.enabled && assignment.enabled;
                // Rules only matter once both toggles are on; suppression is absolute,
                // so a suppressed chip takes no body click (remove/drag still work).
                bool suppressed = chipEnabled && !RoleRules.Pass(role, pawn);
                ChipStyle style = !chipEnabled ? ChipStyle.Disabled
                    : suppressed ? ChipStyle.AutoOff
                    : ChipStyle.Normal;
                var click = RoleChipUI.Draw(chipRect, role, style,
                    showRemove: true, dragSource: pawn,
                    onClick: suppressed
                        ? (System.Action)null
                        : () => RoleCommands.ToggleRoleForPawn(capturedPawn, capturedRoleId));
                if (click == ChipClick.Remove) RoleCommands.RemoveRoleFromPawn(pawn, role.id);
                if (suppressed && Mouse.IsOver(chipRect))
                    TooltipHandler.TipRegion(chipRect, SuppressionTip(role, pawn));
            }

            if (RoleDrag.Active && Mouse.IsOver(stripRect))
            {
                bool alreadyHasRole = store.pawnSets.TryGetValue(pawn, out var pawnSet)
                    && pawnSet.assignments.Any(a => a.roleId == RoleDrag.RoleId);
                bool isSamePawn = RoleDrag.SourcePawn == pawn;

                if (alreadyHasRole && !isSamePawn)
                {
                    RoleDrag.HoverBlocked = true;
                    Widgets.DrawBoxSolid(stripRect, new Color(0.8f, 0.2f, 0.2f, 0.12f));
                }
                else
                {
                    var mouse = Event.current.mousePosition;
                    float mx = mouse.x - stripRect.x;
                    float my = mouse.y - yOffset;
                    int insertIndex = 0;
                    for (int i = 0; i < layout.Count; i++)
                    {
                        var (_, r, _) = layout[i];
                        if (my > r.yMax)
                        {
                            insertIndex = i + 1;
                            continue;
                        }
                        if (my >= r.y && mx > r.x + r.width / 2f)
                            insertIndex = i + 1;
                    }

                    RoleDrag.HoverPawn = pawn;
                    RoleDrag.HoverInsertIndex = insertIndex;

                    float markerX, markerY, markerH;
                    if (insertIndex == 0 || layout.Count == 0)
                    {
                        markerX = stripRect.x - ChipGap / 2f;
                        markerY = yOffset + 3f;
                        markerH = RoleChipUI.Height - 6f;
                    }
                    else
                    {
                        int prevIdx = insertIndex - 1;
                        var (_, prevR, _) = layout[prevIdx];
                        markerX = stripRect.x + prevR.xMax - ChipGap / 2f;
                        markerY = yOffset + prevR.y + 3f;
                        markerH = prevR.height - 6f;
                    }
                    Widgets.DrawBoxSolid(new Rect(markerX - 1f, markerY, 2f, markerH), new Color(1f, 1f, 1f, 0.9f));
                }
            }
        }

        private static string SuppressionTip(Role role, Pawn pawn)
        {
            switch (RoleRules.FailReason(role, pawn))
            {
                case RuleFailReason.OutsideHours: return "WR_SuppressedHours".Translate();
                case RuleFailReason.AwayFromHome: return "WR_SuppressedAway".Translate();
                case RuleFailReason.AtHome: return "WR_SuppressedHome".Translate();
                default: return "";
            }
        }

        // ----- Stats panel -----

        private void DrawStatsPanel(Rect rect, RoleStore store)
        {
            Widgets.DrawBoxSolidWithOutline(rect, new Color(0.08f, 0.08f, 0.08f, 0.9f), new Color(1f, 1f, 1f, 0.15f));
            rect = rect.ContractedBy(StatsPadding);
            if (selectedPawn == null) return;

            // Left section: portrait framed + name below
            float portraitBoxSize = PortraitDisplaySize;
            var portraitFrameRect = new Rect(rect.x, rect.y, portraitBoxSize, portraitBoxSize);

            // Draw portrait inside a thin outline box
            Widgets.DrawBoxSolidWithOutline(portraitFrameRect,
                new Color(0.05f, 0.05f, 0.05f, 1f),
                new Color(1f, 1f, 1f, 0.25f));
            GUI.DrawTexture(portraitFrameRect,
                PortraitsCache.Get(selectedPawn, new Vector2(portraitBoxSize, portraitBoxSize), Rot4.South));

            // Pawn name directly below portrait, centered
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(rect.x, rect.y + portraitBoxSize + 2f, portraitBoxSize, 20f),
                selectedPawn.LabelShortCap);
            Text.Anchor = TextAnchor.UpperLeft;

            // Change 2a: NO separator between portrait and col1 — 16f blank gap instead
            float col1X = rect.x + portraitBoxSize + 16f;

            // Two fixed-width skill columns
            // Change 2b: col1→col2 separator: 2px wide, brighter, 16f on both sides
            float col2X = col1X + SkillColWidth + ColSepMargin + ColSepWidth + ColSepMargin;

            // Draw col1→col2 separator
            float sep12X = col1X + SkillColWidth + ColSepMargin;
            if (sep12X + ColSepWidth <= rect.xMax)
            {
                Widgets.DrawBoxSolid(new Rect(sep12X, rect.y, ColSepWidth, rect.height),
                    new Color(1f, 1f, 1f, 0.4f));
            }

            // Change 2d: after second skill column — another 2px separator (same style), then Recommended Roles
            float sep23X = col2X + SkillColWidth + ColSepMargin;
            float recX = sep23X + ColSepWidth + ColSepMargin;
            if (sep23X + ColSepWidth <= rect.xMax)
            {
                Widgets.DrawBoxSolid(new Rect(sep23X, rect.y, ColSepWidth, rect.height),
                    new Color(1f, 1f, 1f, 0.4f));
            }

            // Draw skill lines in the two columns
            var lines = SkillsTip.Lines(selectedPawn);
            if (lines.Count == 0) return;

            Text.Font = GameFont.Small;
            for (int i = 0; i < lines.Count; i++)
            {
                int col = i % SkillCols;
                int row = i / SkillCols;
                var line = lines[i];

                float cellX = (col == 0) ? col1X : col2X;
                float cellY = rect.y + row * CellH;

                // Skip if beyond available columns
                if (col >= SkillCols) continue;

                // Text colour priority
                Color textColor;
                if (line.Disabled || line.Level <= 1)
                    textColor = ColorDisabled;
                else if (line.Level <= 5)
                    textColor = ColorLow;
                else if (line.Passion == Passion.Major)
                    textColor = ColorPassMajor;
                else if (line.Passion == Passion.Minor)
                    textColor = ColorPassMinor;
                else
                    textColor = Color.white;

                float xCursor = cellX;

                // 6×6 aptitude square (or reserved space for alignment)
                if (line.Aptitude != 0)
                {
                    Color sqColor = line.Aptitude > 0 ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
                    Widgets.DrawBoxSolid(new Rect(xCursor, cellY + (CellH - 6f) / 2f, 6f, 6f), sqColor);
                }
                xCursor += 8f; // reserve space for aptitude square alignment

                // Skill label
                GUI.color = textColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                string labelText = line.Label;
                Vector2 labelSize = Text.CalcSize(labelText);
                float labelMaxW = SkillColWidth - 8f - (16f + 4f) - 48f; // col width - aptitude space - icon+gap - value col
                Widgets.Label(new Rect(xCursor, cellY, labelMaxW, CellH), labelText);

                // Passion icon appended after label text
                const float IconW = 16f;
                float iconX = xCursor + Mathf.Min(labelSize.x, labelMaxW) + 4f;
                if (line.Passion == Passion.Major)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(iconX, cellY + (CellH - IconW) / 2f, IconW, IconW), WorkRolesTex.PassionMajor);
                }
                else if (line.Passion == Passion.Minor)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(iconX, cellY + (CellH - IconW) / 2f, IconW, IconW), WorkRolesTex.PassionMinor);
                }

                // Value right-aligned in fixed 48f column at right edge of skill column
                // Change 2c: value X positions use updated SkillColWidth (200f) and new layout
                const float ValueW = 48f;
                float valueX = (col == 0)
                    ? (col1X + SkillColWidth - ValueW)
                    : (col2X + SkillColWidth - ValueW);
                GUI.color = textColor;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(valueX, cellY, ValueW, CellH), line.ValueText);

                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // Recommended Roles section
            if (recX < rect.xMax)
            {
                float recW = rect.xMax - recX;
                var recommendations = GetRecommendedRoles(store);

                // Determine which roles are already assigned to this pawn
                var assignedIds = new HashSet<int>();
                if (store.pawnSets.TryGetValue(selectedPawn, out var recPawnSet))
                    foreach (var a in recPawnSet.assignments) assignedIds.Add(a.roleId);

                // Header
                WrText.HeaderLabel(new Rect(recX, rect.y, recW, 28f), "WR_RecommendedRoles".Translate());

                // Chips wrapping below header; the bottom 28f is reserved for "Make It So".
                float chipBottom = rect.yMax - 28f;
                float chipY = rect.y + 28f;
                float chipX = recX;
                foreach (var role in recommendations)
                {
                    bool isAssigned = assignedIds.Contains(role.id);
                    // Already-assigned chips show the remove icon — reserve that extra width.
                    float chipW = RoleChipUI.WidthFor(role, showRemove: isAssigned);
                    if (chipX + chipW > recX + recW && chipX > recX)
                    {
                        chipX = recX;
                        chipY += RoleChipUI.Height + ChipGap;
                        if (chipY + RoleChipUI.Height > chipBottom) break;
                    }
                    var chipRect = new Rect(chipX, chipY, chipW, RoleChipUI.Height);
                    int capturedId = role.id;
                    Pawn capturedPawn = selectedPawn;
                    if (isAssigned)
                    {
                        // Already assigned: Subtle style, remove icon, body click inert.
                        var click = RoleChipUI.Draw(chipRect, role, ChipStyle.Subtle,
                            showRemove: true, dragSource: null, onClick: null);
                        if (click == ChipClick.Remove)
                        {
                            RoleCommands.RemoveRoleFromPawn(capturedPawn, capturedId);
                            InvalidateRecommendationCache();
                        }
                        if (Mouse.IsOver(chipRect))
                            TooltipHandler.TipRegion(chipRect, "WR_AlreadyAssigned".Translate());
                    }
                    else
                    {
                        RoleChipUI.Draw(chipRect, role, ChipStyle.Normal, showRemove: false,
                            dragSource: null,
                            onClick: () =>
                            {
                                AssignAtRecommendedPosition(capturedPawn, capturedId);
                                InvalidateRecommendationCache();
                            });
                    }
                    chipX += chipW + ChipGap;
                }

                if (recommendations.Any(r => !assignedIds.Contains(r.id)))
                {
                    var makeItSoRect = new Rect(rect.xMax - 110f, rect.yMax - 26f, 106f, 24f);
                    if (Widgets.ButtonText(makeItSoRect, "WR_MakeItSo".Translate()))
                    {
                        foreach (var role in recommendations)
                            if (!assignedIds.Contains(role.id))
                                AssignAtRecommendedPosition(selectedPawn, role.id);
                        InvalidateRecommendationCache();
                    }
                }
            }
        }

        /// The recommendation list is a priority preview: display order IS assignment
        /// order. Inserts the role before the pawn's first assignment that ranks later
        /// in the recommendations; assignments outside the list keep their position.
        private void AssignAtRecommendedPosition(Pawn pawn, int roleId)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            AssignAtRecommendedPosition(pawn, roleId, store, GetRecommendedRoles(store));
        }

        private static void AssignAtRecommendedPosition(Pawn pawn, int roleId, RoleStore store, List<Role> recommendations)
        {
            int clickedRank = recommendations.FindIndex(r => r.id == roleId);
            int insertIdx = -1;
            if (clickedRank >= 0 && store.pawnSets.TryGetValue(pawn, out var set))
            {
                for (int i = 0; i < set.assignments.Count; i++)
                {
                    int assignmentId = set.assignments[i].roleId;
                    int rank = recommendations.FindIndex(r => r.id == assignmentId);
                    if (rank >= 0 && rank > clickedRank)
                    {
                        insertIdx = i;
                        break;
                    }
                }
            }
            RoleCommands.AssignRole(pawn, roleId, insertIdx);
        }

        /// Runs the colony-wide lossless combine and refreshes the recommendation cache.
        public void CombineAll()
        {
            RoleCommands.CombineAssignedRoles();
            InvalidateRecommendationCache();
        }

        private static List<Dialog_ChangesPreview.PawnChanges> BuildCombineEntries()
        {
            var entries = new List<Dialog_ChangesPreview.PawnChanges>();
            foreach (var pawn in ListedPawns())
            {
                var steps = RoleCommands.CombinePlanFor(pawn);
                if (steps.Count == 0) continue;
                var lines = steps
                    .Select(s => "WR_PreviewCombineLine".Translate(
                        string.Join(" + ", s.members.Select(m => m.label)), s.combo.label).ToString())
                    .ToList();
                entries.Add(new Dialog_ChangesPreview.PawnChanges { pawn = pawn, lines = lines });
            }
            return entries;
        }

        private static List<Dialog_ChangesPreview.PawnChanges> BuildFixEntries()
        {
            var entries = new List<Dialog_ChangesPreview.PawnChanges>();
            var store = RoleStore.Current;
            if (store == null) return entries;
            foreach (var pawn in ListedPawns())
            {
                var recommendations = ComputeRecommendations(pawn, store);
                var assignedIds = new HashSet<int>();
                if (store.pawnSets.TryGetValue(pawn, out var set))
                    foreach (var a in set.assignments) assignedIds.Add(a.roleId);
                var lines = recommendations
                    .Where(r => !assignedIds.Contains(r.id))
                    .Select(r => "WR_PreviewAddLine".Translate(r.label).ToString())
                    .ToList();
                if (lines.Count == 0) continue;
                entries.Add(new Dialog_ChangesPreview.PawnChanges { pawn = pawn, lines = lines });
            }
            return entries;
        }

        /// Opens the per-colonist change preview for Combine All; applies on confirm.
        public void ShowCombinePreview()
            => Find.WindowStack.Add(new Dialog_ChangesPreview(
                "WR_CombineAll".Translate(), BuildCombineEntries(), CombineAll, BuildCombineEntries));

        /// Opens the per-colonist change preview for Fix My Colony; applies on confirm.
        public void ShowFixPreview()
            => Find.WindowStack.Add(new Dialog_ChangesPreview(
                "WR_FixMyColony".Translate(), BuildFixEntries(), FixMyColony, BuildFixEntries));

        /// Applies the recommendation engine to every listed colonist: each pawn gets
        /// every recommended role it doesn't already hold, at its recommended position.
        public void FixMyColony()
        {
            var store = RoleStore.Current;
            if (store == null) return;
            foreach (var pawn in ListedPawns())
            {
                var recommendations = ComputeRecommendations(pawn, store);
                var assignedIds = new HashSet<int>();
                if (store.pawnSets.TryGetValue(pawn, out var set))
                    foreach (var a in set.assignments) assignedIds.Add(a.roleId);
                foreach (var role in recommendations)
                    if (!assignedIds.Contains(role.id))
                        AssignAtRecommendedPosition(pawn, role.id, store, recommendations);
            }
            InvalidateRecommendationCache();
        }

        // ----- Recommendation logic -----

        private List<Role> GetRecommendedRoles(RoleStore store)
        {
            // Recompute only when selectedPawn changes; explicit invalidation handles assignment changes.
            if (selectedPawn == _recCachePawn)
                return _recCached;

            _recCachePawn = selectedPawn;
            _recCached = ComputeRecommendations(selectedPawn, store);
            return _recCached;
        }

        private static List<Role> ComputeRecommendations(Pawn pawn, RoleStore store)
        {
            if (pawn == null || pawn.skills == null) return new List<Role>();

            // Collect all skill levels across all colonists to find per-skill maximums
            var allPawns = ListedPawns();
            var skillMaxLevel = new Dictionary<SkillDef, int>();
            foreach (var p in allPawns)
            {
                if (p.skills == null) continue;
                foreach (var sr in p.skills.skills)
                {
                    if (!skillMaxLevel.TryGetValue(sr.def, out int cur) || sr.Level > cur)
                        skillMaxLevel[sr.def] = sr.Level;
                }
            }

            // Recommendation groups, listed in display order; within a group, roles sort
            // by the colonist's ability at the matched skill (best first).
            const int GroupBasics = 0;
            const int GroupWardenCarer = 1; // duty roles sit above the vocations, as in vanilla
            const int GroupHunter = 2;      // training activity: must outrank the skilled work
            const int GroupMajorPassion = 3;
            const int GroupMinorPassion = 4;
            const int GroupBestInColony = 5;
            const int GroupAptitude = 6;
            const int GroupGrunt = 7;

            var scored = new List<(Role role, int group, float sortKey)>();
            int pawnsWithHunting = CountPawnsWithHuntingRole(store);

            foreach (var role in store.roles)
            {
                // Collect work types for this role (from WorkType entries + parent types of WorkGiver entries)
                var workTypes = new HashSet<WorkTypeDef>();
                foreach (var entry in role.entries)
                {
                    if (entry.Kind == JobEntryKind.WorkType)
                    {
                        var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.DefName);
                        if (wt != null) workTypes.Add(wt);
                    }
                    else
                    {
                        var wg = DefDatabase<WorkGiverDef>.GetNamedSilentFail(entry.DefName);
                        if (wg?.workType != null) workTypes.Add(wg.workType);
                    }
                }

                // Must be USABLE: at least one work type not disabled
                bool usable = false;
                foreach (var wt in workTypes)
                {
                    if (!pawn.WorkTypeIsDisabled(wt)) { usable = true; break; }
                }
                if (!usable) continue;

                int group = int.MaxValue;
                float sortKey = 0f;

                void Candidate(int g, float key)
                {
                    if (g < group || (g == group && key > sortKey))
                    {
                        group = g;
                        sortKey = key;
                    }
                }

                string tmpl = role.templateDefName;
                if (tmpl == "WS_Basics")
                {
                    Candidate(GroupBasics, 0f);
                }
                else if (tmpl == "WS_Grunt")
                {
                    Candidate(GroupGrunt, 0f);
                }
                else if (tmpl == "WS_Hunter"
                    && pawn.equipment?.Primary?.def?.IsRangedWeapon == true)
                {
                    int shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                    bool canUpSkill = shooting < 15;
                    bool fewHunters = pawnsWithHunting < 3;
                    if (canUpSkill || fewHunters) Candidate(GroupHunter, shooting);
                }

                // Skill-based groups apply to every role and can outrank the template groups.
                foreach (var wt in workTypes)
                {
                    if (wt.relevantSkills == null) continue;
                    foreach (var skillDef in wt.relevantSkills)
                    {
                        var sr = pawn.skills.GetSkill(skillDef);
                        if (sr == null || sr.TotallyDisabled) continue;

                        if (sr.passion == Passion.Major) Candidate(GroupMajorPassion, sr.Level);
                        else if (sr.passion == Passion.Minor) Candidate(GroupMinorPassion, sr.Level);
                        if (skillMaxLevel.TryGetValue(skillDef, out int maxLvl)
                            && sr.Level >= maxLvl && sr.Level > 0)
                            Candidate(GroupBestInColony, sr.Level);
                        if (sr.Aptitude > 0) Candidate(GroupAptitude, sr.Aptitude * 1000f + sr.Level);
                    }
                }

                // Duty roles pin to their fixed slot regardless of which skill rule
                // triggered them; the trigger's skill level remains the sort key.
                if (group != int.MaxValue && (tmpl == "WS_Warden" || tmpl == "WS_Childminder"))
                    group = GroupWardenCarer;

                if (group != int.MaxValue) scored.Add((role, group, sortKey));
            }

            var ordered = scored
                .OrderBy(t => t.group)
                .ThenByDescending(t => t.sortKey)
                .Select(t => t.role)
                .ToList();

            // A combo beats its parts: never recommend a role another recommended role covers
            // (no Grower next to Farmer, no Firefighter next to Basics).
            return ordered.Where(role => !ordered.Any(other => other.Covers(role))).ToList();
        }

        // ----- Helpers -----

        /// <summary>
        /// Returns how many listed colonists have an ENABLED assignment of a globally-enabled role
        /// whose entries cover the Hunting work type.
        /// </summary>
        private static int CountPawnsWithHuntingRole(RoleStore store)
        {
            int count = 0;
            foreach (var pawn in ListedPawns())
            {
                if (!store.pawnSets.TryGetValue(pawn, out var set)) continue;
                bool hasHunting = false;
                foreach (var assignment in set.assignments)
                {
                    if (!assignment.enabled) continue;
                    var role = store.RoleById(assignment.roleId);
                    if (role == null || !role.enabled) continue;
                    foreach (var entry in role.entries)
                    {
                        bool coversHunting = false;
                        if (entry.Kind == JobEntryKind.WorkType)
                        {
                            coversHunting = entry.DefName == "Hunting";
                        }
                        else
                        {
                            var wg = DefDatabase<WorkGiverDef>.GetNamedSilentFail(entry.DefName);
                            coversHunting = wg?.workType?.defName == "Hunting";
                        }
                        if (coversHunting) { hasHunting = true; break; }
                    }
                    if (hasHunting) break;
                }
                if (hasHunting) count++;
            }
            return count;
        }

        private static void OpenAddMenu(Pawn pawn, RoleStore store)
        {
            var assigned = store.pawnSets.TryGetValue(pawn, out var set)
                ? set.assignments.Select(a => a.roleId).ToHashSet()
                : new HashSet<int>();
            var options = store.roles
                .Where(r => !assigned.Contains(r.id))
                .Select(r => new FloatMenuOption(r.label, () => RoleCommands.AssignRole(pawn, r.id)))
                .ToList();
            if (options.Count == 0)
                options.Add(new FloatMenuOption("WR_AllRolesAssigned".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void DrawDragGhost(RoleStore store)
        {
            if (!RoleDrag.Active) return;
            var role = store.RoleById(RoleDrag.RoleId);
            if (role == null) return;
            var mouse = Event.current.mousePosition;
            float w = RoleChipUI.WidthFor(role, showRemove: false);

            Color ghostColor = RoleDrag.HoverBlocked
                ? new Color(1f, 0.3f, 0.3f, 0.7f)
                : new Color(1f, 1f, 1f, 0.7f);

            GUI.color = ghostColor;
            Widgets.DrawBoxSolid(new Rect(mouse.x + 10f, mouse.y + 6f, w, RoleChipUI.Height),
                role.hasCustomColor ? role.color : RoleChipUI.DefaultChipColor);
            Widgets.Label(new Rect(mouse.x + 18f, mouse.y + 8f, w, 22f), role.label);
            GUI.color = Color.white;
        }
    }
}
