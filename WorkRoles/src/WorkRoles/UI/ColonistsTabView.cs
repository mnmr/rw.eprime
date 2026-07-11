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
        private enum KindFilter { All, ColonistsOnly, SlavesOnly }
        private KindFilter kindFilter = KindFilter.All;

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
        /// Design width: fixed chrome (tab strip + FMC button, filter row, editor
        /// swatch grid) fits at this size, so it doubles as the window's min width.
        internal const float DefaultWidth = 1010f;
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
                    w += RoleChipUI.WidthFor(role, showRemove: true, TableChips, AbbrevIfCompact(store, role)) + ChipGap;
                }
                if (w > widestStrip) widestStrip = w;
            }
            return fixedLeft + SkillColumnsWidth() + widestStrip;
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
                float w = RoleChipUI.WidthFor(role, showRemove: true, TableChips, AbbrevIfCompact(store, role));
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
                float w = RoleChipUI.WidthFor(role, showRemove: true, TableChips, AbbrevIfCompact(store, role));
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
                SortedForDisplay(FilteredPawns(pawns, store)));
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
            PaletteCluster everyone = null, unskilled = null, modChores = null;

            PaletteCluster ClusterFor(Role root)
            {
                if (root.managed)
                    return modChores ??= new PaletteCluster { label = "WR_ClusterModChores".Translate() };
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
            if (modChores != null) result.Add(modChores);
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
                w = Mathf.Max(w, WrText.FitWidth(cluster.label));
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
                // Shift-click appends the role to the selected colonist; plain
                // click keeps toggling the role globally.
                var click = RoleChipUI.Draw(chipRect, role, role.enabled ? ChipStyle.Normal : ChipStyle.Disabled,
                    showRemove: false, dragSource: null,
                    onClick: () =>
                    {
                        if (Event.current != null && Event.current.shift)
                        {
                            var target = selectedPawn;
                            if (target != null
                                && !(RoleStore.Current?.SetFor(target).assignments.Any(a => a.roleId == capturedId) ?? true))
                                RoleCommands.AssignRole(target, capturedId);
                        }
                        else
                        {
                            RoleCommands.ToggleRoleGlobal(capturedId);
                        }
                    });
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
                foreach (var role in store.roles.OrderBy(r => r.label, System.StringComparer.OrdinalIgnoreCase))
                {
                    int id = role.id;
                    options.Add(new FloatMenuOption(role.label, () => roleFilterId = id));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Pawn-kind dropdown: colonists and slaves / colonists only / slaves only.
            float kindX = btnX + RoleBtnW + 8f;
            string kindLabel = kindFilter == KindFilter.ColonistsOnly ? "WR_ShowColonists".Translate()
                : kindFilter == KindFilter.SlavesOnly ? "WR_ShowSlaves".Translate()
                : "WR_ShowAll".Translate();
            if (Widgets.ButtonText(new Rect(kindX, y, RoleBtnW, SearchH), kindLabel))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("WR_ShowAll".Translate(), () => kindFilter = KindFilter.All),
                    new FloatMenuOption("WR_ShowColonists".Translate(), () => kindFilter = KindFilter.ColonistsOnly),
                    new FloatMenuOption("WR_ShowSlaves".Translate(), () => kindFilter = KindFilter.SlavesOnly),
                }));
            }

            if (FiltersActive)
            {
                var clearRect = new Rect(kindX + RoleBtnW + 8f, y + (SearchH - 18f) / 2f, 18f, 18f);
                TooltipHandler.TipRegion(clearRect, "WR_ClearFilters".Translate());
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                {
                    colonistFilter = "";
                    roleFilterId = -1;
                    kindFilter = KindFilter.All;
                }
            }

            // Right cluster: Skills column picker + display options behind a gear
            // icon. Display prefs are per-player ModSettings, never world state.
            var settings = WorkRolesMod.Settings;
            if (settings != null)
            {
                var gearRect = new Rect(rect.xMax - SearchH, y, SearchH, SearchH);
                TooltipHandler.TipRegion(gearRect, "WR_DisplayOptions".Translate());
                if (Widgets.ButtonImage(gearRect, TexButton.OpenInspectSettings))
                {
                    string ChipsLabel(ChipDisplay d) =>
                        (settings.chipDisplay == d ? "✓ " : "") +
                        (d == ChipDisplay.Compact ? "WR_ChipsCompact".Translate()
                        : d == ChipDisplay.Minimal ? "WR_ChipsMinimal".Translate()
                        : "WR_ChipsNormal".Translate());
                    string OrderLabel(ColonistOrder o) =>
                        (settings.colonistOrder == o ? "✓ " : "") +
                        (o == ColonistOrder.Alphabetical ? "WR_OrderAZ".Translate() : "WR_OrderBar".Translate());
                    void SetChips(ChipDisplay d) { settings.chipDisplay = d; settings.Write(); }
                    void SetOrder(ColonistOrder o) { settings.colonistOrder = o; settings.Write(); }
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        new FloatMenuOption(ChipsLabel(ChipDisplay.Normal), () => SetChips(ChipDisplay.Normal)),
                        new FloatMenuOption(ChipsLabel(ChipDisplay.Compact), () => SetChips(ChipDisplay.Compact)),
                        new FloatMenuOption(ChipsLabel(ChipDisplay.Minimal), () => SetChips(ChipDisplay.Minimal)),
                        new FloatMenuOption(OrderLabel(ColonistOrder.ColonistBar), () => SetOrder(ColonistOrder.ColonistBar)),
                        new FloatMenuOption(OrderLabel(ColonistOrder.Alphabetical), () => SetOrder(ColonistOrder.Alphabetical)),
                    }));
                }

                const float SkillsBtnW = 110f;
                var skillsRect = new Rect(gearRect.x - 8f - SkillsBtnW, y, SkillsBtnW, SearchH);
                string skillsLabel = skillColumns.Count == 0
                    ? "WR_SkillsButton".Translate().ToString()
                    : "WR_SkillsButtonCount".Translate(skillColumns.Count, MaxSkillColumns).ToString();
                if (Widgets.ButtonText(skillsRect, skillsLabel))
                {
                    var options = new List<FloatMenuOption>();
                    foreach (var skill in DefDatabase<SkillDef>.AllDefsListForReading)
                    {
                        var captured = skill;
                        bool added = skillColumns.Contains(skill);
                        string label = (added ? "✓ " : "") + skill.skillLabel.CapitalizeFirst();
                        options.Add(added || skillColumns.Count >= MaxSkillColumns
                            ? new FloatMenuOption(label, null)
                            : new FloatMenuOption(label, () =>
                            {
                                skillColumns.Add(captured);
                                sortSkill = captured; // new column sorts immediately
                                SaveSkillColumns();
                            }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
        }

        /// "Trains toward X (Skill 8+)." reason suffix for training roles (roles
        /// whose template def carries a maximum skill gate), so players see why
        /// Butcher/Medic/... shows up instead of the full role. Empty otherwise.
        private static string TrainingSuffix(Role role)
        {
            var def = role.templateDefName == null ? null
                : DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
            if (def == null || def.gateMaxLevel <= 0 || def.gateSkill == null) return "";
            var skill = DefDatabase<SkillDef>.GetNamedSilentFail(def.gateSkill);
            var fullDef = DefDatabase<RoleDef>.AllDefsListForReading.FirstOrDefault(d =>
                d.gateSkill == def.gateSkill && d.gateMinLevel == def.gateMaxLevel);
            if (skill == null || fullDef == null) return "";
            string fullLabel = RoleStore.Current?.RoleByTemplate(fullDef.defName)?.label ?? fullDef.label;
            return "\n\n" + "WR_ReasonTraining".Translate(
                fullLabel, skill.skillLabel.CapitalizeFirst(), def.gateMaxLevel);
        }

        /// Genes that make a pawn terrified of fire (Biotech's pyrophobia; extend
        /// here if mods add equivalents). Drives the No Firefighting plan rule.
        private static readonly HashSet<string> FireFearGenes = new HashSet<string> { "FireTerror" };

        private static ChipDisplay TableChips => WorkRolesMod.Settings?.chipDisplay ?? ChipDisplay.Normal;

        private static string AbbrevIfCompact(RoleStore store, Role role) =>
            TableChips == ChipDisplay.Compact ? AbbrevFor(store, role) : null;

        private static Dictionary<int, string> abbrevCache;
        private static int abbrevSignature;

        private static string AbbrevFor(RoleStore store, Role role)
        {
            int sig = store.roles.Count;
            foreach (var r in store.roles)
                sig = sig * 31 + (r.label?.GetHashCode() ?? 0) + r.id;
            if (abbrevCache == null || sig != abbrevSignature)
            {
                abbrevSignature = sig;
                abbrevCache = RoleAbbreviations.Build(
                    store.roles.Select(r => (r.id, r.label)).ToList());
            }
            return abbrevCache.TryGetValue(role.id, out var abbrev) ? abbrev : role.label;
        }

        // Abbreviation building lives in Core (RoleAbbreviations) with tests.

        // Skill columns (static so the static window sizing sees them; mirrored to
        // ModSettings so the table reopens exactly as closed).
        // sortSkill == null = the Order display option decides.
        private static readonly List<SkillDef> skillColumns = new List<SkillDef>();
        private static SkillDef sortSkill;
        private static bool skillColumnsLoaded;
        private const int MaxSkillColumns = 3;
        private const float SkillCellContentW = 82f; // "12.37" + passion/aptitude/expertise icons

        private static void EnsureSkillColumnsLoaded()
        {
            if (skillColumnsLoaded) return;
            skillColumnsLoaded = true;
            var settings = WorkRolesMod.Settings;
            if (settings?.skillColumns != null)
                foreach (var defName in settings.skillColumns)
                {
                    var def = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
                    if (def != null && !skillColumns.Contains(def) && skillColumns.Count < MaxSkillColumns)
                        skillColumns.Add(def);
                }
            sortSkill = settings?.sortSkill == null ? null
                : DefDatabase<SkillDef>.GetNamedSilentFail(settings.sortSkill);
            if (sortSkill != null && !skillColumns.Contains(sortSkill))
                sortSkill = null;
        }

        private static void SaveSkillColumns()
        {
            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            settings.skillColumns = skillColumns.Select(d => d.defName).ToList();
            settings.sortSkill = sortSkill?.defName;
            settings.Write();
        }

        /// Header label (localized) or cell content, whichever is wider.
        private static float SkillColumnWidth(SkillDef skill)
        {
            Text.Font = GameFont.Small;
            return Mathf.Max(SkillCellContentW, WrText.FitWidth(skill.skillLabel.CapitalizeFirst()) + 18f);
        }

        private static float SkillColumnsWidth()
        {
            EnsureSkillColumnsLoaded();
            float w = 0f;
            foreach (var skill in skillColumns) w += SkillColumnWidth(skill);
            return w;
        }

        private static float SkillSortValue(Pawn pawn, SkillDef skill)
        {
            var sr = pawn.skills?.GetSkill(skill);
            if (sr == null || sr.TotallyDisabled) return -1f;
            return sr.Level + Mathf.Clamp(sr.xpSinceLastLevel / sr.XpRequiredForLevelUp, 0f, 0.99f);
        }

        /// Skill-column sort (always descending) when active; else the Order option.
        private static List<Pawn> SortedForDisplay(List<Pawn> pawns)
        {
            EnsureSkillColumnsLoaded();
            if (sortSkill == null || !skillColumns.Contains(sortSkill))
                return OrderedForDisplay(pawns);
            return pawns
                .OrderByDescending(p => SkillSortValue(p, sortSkill))
                .ThenBy(p => p.LabelShortCap, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// Table display order (view-only; plan logic is order-independent): the
        /// vanilla colonist bar's order — including manual drag-reordering — or A-Z.
        private static List<Pawn> OrderedForDisplay(List<Pawn> pawns)
        {
            if (WorkRolesMod.Settings?.colonistOrder == ColonistOrder.Alphabetical)
                return pawns.OrderBy(p => p.LabelShortCap, System.StringComparer.OrdinalIgnoreCase).ToList();
            var bar = Find.ColonistBar?.GetColonistsInOrder();
            if (bar == null) return pawns;
            var pool = new HashSet<Pawn>(pawns);
            var ordered = bar.Where(pool.Contains).ToList();
            if (ordered.Count < pawns.Count)
                foreach (var pawn in pawns)
                    if (!ordered.Contains(pawn))
                        ordered.Add(pawn);
            return ordered;
        }

        private bool FiltersActive =>
            !colonistFilter.NullOrEmpty() || roleFilterId != -1 || kindFilter != KindFilter.All;

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
                        if (!role.blocker && role.Covers(selected))
                            matchIds.Add(role.id);
            }

            var result = new List<Pawn>();
            foreach (var pawn in pawns)
            {
                if (kindFilter == KindFilter.ColonistsOnly && pawn.IsSlaveOfColony) continue;
                if (kindFilter == KindFilter.SlavesOnly && !pawn.IsSlaveOfColony) continue;
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

            float stripWidth = rect.width - 16f - SkillColumnsWidth()
                - (PortraitSize + 6f + NameWidth + 2f + IconButton + 2f + IconButton + 8f + IconButton + 4f);

            // Header row (only while skill columns exist): column names with sort
            // and remove affordances.
            float headerH = skillColumns.Count > 0 ? 22f : 0f;
            if (headerH > 0f)
            {
                DrawSkillHeader(new Rect(rect.x, rect.y, rect.width - 16f, headerH - 2f));
                rect = new Rect(rect.x, rect.y + headerH, rect.width, rect.height - headerH);
            }

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
            // Slaves get the game's own sandy-yellow name color, as in vanilla lists.
            GUI.color = pawn.IsSlave ? PawnNameColorUtility.PawnNameColorOf(pawn) : Color.white;
            Widgets.Label(nameRect, pawn.LabelShortCap);
            GUI.color = Color.white;
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

            float cellX = pasteRect.xMax + 8f;
            foreach (var skill in skillColumns)
            {
                float colW = SkillColumnWidth(skill);
                DrawSkillCell(new Rect(cellX, rect.y, colW, rect.height), pawn, skill);
                cellX += colW;
            }

            var stripRect = new Rect(cellX, rect.y, stripWidth, rect.height);
            DrawChipStrip(stripRect, pawn, store, stripWidth);

            var plusRect = new Rect(rect.xMax - IconButton, rect.y + (rect.height - IconButton) / 2f, IconButton, IconButton);
            if (Widgets.ButtonImage(plusRect, TexButton.Plus))
                OpenAddMenu(pawn, store);

        }

        /// Header row above the table: "Colonist" (click = default sort) and one
        /// header per skill column (click = sort by it, descending; X = remove).
        private static void DrawSkillHeader(Rect rect)
        {
            const float FixedLeft = PortraitSize + 6f + NameWidth + 2f + IconButton + 2f + IconButton + 8f;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            bool wrap = Text.WordWrap;
            Text.WordWrap = false;

            var nameRect = new Rect(rect.x, rect.y, FixedLeft - 8f, rect.height);
            GUI.color = sortSkill == null ? Color.white : new Color(0.65f, 0.65f, 0.65f);
            Widgets.Label(nameRect, "WR_ColColonist".Translate());
            GUI.color = Color.white;
            if (Mouse.IsOver(nameRect)) Widgets.DrawHighlight(nameRect);
            if (Widgets.ButtonInvisible(nameRect)) { sortSkill = null; SaveSkillColumns(); }

            float x = rect.x + FixedLeft;
            SkillDef removed = null;
            foreach (var skill in skillColumns)
            {
                float colW = SkillColumnWidth(skill);
                var labelRect = new Rect(x, rect.y, colW - 16f, rect.height);
                var closeRect = new Rect(x + colW - 16f, rect.y + (rect.height - 12f) / 2f, 12f, 12f);
                GUI.color = sortSkill == skill ? Color.white : new Color(0.65f, 0.65f, 0.65f);
                Widgets.Label(labelRect, skill.skillLabel.CapitalizeFirst());
                GUI.color = Color.white;
                if (Mouse.IsOver(labelRect)) Widgets.DrawHighlight(labelRect);
                if (Widgets.ButtonInvisible(labelRect)) { sortSkill = skill; SaveSkillColumns(); }
                if (Widgets.ButtonImage(closeRect, TexButton.CloseXSmall)) removed = skill;
                x += colW;
            }
            if (removed != null)
            {
                skillColumns.Remove(removed);
                if (sortSkill == removed) sortSkill = null;
                SaveSkillColumns();
            }

            Text.WordWrap = wrap;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// One skill cell: fractional level in the stats panel's color language,
        /// then passion icon, aptitude square and expertise square; breakdown
        /// tooltip (traits, aptitude, custom passion) on hover.
        private static void DrawSkillCell(Rect cell, Pawn pawn, SkillDef skill)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            var sr = pawn.skills?.GetSkill(skill);
            if (sr == null || sr.TotallyDisabled)
            {
                GUI.color = ColorDisabled;
                Widgets.Label(new Rect(cell.x + 2f, cell.y, 44f, cell.height), "-");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float fractional = sr.Level + Mathf.Clamp(sr.xpSinceLastLevel / sr.XpRequiredForLevelUp, 0f, 0.99f);
            int passionScore = Passions.ScoreOf(sr.passion);
            GUI.color = sr.Level <= 1 ? ColorDisabled
                : sr.Level <= 5 ? ColorLow
                : passionScore == 2 ? ColorPassMajor
                : passionScore == 1 ? ColorPassMinor
                : Color.white;
            Widgets.Label(new Rect(cell.x + 2f, cell.y, 44f, cell.height), fractional.ToString("F2"));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            float ix = cell.x + 48f;
            var passionTex = sr.passion == Passion.Major ? WorkRolesTex.PassionMajor
                : sr.passion == Passion.Minor ? WorkRolesTex.PassionMinor
                : Passions.CustomIcon(sr.passion);
            if (passionTex != null)
            {
                GUI.DrawTexture(new Rect(ix, cell.y + (cell.height - 16f) / 2f, 16f, 16f), passionTex);
                ix += 18f;
            }
            if (sr.Aptitude != 0)
            {
                Widgets.DrawBoxSolid(new Rect(ix, cell.y + (cell.height - 6f) / 2f, 6f, 6f),
                    sr.Aptitude > 0 ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f));
                ix += 8f;
            }
            foreach (var expertise in Expertise.For(pawn))
            {
                if (expertise.Skill != skill) continue;
                Widgets.DrawBoxSolid(new Rect(ix, cell.y + (cell.height - 6f) / 2f, 6f, 6f),
                    new Color(1f, 0.85f, 0.3f));
                ix += 8f;
                break;
            }

            if (Mouse.IsOver(cell))
            {
                var tip = new List<string>();
                var traitList = pawn.story?.traits?.allTraits;
                if (traitList != null)
                    foreach (var trait in traitList)
                    {
                        if (trait.Suppressed) continue;
                        var gains = trait.CurrentData?.skillGains;
                        if (gains == null) continue;
                        foreach (var gain in gains)
                            if (gain.skill == skill && gain.amount != 0)
                                tip.Add($"{trait.LabelCap} {gain.amount:+0;-0}");
                    }
                if (sr.Aptitude != 0)
                    tip.Add("WR_SkillTipAptitude".Translate(sr.Aptitude.ToString("+0;-0")));
                string customPassion = Passions.CustomLabel(sr.passion);
                if (customPassion != null) tip.Add(customPassion);
                if (tip.Count > 0)
                    TooltipHandler.TipRegion(cell,
                        skill.skillLabel.CapitalizeFirst() + "\n" + string.Join("\n", tip));
            }
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
                        : () => RoleCommands.ToggleRoleForPawn(capturedPawn, capturedRoleId),
                    display: TableChips, abbrev: AbbrevIfCompact(store, role));
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

            // Pawn name directly below portrait, centered (slaves in vanilla's color)
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = selectedPawn.IsSlave ? PawnNameColorUtility.PawnNameColorOf(selectedPawn) : Color.white;
            Widgets.Label(new Rect(rect.x, rect.y + portraitBoxSize + 2f, portraitBoxSize, 20f),
                selectedPawn.LabelShortCap);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Trait list under the name (saves a trip to the Bio tab); tooltips
            // carry the vanilla trait descriptions.
            var pawnTraits = selectedPawn.story?.traits?.allTraits;
            if (pawnTraits != null)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                bool traitWrap = Text.WordWrap;
                Text.WordWrap = false;
                float traitY = rect.y + portraitBoxSize + 24f;
                foreach (var trait in pawnTraits)
                {
                    if (trait.Suppressed) continue;
                    if (traitY + 16f > rect.yMax) break;
                    var traitRect = new Rect(rect.x, traitY, portraitBoxSize, 16f);
                    Widgets.Label(traitRect, trait.LabelCap);
                    if (Mouse.IsOver(traitRect))
                        TooltipHandler.TipRegion(traitRect, trait.TipString(selectedPawn));
                    traitY += 16f;
                }
                Text.WordWrap = traitWrap;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

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
            var expertises = Expertise.For(selectedPawn);

            // Trait skill bonuses per skill (baked into levels at creation), for
            // the per-cell breakdown tooltip.
            var traitGains = new Dictionary<SkillDef, List<string>>();
            if (pawnTraits != null)
                foreach (var trait in pawnTraits)
                {
                    if (trait.Suppressed) continue;
                    var gains = trait.CurrentData?.skillGains;
                    if (gains == null) continue;
                    foreach (var gain in gains)
                    {
                        if (gain.skill == null || gain.amount == 0) continue;
                        if (!traitGains.TryGetValue(gain.skill, out var list))
                            traitGains[gain.skill] = list = new List<string>();
                        list.Add($"{trait.LabelCap} {gain.amount:+0;-0}");
                    }
                }

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

                // Text colour priority (custom VSE passions map by learn-rate score)
                int passionScore = Passions.ScoreOf(line.Passion);
                Color textColor;
                if (line.Disabled || line.Level <= 1)
                    textColor = ColorDisabled;
                else if (line.Level <= 5)
                    textColor = ColorLow;
                else if (passionScore == 2)
                    textColor = ColorPassMajor;
                else if (passionScore == 1)
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

                // Skill label (wrap off: a long modded skill name must clip, not
                // wrap out of the single-line cell)
                GUI.color = textColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                string labelText = line.Label;
                Vector2 labelSize = Text.CalcSize(labelText);
                float labelMaxW = SkillColWidth - 8f - (16f + 4f) - 48f; // col width - aptitude space - icon+gap - value col
                bool wrapWas = Text.WordWrap;
                Text.WordWrap = false;
                Widgets.Label(new Rect(xCursor, cellY, labelMaxW, CellH), labelText);
                Text.WordWrap = wrapWas;

                // Passion icon appended after label text; VSE custom passions draw
                // their own def icon with the passion's name as tooltip.
                const float IconW = 16f;
                float iconX = xCursor + Mathf.Min(labelSize.x, labelMaxW) + 4f;
                var passionTex = line.Passion == Passion.Major ? WorkRolesTex.PassionMajor
                    : line.Passion == Passion.Minor ? WorkRolesTex.PassionMinor
                    : Passions.CustomIcon(line.Passion);
                if (passionTex != null)
                {
                    GUI.color = Color.white;
                    var iconRect = new Rect(iconX, cellY + (CellH - IconW) / 2f, IconW, IconW);
                    GUI.DrawTexture(iconRect, passionTex);
                    string passionLabel = Passions.CustomLabel(line.Passion);
                    if (passionLabel != null && Mouse.IsOver(iconRect))
                        TooltipHandler.TipRegion(iconRect, passionLabel);
                    iconX += IconW + 2f;
                }

                // VSE expertise marker: gold square on the expertise's skill line,
                // tooltip carries the full description (name, level, effects).
                foreach (var expertise in expertises)
                {
                    if (expertise.Skill != line.Def) continue;
                    GUI.color = Color.white;
                    var markRect = new Rect(iconX, cellY + (CellH - 6f) / 2f, 6f, 6f);
                    Widgets.DrawBoxSolid(markRect, new Color(1f, 0.85f, 0.3f));
                    var tipRect = new Rect(iconX - 3f, cellY, 12f, CellH);
                    if (Mouse.IsOver(tipRect))
                        TooltipHandler.TipRegion(tipRect, expertise.Description);
                    iconX += 8f;
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

                // Breakdown tooltip: trait bonuses and aptitudes shaping this skill.
                var cellRect = new Rect(cellX, cellY, SkillColWidth, CellH);
                if (!line.Disabled && Mouse.IsOver(cellRect))
                {
                    var tipLines = new List<string>();
                    if (line.Def != null && traitGains.TryGetValue(line.Def, out var cellGains))
                        tipLines.AddRange(cellGains);
                    if (line.Aptitude != 0)
                        tipLines.Add("WR_SkillTipAptitude".Translate(line.Aptitude.ToString("+0;-0")));
                    if (tipLines.Count > 0)
                        TooltipHandler.TipRegion(cellRect, line.Label + "\n" + string.Join("\n", tipLines));
                }
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
            if (shipped != null && shipped.enabled && !shipped.HasRules && !shipped.blocker) return shipped;
            Role best = null;
            foreach (var role in store.roles)
            {
                if (!role.enabled || role.HasRules || role.blocker || !HasWorkTypeEntry(role, workType)) continue;
                if (best == null || role.entries.Count < best.entries.Count) best = role;
            }
            return best;
        }

        /// The medic-style role backing the doctoring redundancy floor: the shipped
        /// template when usable, else any enabled rule-free role made purely of
        /// Doctor-work jobs without being a full doctor (covering the Doctor role).
        private static Role MedicRole(RoleStore store, Role doctorRole)
        {
            var shipped = store.RoleByTemplate("WS_Medic");
            if (shipped != null && shipped.enabled && !shipped.HasRules) return shipped;
            foreach (var role in store.roles)
            {
                if (!role.enabled || role.HasRules || role.blocker || role == doctorRole || role.entries.Count == 0) continue;
                if (doctorRole != null && role.Covers(doctorRole)) continue;
                bool allDoctorWork = role.entries.All(e =>
                    e.Kind == JobEntryKind.WorkGiver
                    && DefDatabase<WorkGiverDef>.GetNamedSilentFail(e.DefName)?.workType?.defName == "Doctor");
                if (allDoctorWork) return role;
            }
            return null;
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

            // The ordering rules live in Core (TargetPlanner) with tests; this
            // adapter only projects game types into the planner's views.
            var catalog = store.roles.Select(r => new TargetRole
            {
                Id = r.id,
                Entries = r.entries,
                AutoAssign = r.autoAssign,
                HasRules = r.HasRules,
                Blocker = r.blocker,
                Unskilled = IsUnskilledRole(r),
                Doctoring = WorkTypesOf(r).Any(wt => wt.defName == "Doctor"),
                NaturalPriority = MaxNaturalPriority(r),
            }).ToList();

            var planned = TargetPlanner.Build(
                existing.Select(a => new PlannedAssignment
                { RoleId = a.roleId, Enabled = a.enabled, Pinned = a.pinned }).ToList(),
                catalog,
                recommendations.Select(r => r.id).ToList(),
                extraIds,
                promoted,
                hunterTier, hunterRoleId);

            return planned.Select(p => new RoleAssignment
            { roleId = p.RoleId, enabled = p.Enabled, pinned = p.Pinned }).ToList();
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

        /// One pawn projected into the Core engines' shape.
        private static RecPawn RecPawnOf(Pawn pawn)
        {
            var view = new RecPawn
            {
                HasRangedWeapon = pawn.equipment?.Primary?.def?.IsRangedWeapon == true,
                ShootingLevel = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0,
            };
            if (pawn.skills != null)
                foreach (var sr in pawn.skills.skills)
                {
                    if (sr.TotallyDisabled) continue;
                    view.SkillLevels[sr.def.defName] = sr.Level;
                    view.PassionScores[sr.def.defName] = Passions.Score(sr);
                    if (sr.Aptitude > 0) view.Aptitudes[sr.def.defName] = sr.Aptitude;
                }
            foreach (var expertise in Expertise.For(pawn))
                view.ExpertiseSkills.Add(expertise.Skill.defName);
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                if (!pawn.WorkTypeIsDisabled(workType))
                    view.CapableWorkTypes.Add(workType.defName);
            return view;
        }

        /// One catalog role projected into the Core engines' shape.
        private static RecRole RecRoleOf(Role role)
        {
            var def = role.templateDefName == null ? null
                : DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
            bool gateSkillKnown = def?.gateSkill != null
                && DefDatabase<SkillDef>.GetNamedSilentFail(def.gateSkill) != null;
            return new RecRole
            {
                Id = role.id,
                Entries = role.entries,
                AutoAssign = role.autoAssign,
                HasRules = role.HasRules,
                Blocker = role.blocker,
                Unskilled = IsUnskilledRole(role),
                Hunting = ProvidesHunting(role),
                NaturalPriority = MaxNaturalPriority(role),
                WorkTypes = WorkTypesOf(role).Select(wt => wt.defName).ToList(),
                GateSkill = gateSkillKnown ? def.gateSkill : null,
                GateMinLevel = def?.gateMinLevel ?? 0,
                GateMaxLevel = def?.gateMaxLevel ?? 0,
                GateNeedsPassion = def?.gateNeedsPassion ?? false,
                Enabled = role.enabled,
                Managed = role.managed,
                Gated = HasGate(role),
                SkipCoverage = role.templateDefName == "WS_Artist", // min 0: not required
                WantOverride = role.templateDefName == "WS_Researcher"
                    ? Mathf.Max(3, CountResearchBenches()) : 0,
            };
        }

        /// workType defName -> relevant skill defNames, for the roles' work types.
        private static Dictionary<string, IReadOnlyList<string>> WorkTypeSkillMap()
        {
            var map = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                if (workType.relevantSkills != null && workType.relevantSkills.Count > 0)
                    map[workType.defName] = workType.relevantSkills.Select(s => s.defName).ToList();
            return map;
        }

        private static Dictionary<string, int> SkillMaxLevelsByName(List<Pawn> pawns) =>
            SkillMaxLevels(pawns).ToDictionary(kv => kv.Key.defName, kv => kv.Value);

        /// Computes the recommendation list via the Core engine (see
        /// RecommendationEngine for the rules); this adapter projects game state
        /// and maps reason codes to translated tooltips. When reasons is given,
        /// each recommended role's id maps to a human-readable trigger.
        private static List<Role> ComputeRecommendations(Pawn pawn, RoleStore store,
            Dictionary<int, string> reasons = null)
        {
            if (pawn == null || pawn.skills == null) return new List<Role>();

            var recommendations = RecommendationEngine.Compute(
                store.roles.Select(RecRoleOf).ToList(),
                RecPawnOf(pawn),
                SkillMaxLevelsByName(ListedPawns()),
                WorkTypeSkillMap());

            // VSE expertise (empty without the mod): skill defName -> label.
            var expertiseBySkill = new Dictionary<string, string>();
            foreach (var expertise in Expertise.For(pawn))
                expertiseBySkill[expertise.Skill.defName] = expertise.Label;

            var result = new List<Role>();
            foreach (var rec in recommendations)
            {
                var role = store.RoleById(rec.RoleId);
                if (role == null) continue;
                result.Add(role);
                if (reasons == null) continue;
                string skillLabel = rec.SkillDefName == null ? ""
                    : DefDatabase<SkillDef>.GetNamedSilentFail(rec.SkillDefName)?.skillLabel.CapitalizeFirst() ?? "";
                reasons[role.id] =
                    rec.Reason == RecReason.Everyone ? "WR_ReasonEveryone".Translate()
                    : rec.Reason == RecReason.Duty ? "WR_ReasonDuty".Translate()
                    : rec.Reason == RecReason.Hunter ? "WR_ReasonHunter".Translate()
                    : rec.Reason == RecReason.Unskilled ? "WR_ReasonUnskilled".Translate()
                    : rec.Reason == RecReason.Expertise ? "WR_ReasonExpertise".Translate(
                        rec.SkillDefName != null && expertiseBySkill.TryGetValue(rec.SkillDefName, out var expertiseLabel)
                            ? expertiseLabel : skillLabel)
                    : rec.Reason == RecReason.MajorPassion ? "WR_ReasonMajorPassion".Translate(skillLabel)
                    : rec.Reason == RecReason.MinorPassion ? "WR_ReasonMinorPassion".Translate(skillLabel)
                    : rec.Reason == RecReason.Best ? "WR_ReasonBest".Translate(skillLabel)
                    : "WR_ReasonAptitude".Translate(skillLabel);
                reasons[role.id] += TrainingSuffix(role);
            }
            return result;
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

            // The colony passes (virtual sets, coverage, doctoring floor, fire
            // safety) live in Core (ColonyPlanner) with tests; this adapter projects
            // game state, resolves the special roles by content, and maps reason
            // codes to translated tooltips.
            var essentialRank = new Dictionary<int, int>();
            for (int i = 0; i < Essentials.Length; i++)
            {
                var essential = RoleProviding(store, Essentials[i].workType, Essentials[i].template);
                if (essential != null && !essentialRank.ContainsKey(essential.id))
                    essentialRank[essential.id] = i;
            }
            var hunterRole = RoleProviding(store, "Hunting", "WS_Hunter");
            var doctorRole = RoleProviding(store, "Doctor", "WS_Doctor");
            var medicRole = MedicRole(store, doctorRole);
            // Fire blocker: the shipped template, else any enabled rule-free
            // blocker carrying the Firefighter work type.
            var fireBlocker = store.RoleByTemplate("WS_NoFirefighting");
            if (fireBlocker != null && (!fireBlocker.enabled || fireBlocker.HasRules || !fireBlocker.blocker))
                fireBlocker = null;
            fireBlocker ??= store.roles.FirstOrDefault(r =>
                r.enabled && !r.HasRules && r.blocker && HasWorkTypeEntry(r, "Firefighter"));

            var planPawns = pawns.Select(pawn =>
            {
                store.pawnSets.TryGetValue(pawn, out var pawnSet);
                return new PlanPawn
                {
                    Rec = RecPawnOf(pawn),
                    Existing = (pawnSet?.assignments ?? new List<RoleAssignment>())
                        .Select(a => new PlannedAssignment
                        { RoleId = a.roleId, Enabled = a.enabled, Pinned = a.pinned })
                        .ToList(),
                    FireFear = pawn.genes != null
                        && pawn.genes.GenesListForReading.Any(g => FireFearGenes.Contains(g.def.defName)),
                };
            }).ToList();

            var colonyPlan = ColonyPlanner.Compute(
                store.roles.Select(RecRoleOf).ToList(), planPawns,
                SkillMaxLevelsByName(pawns), WorkTypeSkillMap(), essentialRank,
                hunterRole?.id ?? -1, doctorRole?.id ?? -1, medicRole?.id ?? -1,
                fireBlocker?.id ?? -1);

            // Grant reason codes -> translated tooltips.
            var doctorType = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Doctor");
            var coverageReasons = new Dictionary<Pawn, Dictionary<int, string>>();
            foreach (var grant in colonyPlan.Grants)
            {
                var pawn = pawns[grant.PawnIndex];
                if (!coverageReasons.TryGetValue(pawn, out var map))
                    coverageReasons[pawn] = map = new Dictionary<int, string>();
                string reason;
                if (grant.Reason == PlanReason.Hunter)
                    reason = "WR_ReasonHunter".Translate();
                else if (grant.Reason == PlanReason.FireFear)
                    reason = "WR_ReasonFireFear".Translate();
                else if (grant.Reason == PlanReason.Coverage)
                    reason = "WR_ReasonCoverage".Translate() + TrainingSuffix(store.RoleById(grant.RoleId));
                else if (grant.EssentialRank >= 0)
                    reason = "WR_ReasonEssential".Translate(
                        DefDatabase<WorkTypeDef>.GetNamedSilentFail(Essentials[grant.EssentialRank].workType)?.gerundLabel
                        ?? Essentials[grant.EssentialRank].workType);
                else // the doctoring backup
                    reason = "WR_ReasonEssential".Translate(doctorType?.gerundLabel ?? "Doctor");
                map[grant.RoleId] = reason;
            }

            // Final pass: per-pawn ordering (see TargetPlanner via
            // BuildOrderedTarget), then diff vs real assignments.
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                store.pawnSets.TryGetValue(pawn, out var set);
                var existing = set?.assignments ?? new List<RoleAssignment>();

                var recReasons = new Dictionary<int, string>();
                var target = BuildOrderedTarget(pawn, store,
                    ComputeRecommendations(pawn, store, recReasons), colonyPlan.VirtualSets[i],
                    colonyPlan.Promoted[i], colonyPlan.HunterTiers[i], hunterRole?.id ?? -1);

                // The fire-safety blocker leads the list: it must sit above Basics
                // (and everything else) to veto firefighting.
                if (fireBlocker != null && colonyPlan.FireGranted[i])
                {
                    int fireIdx = target.FindIndex(a => a.roleId == fireBlocker.id);
                    if (fireIdx > 0)
                    {
                        var grant = target[fireIdx];
                        target.RemoveAt(fireIdx);
                        target.Insert(0, grant);
                    }
                }

                var plan = new PawnFixPlan { pawn = pawn, target = target, reasons = recReasons };
                if (coverageReasons.TryGetValue(pawn, out var granted))
                    foreach (var kv in granted)
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
                .OrderBy(r => r.label, System.StringComparer.OrdinalIgnoreCase)
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
