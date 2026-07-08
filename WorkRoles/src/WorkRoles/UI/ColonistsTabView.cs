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

        private const float PaletteMaxHeight = 260f;   // palette scrolls beyond this
        private const float PalettePadding = 6f;
        private const float ClusterLabelH = 15f;
        private const float ClusterGapX = 20f;
        private const float ClusterGapY = 12f;
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

        // Colony suggestion plan: the Recommended Roles panel, Make It So and Fix My
        // Colony all read this one plan, so they always agree. Computed lazily,
        // invalidated on any role/assignment change.
        private List<PawnFixPlan> _planCache;

        public void Reset()
        {
            paletteScroll = Vector2.zero;
            tableScroll = Vector2.zero;
            selectedPawn = null;
            colonistFilter = "";
            roleFilterId = -1;
            InvalidateRecommendationCache();
        }

        /// Public so tab switches can invalidate after Roles-tab edits.
        public void InvalidateRecommendationCache() => _planCache = null;

        private List<PawnFixPlan> GetPlan() => _planCache ??= BuildColonyFixPlan();

        private PawnFixPlan PlanFor(Pawn pawn) => GetPlan().FirstOrDefault(p => p.pawn == pawn);

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

            // Fixed left columns: portrait | gap | name | gap | copy | gap | paste | gap | [+] | gap | trailing
            float fixedLeft = PortraitSize + 6f + NameWidth + 2f + IconButton + 2f + IconButton + 8f + IconButton + 4f + 16f;
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

        private sealed class PaletteCluster
        {
            public string label;
            public List<Role> roles = new List<Role>();
        }

        /// Clusters for the palette, keyed by the tree root's first relevant skill;
        /// children always cluster with their displayed parent, and rule-carrying
        /// (auto) roles cluster by skill like any other — the chip marker identifies
        /// them. Exceptions: autoAssign roots form "Everyone" (pinned first),
        /// skill-less roots form "Unskilled" (pinned last). Skill clusters keep
        /// catalog (first-appearance) order.
        private static List<PaletteCluster> BuildPaletteClusters(RoleStore store)
        {
            var skillClusters = new List<PaletteCluster>();
            PaletteCluster everyone = null, unskilled = null;

            PaletteCluster ClusterFor(Role root)
            {
                if (root.autoAssign)
                    return everyone ??= new PaletteCluster { label = "WR_ClusterEveryone".Translate() };
                var skills = RelevantSkillsOf(root);
                if (skills.Count == 0)
                    return unskilled ??= new PaletteCluster { label = "WR_ClusterUnskilled".Translate() };
                string label = skills[0].LabelCap;
                var cluster = skillClusters.FirstOrDefault(c => c.label == label);
                if (cluster == null)
                {
                    cluster = new PaletteCluster { label = label };
                    skillClusters.Add(cluster);
                }
                return cluster;
            }

            foreach (var (role, parent) in RolesTabView.BuildRoleTree(store).rows)
                ClusterFor(parent ?? role).roles.Add(role);

            var result = new List<PaletteCluster>();
            if (everyone != null) result.Add(everyone);
            result.AddRange(skillClusters);
            if (unskilled != null) result.Add(unskilled);
            return result;
        }

        /// Lays out the palette as skill clusters: a Tiny label above each cluster's
        /// chips, clusters flowing left-to-right with ClusterGapX between them and
        /// wrapping as atomic blocks (a cluster wider than the full row wraps its
        /// chips internally instead). Returns content height; pass null lists to
        /// measure only.
        private static float LayoutPalette(RoleStore store, float rowWidth,
            List<(Role role, Rect rect)> chips, List<(string label, Rect rect)> labels)
        {
            float x = 0f, bandY = 0f, bandBottom = 0f;
            foreach (var cluster in BuildPaletteClusters(store))
            {
                float w = -ChipGap;
                foreach (var role in cluster.roles)
                    w += RoleChipUI.WidthFor(role, showRemove: false) + ChipGap;
                Text.Font = GameFont.Tiny;
                w = Mathf.Max(w, Text.CalcSize(cluster.label).x);
                Text.Font = GameFont.Small;
                w = Mathf.Min(w, rowWidth);

                if (x > 0f && x + w > rowWidth)
                {
                    x = 0f;
                    bandY = bandBottom + ClusterGapY;
                }

                labels?.Add((cluster.label, new Rect(x, bandY, w, ClusterLabelH)));
                float cx = x, cy = bandY + ClusterLabelH + 2f;
                foreach (var role in cluster.roles)
                {
                    float chipW = RoleChipUI.WidthFor(role, showRemove: false);
                    if (cx + chipW > rowWidth && cx > x) { cx = x; cy += RoleChipUI.Height + ChipGap; }
                    chips?.Add((role, new Rect(cx, cy, chipW, RoleChipUI.Height)));
                    cx += chipW + ChipGap;
                }
                bandBottom = Mathf.Max(bandBottom, cy + RoleChipUI.Height);
                x += w + ClusterGapX;
            }
            return bandBottom;
        }

        private static float PaletteHeight(RoleStore store, float rowWidth)
            => Mathf.Min(LayoutPalette(store, rowWidth, null, null) + PalettePadding, PaletteMaxHeight);

        private void DrawPalette(Rect rect, RoleStore store)
        {
            float rowWidth = rect.width - 16f;
            var chips = new List<(Role role, Rect rect)>();
            var labels = new List<(string label, Rect rect)>();
            float contentHeight = LayoutPalette(store, rowWidth, chips, labels);

            Widgets.BeginScrollView(rect, ref paletteScroll, new Rect(0f, 0f, rowWidth, contentHeight));

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.60f, 0.62f, 0.64f);
            foreach (var (label, labelRect) in labels)
                Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

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

            // The role filter matches anyone with the selected role's capabilities:
            // holders of the role itself or of any role covering it (picking Medic
            // shows Doctors too; picking Firefighter shows Basics holders).
            HashSet<int> matchIds = null;
            if (roleFilterId != -1)
            {
                matchIds = new HashSet<int> { roleFilterId };
                var selected = store.RoleById(roleFilterId);
                if (selected != null)
                    foreach (var role in store.roles)
                        if (role.Covers(selected))
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

            float stripWidth = rect.width - 16f - (PortraitSize + 6f + NameWidth + 2f + IconButton + 2f + IconButton + 8f + IconButton + 4f);
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
                if (click == ChipClick.Context)
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        new FloatMenuOption(
                            assignment.pinned ? "WR_UnpinAssignment".Translate() : "WR_PinAssignment".Translate(),
                            () =>
                            {
                                RoleCommands.ToggleAssignmentPin(capturedPawn, capturedRoleId);
                                InvalidateRecommendationCache();
                            })
                    }));
                if (assignment.pinned)
                {
                    RoleChipUI.DrawPinnedOutline(chipRect);
                    if (Mouse.IsOver(chipRect))
                        TooltipHandler.TipRegion(chipRect, "WR_PinnedTip".Translate());
                }
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

            // Recommended Roles section: mirrors the Make It So outcome — kept roles
            // subtle, additions normal, removals struck — so the panel IS the preview
            // and the button applies directly.
            if (recX < rect.xMax)
            {
                float recW = rect.xMax - recX;
                var pawnPlan = PlanFor(selectedPawn);

                WrText.HeaderLabel(new Rect(recX, rect.y, recW, 28f), "WR_RecommendedRoles".Translate());

                // Chips wrapping below header; the bottom 28f is reserved for "Make It So".
                float chipBottom = rect.yMax - 28f;
                float chipY = rect.y + 28f;
                float chipX = recX;
                var chips = pawnPlan == null
                    ? new List<(Role role, Dialog_ChangesPreview.ChipState state, string tip)>()
                    : PreviewEntry(store, pawnPlan).lines[0].chips;
                foreach (var (role, state, tip) in chips)
                {
                    bool isAssigned = state != Dialog_ChangesPreview.ChipState.Added;
                    // Assigned chips show the remove icon — reserve that extra width.
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
                        // Assigned: Subtle style, remove icon, body click inert.
                        var click = RoleChipUI.Draw(chipRect, role, ChipStyle.Subtle,
                            showRemove: true, dragSource: null, onClick: null);
                        if (click == ChipClick.Remove)
                        {
                            RoleCommands.RemoveRoleFromPawn(capturedPawn, capturedId);
                            InvalidateRecommendationCache();
                        }
                        if (state == Dialog_ChangesPreview.ChipState.Removed)
                        {
                            RoleChipUI.DrawRemovedOutline(chipRect);
                            if (Mouse.IsOver(chipRect))
                                TooltipHandler.TipRegion(chipRect, "WR_WillBeRemoved".Translate());
                        }
                        else if (Mouse.IsOver(chipRect))
                        {
                            TooltipHandler.TipRegion(chipRect, "WR_AlreadyAssigned".Translate());
                        }
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
                        if (tip != null && Mouse.IsOver(chipRect))
                            TooltipHandler.TipRegion(chipRect, tip);
                    }
                    chipX += chipW + ChipGap;
                }

                if (pawnPlan != null && (pawnPlan.added.Count > 0 || pawnPlan.removed.Count > 0))
                {
                    var makeItSoRect = new Rect(rect.xMax - 110f, rect.yMax - 26f, 106f, 24f);
                    if (Widgets.ButtonText(makeItSoRect, "WR_MakeItSo".Translate()))
                        ApplyFix(selectedPawn);
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

        /// Work the colony must never be without, in preference order (when one pawn
        /// holds several, this is their relative order): the work type that defines
        /// it, and the shipped role preferred to provide it. Coverage-granted
        /// essentials are promoted to the front of the pawn's target so they run.
        private static readonly (string workType, string template)[] Essentials =
        {
            ("Doctor", "WS_Doctor"),
            ("Cooking", "WS_Cook"),
            ("Construction", "WS_Builder"),
            ("Growing", "WS_Farmer"),
            ("Mining", "WS_Miner"),
            ("Smithing", "WS_Smith"),
            ("Tailoring", "WS_Tailor"),
            ("Crafting", "WS_Crafter"),
        };

        /// Whether the role's entries include the whole work type.
        private static bool HasWorkTypeEntry(Role role, string workType)
            => role.entries.Any(e => e.Kind == JobEntryKind.WorkType && e.DefName == workType);

        /// The role providing a work type: the shipped template when it exists, is
        /// enabled and rule-free; otherwise the smallest enabled rule-free role
        /// carrying the whole work type (catalog-order ties). Keeps essentials and
        /// Hunter logic working when players replace the shipped catalog.
        private static Role RoleProviding(RoleStore store, string workType, string template)
        {
            var shipped = store.roles.FirstOrDefault(r => r.templateDefName == template);
            if (shipped != null && shipped.enabled && !shipped.HasRules) return shipped;
            Role best = null;
            foreach (var role in store.roles)
            {
                if (!role.enabled || role.HasRules || !HasWorkTypeEntry(role, workType)) continue;
                if (best == null || role.entries.Count < best.entries.Count) best = role;
            }
            return best;
        }

        /// Whether the role touches the Hunting work type (whole type or any job).
        private static bool ProvidesHunting(Role role)
            => WorkTypesOf(role).Any(wt => wt.defName == "Hunting");

        /// Whether the role carries a gate (a hunting role's weapon gate counts).
        /// Gated roles keep their special coverage dealing.
        private static bool HasGate(Role role)
        {
            if (ProvidesHunting(role)) return true;
            if (role.templateDefName == null) return false;
            var def = DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
            return def?.gateSkill != null;
        }

        /// Whether the role trains no skill — grunt work (Grunt, Hauler, Cleaner, …).
        private static bool IsUnskilledRole(Role role)
            => !role.autoAssign && !role.HasRules && RelevantSkillsOf(role).Count == 0;

        /// Assembles a pawn's replacement role list: autoAssign roles (Basics) lead;
        /// Hunter (tier 0: Shooting below 15 or the colony's food-security hunter)
        /// comes next, then promoted essentials (coverage-granted must-have work,
        /// preference order) so they actually run, then Hunter tier 1 (Shooting
        /// 15-18) right after the essentials;
        /// then the pawn's DESIGNATED unskilled roles — unskilled roles it
        /// already held before its first skilled role (or all of them when it holds
        /// no skilled role), so dedicated haulers/cleaners keep their spot and order;
        /// then the skilled recommendations in order, then colony-plan extras, then
        /// the unskilled tail (preserved existing ones, then recommended ones like
        /// Grunt), then Hunter tier 2 (Shooting 19+) dead last; assigned auto roles
        /// pin at their original position. Unskilled
        /// roles the pawn holds are never removed. Retained roles keep their
        /// per-pawn toggle, new ones start enabled.
        private static List<RoleAssignment> BuildOrderedTarget(Pawn pawn, RoleStore store,
            List<Role> recommendations, List<int> extraIds, List<int> promoted = null,
            int hunterTier = -1, int hunterRoleId = -1)
        {
            store.pawnSets.TryGetValue(pawn, out var set);
            var existing = set?.assignments ?? new List<RoleAssignment>();
            var hunterRole = store.RoleById(hunterRoleId);

            // Protected assignments (rule-carrying roles, player-pinned) never move:
            // they skip normal placement and re-enter at their original position.
            var protectedIds = new HashSet<int>();
            foreach (var a in existing)
                if (a.pinned || store.RoleById(a.roleId)?.HasRules == true)
                    protectedIds.Add(a.roleId);

            var target = new List<RoleAssignment>();
            void Add(int roleId)
            {
                if (protectedIds.Contains(roleId)) return;
                if (target.Any(a => a.roleId == roleId)) return;
                var current = existing.FirstOrDefault(a => a.roleId == roleId);
                target.Add(new RoleAssignment
                { roleId = roleId, enabled = current?.enabled ?? true, pinned = current?.pinned ?? false });
            }
            bool IsTieredHunter(int roleId)
                => hunterTier == 2 && hunterRole != null && roleId == hunterRole.id;

            // Auto-assign roles lead, ordered by their work-type priority (the flag
            // grants membership in the block; content decides the order within it).
            foreach (var role in store.roles
                .Where(r => r.autoAssign)
                .OrderByDescending(MaxNaturalPriority))
                Add(role.id);

            if (hunterTier == 0 && hunterRole != null) Add(hunterRole.id);
            if (promoted != null)
                foreach (var id in promoted) Add(id);
            if (hunterTier == 1 && hunterRole != null) Add(hunterRole.id);

            int firstSkilled = existing.FindIndex(a =>
            {
                var r = store.RoleById(a.roleId);
                return r != null && !r.autoAssign && !r.HasRules && RelevantSkillsOf(r).Count > 0;
            });
            var trailingUnskilled = new List<int>();
            for (int i = 0; i < existing.Count; i++)
            {
                var r = store.RoleById(existing[i].roleId);
                if (r == null || !IsUnskilledRole(r)) continue;
                if (firstSkilled < 0 || i < firstSkilled) Add(r.id);
                else trailingUnskilled.Add(r.id);
            }

            var recUnskilled = new List<int>();
            foreach (var role in recommendations)
            {
                if (role.autoAssign || IsTieredHunter(role.id)) continue;
                if (IsUnskilledRole(role)) { recUnskilled.Add(role.id); continue; }
                Add(role.id);
            }

            if (extraIds != null)
                foreach (var id in extraIds)
                {
                    var r = store.RoleById(id);
                    if (r == null || IsTieredHunter(id)) continue;
                    if (!r.HasRules && !r.autoAssign && !IsUnskilledRole(r)) Add(id);
                }

            foreach (var id in trailingUnskilled) Add(id);
            foreach (var id in recUnskilled) Add(id);
            if (extraIds != null)
                foreach (var id in extraIds)
                {
                    var r = store.RoleById(id);
                    if (r != null && !r.HasRules && IsUnskilledRole(r)) Add(id);
                }
            if (hunterTier == 2 && hunterRole != null) Add(hunterRole.id);

            // Hunting never outranks doctoring: when the target holds both, the
            // hunter role demotes to just after the last doctoring role above it.
            if (hunterRole != null)
            {
                int hunterIdx = target.FindIndex(a => a.roleId == hunterRole.id);
                if (hunterIdx >= 0)
                {
                    int lastDoctoring = -1;
                    for (int i = 0; i < target.Count; i++)
                    {
                        var r = store.RoleById(target[i].roleId);
                        if (r != null && r.id != hunterRole.id
                            && WorkTypesOf(r).Any(wt => wt.defName == "Doctor"))
                            lastDoctoring = i;
                    }
                    if (lastDoctoring > hunterIdx)
                    {
                        var hunter = target[hunterIdx];
                        target.RemoveAt(hunterIdx);
                        target.Insert(lastDoctoring, hunter);
                    }
                }
            }

            PinProtectedRoles(existing, target, store);
            return target;
        }

        /// Protected assignments — rule-carrying (auto) roles and player-pinned
        /// ones — are never stripped or moved by a replace plan: each re-enters the
        /// target at min(original index, target count), keeping its per-pawn toggle.
        private static void PinProtectedRoles(List<RoleAssignment> existing,
            List<RoleAssignment> target, RoleStore store)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                var role = store.RoleById(existing[i].roleId);
                if (role == null) continue;
                if (!role.HasRules && !existing[i].pinned) continue;
                if (target.Any(a => a.roleId == existing[i].roleId)) continue;
                int at = Mathf.Min(i, target.Count);
                target.Insert(at, new RoleAssignment
                {
                    roleId = existing[i].roleId,
                    enabled = existing[i].enabled,
                    pinned = existing[i].pinned
                });
            }
        }

        /// One wrapped chip line in target order: kept roles dimmed
        /// already-assigned style, added roles normal, removed roles struck
        /// corner-to-corner and slotted back in near their original position.
        private static Dialog_ChangesPreview.PawnPreview PreviewEntry(RoleStore store, PawnFixPlan plan)
        {
            store.pawnSets.TryGetValue(plan.pawn, out var set);
            var existing = set?.assignments ?? new List<RoleAssignment>();
            var existingIds = new HashSet<int>(existing.Select(a => a.roleId));
            var targetIds = new HashSet<int>(plan.target.Select(a => a.roleId));

            var line = new Dialog_ChangesPreview.Line();
            foreach (var a in plan.target)
            {
                var role = store.RoleById(a.roleId);
                if (role == null) continue;
                bool kept = existingIds.Contains(a.roleId);
                string tip = kept ? null
                    : plan.reasons.TryGetValue(a.roleId, out var reason) ? reason : null;
                line.chips.Add((role, kept
                    ? Dialog_ChangesPreview.ChipState.Kept
                    : Dialog_ChangesPreview.ChipState.Added, tip));
            }
            for (int i = 0; i < existing.Count; i++)
            {
                if (targetIds.Contains(existing[i].roleId)) continue;
                var role = store.RoleById(existing[i].roleId);
                if (role == null) continue;
                line.chips.Insert(Mathf.Min(i, line.chips.Count),
                    (role, Dialog_ChangesPreview.ChipState.Removed, (string)"WR_ReasonRemoved".Translate()));
            }

            var entry = new Dialog_ChangesPreview.PawnPreview { pawn = plan.pawn };
            entry.lines.Add(line);
            return entry;
        }

        /// Preview entries from the colony plan (all changed pawns, or just one).
        private List<Dialog_ChangesPreview.PawnPreview> BuildFixEntries(Pawn only)
        {
            var entries = new List<Dialog_ChangesPreview.PawnPreview>();
            var store = RoleStore.Current;
            if (store == null) return entries;
            foreach (var plan in GetPlan())
            {
                if (only != null && plan.pawn != only) continue;
                if (plan.added.Count == 0 && plan.removed.Count == 0) continue;
                entries.Add(PreviewEntry(store, plan));
            }
            return entries;
        }

        /// Rebuild for the preview's stale check: recompute the plan from scratch.
        private List<Dialog_ChangesPreview.PawnPreview> RebuildFixEntries(Pawn only)
        {
            InvalidateRecommendationCache();
            return BuildFixEntries(only);
        }

        /// Applies the colony fix plan (all changed pawns, one pawn, or the preview's
        /// selected subset).
        private void ApplyFix(Pawn only, HashSet<Pawn> included = null)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            foreach (var plan in GetPlan())
            {
                if (only != null && plan.pawn != only) continue;
                if (included != null && !included.Contains(plan.pawn)) continue;
                if (plan.added.Count == 0 && plan.removed.Count == 0) continue;
                RoleCommands.PasteRoleSet(plan.pawn, plan.target);
            }
            InvalidateRecommendationCache();
        }

        /// Opens the per-colonist change preview for Fix My Colony; applies to the
        /// preview's selected colonists on confirm. (Make It So needs no dialog:
        /// the Recommended Roles panel IS its preview.)
        public void ShowFixPreview()
            => Find.WindowStack.Add(new Dialog_ChangesPreview(
                "WR_FixMyColony".Translate(), BuildFixEntries(null),
                included => ApplyFix(null, included), () => RebuildFixEntries(null)));

        // ----- Recommendation logic -----

        /// The selected pawn's suggested role list — the colony plan's target for it,
        /// so the panel, Make It So and Fix My Colony always agree.
        private List<Role> GetRecommendedRoles(RoleStore store)
        {
            var plan = PlanFor(selectedPawn);
            if (plan == null) return new List<Role>();
            var result = new List<Role>();
            foreach (var a in plan.target)
            {
                var role = store.RoleById(a.roleId);
                if (role != null) result.Add(role);
            }
            return result;
        }

        /// Computes the recommendation list. All colony-context reads (redundancy
        /// suppression) go through assignedIdsOf — the colony view — which
        /// defaults to the live store (enabled assignments of globally-enabled roles);
        /// the colony fix plan passes its virtual view instead. When reasons is
        /// given, each recommended role's id maps to a human-readable trigger.
        private static List<Role> ComputeRecommendations(Pawn pawn, RoleStore store,
            System.Func<Pawn, IEnumerable<int>> assignedIdsOf = null,
            Dictionary<int, string> reasons = null)
        {
            if (pawn == null || pawn.skills == null) return new List<Role>();
            var view = assignedIdsOf ?? StoreView(store);

            var allPawns = ListedPawns();
            var skillMaxLevel = SkillMaxLevels(allPawns);

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

            foreach (var role in store.roles)
            {
                // Auto (rule-carrying) roles are player-built automation: never recommended.
                if (role.HasRules) continue;

                var workTypes = WorkTypesOf(role);

                // Must be USABLE: at least one work type not disabled
                bool usable = false;
                foreach (var wt in workTypes)
                {
                    if (!pawn.WorkTypeIsDisabled(wt)) { usable = true; break; }
                }
                if (!usable) continue;

                // Template gates (Hunter/Cook/Butcher) are hard: a failing role never
                // appears, not even through its skill candidacies.
                if (!PassesTemplateGates(role, pawn, skillMaxLevel)) continue;

                int group = int.MaxValue;
                float sortKey = 0f;
                SkillDef matchedSkill = null;

                void Candidate(int g, float key, SkillDef skill = null)
                {
                    if (g < group || (g == group && key > sortKey))
                    {
                        group = g;
                        sortKey = key;
                        matchedSkill = skill;
                    }
                }

                // Content-keyed groups: any auto-assign role is "everyone" work, any
                // skill-less role is grunt work, any hunting-touching role is Hunter —
                // player-built catalogs keep working without shipped templates.
                bool huntingRole = ProvidesHunting(role);
                if (role.autoAssign)
                {
                    // Sorted by work-type priority, not the flag: content decides order.
                    Candidate(GroupBasics, MaxNaturalPriority(role));
                }
                else if (IsUnskilledRole(role))
                {
                    Candidate(GroupGrunt, 0f);
                }
                else if (huntingRole)
                {
                    Candidate(GroupHunter, pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0);
                }

                // Skill-based groups apply to every role and can outrank the template groups.
                foreach (var wt in workTypes)
                {
                    if (wt.relevantSkills == null) continue;
                    foreach (var skillDef in wt.relevantSkills)
                    {
                        var sr = pawn.skills.GetSkill(skillDef);
                        if (sr == null || sr.TotallyDisabled) continue;

                        if (sr.passion == Passion.Major) Candidate(GroupMajorPassion, sr.Level, skillDef);
                        else if (sr.passion == Passion.Minor) Candidate(GroupMinorPassion, sr.Level, skillDef);
                        if (skillMaxLevel.TryGetValue(skillDef, out int maxLvl)
                            && sr.Level >= maxLvl && sr.Level > 0)
                            Candidate(GroupBestInColony, sr.Level, skillDef);
                        if (sr.Aptitude > 0) Candidate(GroupAptitude, sr.Aptitude * 1000f + sr.Level, skillDef);
                    }
                }

                // Redundancy suppression for skill-based candidacies (never for
                // everyone/grunt/hunting roles): when two or more other colonists
                // hold the role and beat this pawn at its matched skill, the colony
                // doesn't need a third — passion or not.
                bool skillCandidacy = group == GroupMajorPassion || group == GroupMinorPassion
                    || group == GroupBestInColony || group == GroupAptitude;
                if (skillCandidacy && matchedSkill != null
                    && !role.autoAssign && !IsUnskilledRole(role) && !huntingRole)
                {
                    int myLevel = pawn.skills.GetSkill(matchedSkill)?.Level ?? 0;
                    int better = 0;
                    foreach (var other in allPawns)
                    {
                        if (other == pawn || other.skills == null) continue;
                        if (!view(other).Contains(role.id)) continue;
                        var otherSkill = other.skills.GetSkill(matchedSkill);
                        if (otherSkill != null && !otherSkill.TotallyDisabled
                            && otherSkill.Level > myLevel) better++;
                    }
                    if (better >= 2) continue;
                }

                // Duty roles (prisoner/child work) pin to their fixed slot regardless
                // of which skill rule triggered them; the trigger's skill level
                // remains the sort key.
                if (group != int.MaxValue
                    && workTypes.Any(wt => wt.defName == "Warden" || wt.defName == "Childcare"))
                    group = GroupWardenCarer;

                if (group != int.MaxValue)
                {
                    scored.Add((role, group, sortKey));
                    if (reasons != null)
                    {
                        string skillLabel = matchedSkill?.skillLabel.CapitalizeFirst() ?? "";
                        reasons[role.id] =
                            group == GroupBasics ? "WR_ReasonEveryone".Translate()
                            : group == GroupWardenCarer ? "WR_ReasonDuty".Translate()
                            : group == GroupHunter ? "WR_ReasonHunter".Translate()
                            : group == GroupGrunt ? "WR_ReasonUnskilled".Translate()
                            : group == GroupMajorPassion ? "WR_ReasonMajorPassion".Translate(skillLabel)
                            : group == GroupMinorPassion ? "WR_ReasonMinorPassion".Translate(skillLabel)
                            : group == GroupBestInColony ? "WR_ReasonBest".Translate(skillLabel)
                            : "WR_ReasonAptitude".Translate(skillLabel);
                    }
                }
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

        /// Hard recommendation gates, evaluated against a colony view.
        /// Hunter's gate is a ranged weapon — every gun carrier hunts (placement is
        /// skill-tiered in the colony plan). Every other gate is data on the
        /// role's template def (training paths): a minimum gate passes at the level
        /// or when best in colony; a maximum gate passes below the level, with a
        /// passion when the def demands one.
        private static bool PassesTemplateGates(Role role, Pawn pawn,
            Dictionary<SkillDef, int> skillMaxLevel)
        {
            if (ProvidesHunting(role))
                return pawn.equipment?.Primary?.def?.IsRangedWeapon == true;

            var def = role.templateDefName == null ? null
                : DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
            if (def?.gateSkill == null) return true;
            var skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(def.gateSkill);
            if (skillDef == null) return true;

            var sr = pawn.skills?.GetSkill(skillDef);
            int level = sr != null && !sr.TotallyDisabled ? sr.Level : 0;
            if (def.gateMinLevel > 0)
            {
                bool best = level > 0 && skillMaxLevel.TryGetValue(skillDef, out int max) && level >= max;
                if (level < def.gateMinLevel && !best) return false;
            }
            if (def.gateMaxLevel > 0 && level >= def.gateMaxLevel) return false;
            if (def.gateNeedsPassion && (sr == null || sr.TotallyDisabled || sr.passion == Passion.None))
                return false;
            return true;
        }

        // ----- Colony fix plan -----

        /// One planned pawn in a colony fix: the full target assignment list plus
        /// the add/remove diff vs the pawn's real assignments, and per-role reason
        /// strings for the preview tooltips.
        private sealed class PawnFixPlan
        {
            public Pawn pawn;
            public List<RoleAssignment> target;
            public List<Role> added = new List<Role>();
            public List<Role> removed = new List<Role>();
            public Dictionary<int, string> reasons = new Dictionary<int, string>();
        }

        /// The highest vanilla work-tab priority among the role's work types —
        /// content-derived placement for auto-assign roles.
        private static int MaxNaturalPriority(Role role)
        {
            int max = 0;
            foreach (var wt in WorkTypesOf(role))
                if (wt.naturalPriority > max) max = wt.naturalPriority;
            return max;
        }

        /// Multi-pass plan for Fix My Colony.
        /// 1. Virtual set per pawn: the autoAssign catalog roles (catalog order) plus
        ///    the pawn's assigned rule-carrying roles (pinned, original order).
        /// 2. Coverage pass: every enabled, rule-free, non-autoAssign, skill-associated
        ///    catalog role is dealt out until N pawns hold it (N scales with colony
        ///    size). Eligible pawns (capable + template gates against the virtual view)
        ///    rank by matched skill descending, then passion (major > minor > none),
        ///    then fewer virtual roles (spread the load); pawns whose virtual set
        ///    already contains a covering role are skipped (overlap minimization).
        ///    Hunter is exempt from top-N: every ranged-armed pawn with Shooting
        ///    below 15 hunts, topped up to at least two hunters from sub-20 shooters.
        /// 3. Backup + ordering pass: recommendations computed against the virtual
        ///    view lead, remaining virtual-set roles append in virtual order; each
        ///    kept role retains the pawn's per-pawn toggle; assigned rule-carrying
        ///    roles pin at their original position.
        /// Every pawn gets a plan entry (the Recommended Roles panel reads them);
        /// appliers skip pawns with neither additions nor removals, so order-only
        /// differences are left alone.
        private static List<PawnFixPlan> BuildColonyFixPlan()
        {
            var plans = new List<PawnFixPlan>();
            var store = RoleStore.Current;
            if (store == null) return plans;
            var pawns = ListedPawns();

            // Pass 1: virtual sets.
            var virtualSets = new Dictionary<Pawn, List<int>>();
            foreach (var pawn in pawns)
            {
                var ids = new List<int>();
                foreach (var role in store.roles)
                    if (role.autoAssign) ids.Add(role.id);
                if (store.pawnSets.TryGetValue(pawn, out var set))
                    foreach (var a in set.assignments)
                    {
                        var role = store.RoleById(a.roleId);
                        if (role != null && role.HasRules && !ids.Contains(role.id)) ids.Add(role.id);
                    }
                virtualSets[pawn] = ids;
            }
            IEnumerable<int> VirtualView(Pawn p)
                => virtualSets.TryGetValue(p, out var ids) ? ids : Enumerable.Empty<int>();

            // Pass 2: coverage. Small colonies get one holder per role; Researcher's
            // minimum scales with the colony's research benches instead; Artist has
            // no minimum (art is optional). Essential grants are remembered so pass 3
            // can promote them.
            int coverage = Mathf.Max(1, (pawns.Count + 5) / 6);
            var skillMaxLevel = SkillMaxLevels(pawns);
            var essentialGrants = new Dictionary<Pawn, List<int>>();
            var hunterTiers = new Dictionary<Pawn, int>(); // 0 pre-essentials, 1 post-essentials, 2 after grunt
            var coverageReasons = new Dictionary<Pawn, Dictionary<int, string>>();
            void SetReason(Pawn p, int roleId, string reason)
            {
                if (!coverageReasons.TryGetValue(p, out var map))
                    coverageReasons[p] = map = new Dictionary<int, string>();
                map[roleId] = reason;
            }

            // Content-based resolution: essentials and Hunter follow whatever role
            // provides the work type, so a player-built catalog keeps working.
            var essentialRank = new Dictionary<int, int>();
            for (int i = 0; i < Essentials.Length; i++)
            {
                var essential = RoleProviding(store, Essentials[i].workType, Essentials[i].template);
                if (essential != null && !essentialRank.ContainsKey(essential.id))
                    essentialRank[essential.id] = i;
            }
            var hunterRole = RoleProviding(store, "Hunting", "WS_Hunter");

            foreach (var role in store.roles)
            {
                if (!role.enabled || role.HasRules || role.autoAssign) continue;
                if (role.templateDefName == "WS_Artist") continue; // min 0: not required
                var relevantSkills = RelevantSkillsOf(role);
                if (relevantSkills.Count == 0) continue; // not skill-associated
                // Sub-roles are not dealt — their coverer is (Farmer, not
                // Grower/Plant Cutter; Smith, not Fabricator) — unless gated
                // (training roles have their own low-skill audience) or resolved as
                // an essential/Hunter (those must be dealt to keep their guarantee).
                if (!HasGate(role) && !essentialRank.ContainsKey(role.id) && role != hunterRole
                    && store.roles.Any(o => o.enabled && !o.HasRules && o.Covers(role)))
                    continue;
                var workTypes = WorkTypesOf(role);

                // Hunter ignores top-N: EVERY pawn with a ranged weapon hunts, but at
                // a skill-dependent position (below 15 = before essentials, 15-18 =
                // after essentials, 19+ = after grunt work), and at least one hunter
                // is placed before essentials so there's food on the table.
                if (role == hunterRole)
                {
                    var shooters = new List<(Pawn pawn, int level, int passion)>();
                    foreach (var pawn in pawns)
                    {
                        if (pawn.skills == null) continue;
                        if (!workTypes.Any(wt => !pawn.WorkTypeIsDisabled(wt))) continue;
                        if (pawn.equipment?.Primary?.def?.IsRangedWeapon != true) continue;
                        var sr = pawn.skills.GetSkill(SkillDefOf.Shooting);
                        int level = sr != null && !sr.TotallyDisabled ? sr.Level : 0;

                        var ids = virtualSets[pawn];
                        bool has = ids.Contains(role.id);
                        if (!has && !ids.Any(id => store.RoleById(id)?.Covers(role) == true))
                        {
                            ids.Add(role.id);
                            has = true;
                        }
                        if (!has) continue;

                        int passion = sr == null ? 0
                            : sr.passion == Passion.Major ? 2
                            : sr.passion == Passion.Minor ? 1 : 0;
                        shooters.Add((pawn, level, passion));
                        hunterTiers[pawn] = level < 15 ? 0 : level < 19 ? 1 : 2;
                        SetReason(pawn, role.id, "WR_ReasonHunter".Translate());
                    }
                    if (shooters.Count > 0 && !hunterTiers.ContainsValue(0))
                    {
                        var best = shooters
                            .OrderByDescending(s => s.level)
                            .ThenByDescending(s => s.passion)
                            .First();
                        hunterTiers[best.pawn] = 0;
                    }
                    continue;
                }

                var eligible = new List<(Pawn pawn, int level, int passion, int load)>();
                foreach (var pawn in pawns)
                {
                    if (pawn.skills == null) continue;
                    if (!workTypes.Any(wt => !pawn.WorkTypeIsDisabled(wt))) continue;
                    if (!PassesTemplateGates(role, pawn, skillMaxLevel)) continue;
                    int level = 0, passion = 0;
                    foreach (var skillDef in relevantSkills)
                    {
                        var sr = pawn.skills.GetSkill(skillDef);
                        if (sr == null || sr.TotallyDisabled) continue;
                        if (sr.Level > level) level = sr.Level;
                        int p = sr.passion == Passion.Major ? 2 : sr.passion == Passion.Minor ? 1 : 0;
                        if (p > passion) passion = p;
                    }
                    eligible.Add((pawn, level, passion, virtualSets[pawn].Count));
                }

                int want = role.templateDefName == "WS_Researcher"
                    ? Mathf.Max(3, CountResearchBenches())
                    : coverage;
                int holders = pawns.Count(p => virtualSets[p].Contains(role.id));
                foreach (var candidate in eligible
                    .OrderByDescending(t => t.level)
                    .ThenByDescending(t => t.passion)
                    .ThenBy(t => t.load))
                {
                    if (holders >= want) break;
                    var ids = virtualSets[candidate.pawn];
                    if (ids.Contains(role.id)) continue;
                    if (ids.Any(id => store.RoleById(id)?.Covers(role) == true)) continue;
                    ids.Add(role.id);
                    holders++;
                    if (essentialRank.TryGetValue(role.id, out int rank))
                    {
                        if (!essentialGrants.TryGetValue(candidate.pawn, out var granted))
                            essentialGrants[candidate.pawn] = granted = new List<int>();
                        granted.Add(role.id);
                        var workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(Essentials[rank].workType);
                        SetReason(candidate.pawn, role.id, "WR_ReasonEssential".Translate(
                            workType?.gerundLabel ?? Essentials[rank].workType));
                    }
                    else
                    {
                        SetReason(candidate.pawn, role.id, "WR_ReasonCoverage".Translate());
                    }
                }
            }

            // Pass 3: backup + ordering (see BuildOrderedTarget), then diff vs real
            // assignments.
            foreach (var pawn in pawns)
            {
                store.pawnSets.TryGetValue(pawn, out var set);
                var existing = set?.assignments ?? new List<RoleAssignment>();

                List<int> promoted = null;
                if (essentialGrants.TryGetValue(pawn, out var granted))
                    promoted = granted
                        .OrderBy(id => essentialRank.TryGetValue(id, out var rank) ? rank : int.MaxValue)
                        .ToList();
                int hunterTier = hunterTiers.TryGetValue(pawn, out var tier) ? tier : -1;

                var recReasons = new Dictionary<int, string>();
                var target = BuildOrderedTarget(pawn, store,
                    ComputeRecommendations(pawn, store, VirtualView, recReasons), virtualSets[pawn],
                    promoted, hunterTier, hunterRole?.id ?? -1);

                var plan = new PawnFixPlan { pawn = pawn, target = target, reasons = recReasons };
                if (coverageReasons.TryGetValue(pawn, out var granted2))
                    foreach (var kv in granted2)
                        plan.reasons[kv.Key] = kv.Value; // coverage story beats the skill story
                var targetIds = new HashSet<int>(target.Select(a => a.roleId));
                var existingIds = new HashSet<int>(existing.Select(a => a.roleId));
                foreach (var a in target)
                {
                    if (existingIds.Contains(a.roleId)) continue;
                    var role = store.RoleById(a.roleId);
                    if (role != null) plan.added.Add(role);
                }
                foreach (var a in existing)
                {
                    if (targetIds.Contains(a.roleId)) continue;
                    var role = store.RoleById(a.roleId);
                    if (role != null) plan.removed.Add(role);
                }
                plans.Add(plan);
            }
            return plans;
        }

        /// Research benches on player home maps (Researcher coverage scales with it).
        private static int CountResearchBenches()
        {
            int count = 0;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                foreach (var building in map.listerBuildings.allBuildingsColonist)
                    if (building is Building_ResearchBench) count++;
            }
            return count;
        }

        // ----- Helpers -----

        /// The live colony view: a pawn's ENABLED assignments of globally-enabled roles.
        private static System.Func<Pawn, IEnumerable<int>> StoreView(RoleStore store)
            => pawn => !store.pawnSets.TryGetValue(pawn, out var set)
                ? Enumerable.Empty<int>()
                : set.assignments
                    .Where(a => a.enabled && store.RoleById(a.roleId)?.enabled == true)
                    .Select(a => a.roleId);

        /// Per-skill maximum level across the given pawns.
        private static Dictionary<SkillDef, int> SkillMaxLevels(List<Pawn> pawns)
        {
            var result = new Dictionary<SkillDef, int>();
            foreach (var p in pawns)
            {
                if (p.skills == null) continue;
                foreach (var sr in p.skills.skills)
                    if (!result.TryGetValue(sr.def, out int cur) || sr.Level > cur)
                        result[sr.def] = sr.Level;
            }
            return result;
        }

        /// Work types a role touches: WorkType entries directly, WorkGiver entries
        /// through their parent work type.
        private static HashSet<WorkTypeDef> WorkTypesOf(Role role)
        {
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
            return workTypes;
        }

        /// Distinct relevant skills across a role's member work types.
        private static List<SkillDef> RelevantSkillsOf(Role role)
        {
            var skills = new List<SkillDef>();
            foreach (var wt in WorkTypesOf(role))
            {
                if (wt.relevantSkills == null) continue;
                foreach (var skillDef in wt.relevantSkills)
                    if (!skills.Contains(skillDef)) skills.Add(skillDef);
            }
            return skills;
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
