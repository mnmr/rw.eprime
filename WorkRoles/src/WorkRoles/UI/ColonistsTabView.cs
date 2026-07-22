using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;
using WorkRoles.Signals;
using SignalSource = WorkRoles.Core.Recs.SignalSource;

namespace WorkRoles.UI
{
    /// Which surface a role tooltip serves; content is shared, only the
    /// actions section and pawn facts differ.
    public enum RoleTipContext { Palette, TreeRow, AssignmentChip }

    public class ColonistsTabView
    {
        // What varies between table instances (pawn source, settings storage,
        // optional panels) lives in the profile.
        private readonly ColonistsViewProfile profile;
        private readonly ColonistsRosterState rosterState;
        private readonly ColonistRecommendationState recommendationState;
        private readonly ColonistStatsState statsState;
        private readonly ColonistRoleCapabilityState roleCapabilityState;

        public ColonistsTabView(ColonistsViewProfile profile)
        {
            this.profile = profile;
            recommendationState = new ColonistRecommendationState();
            statsState = new ColonistStatsState();
            rosterState = new ColonistsRosterState(profile, statsState.SkillSortValue);
            roleCapabilityState = new ColonistRoleCapabilityState();
        }

        private Vector2 paletteScroll;
        private Pawn selectedPawn;

        // Our own table renderer: a fixed header row above a scroll view of
        // group sections and per-pawn rows (chip strips make row heights vary).
        private Vector2 tableScroll;
        private float lastTableViewH = 400f;
        private float EstimatedStripWidth = 300f;
        private const float TableHeaderH = 30f;
        private const float GroupHeaderH = 30f;

        // Flattened group-header/pawn geometry. VariableViewportLayout owns the
        // prefix offsets, so normal IMGUI passes binary-search the visible rows
        // instead of rescanning the complete colony to recover y positions.
        private readonly struct TableLayoutRow
        {
            internal TableLayoutRow(GroupSection<Pawn> section, Pawn pawn)
            {
                Section = section;
                Pawn = pawn;
            }

            internal GroupSection<Pawn> Section { get; }
            internal Pawn Pawn { get; }
        }

        private IReadOnlyList<GroupSection<Pawn>> tableLayoutSections;
        private readonly List<TableLayoutRow> tableLayoutRows = new List<TableLayoutRow>();
        private VariableViewportLayout tableRowLayout;
        private ScopeCacheStamp tableLayoutStamp = ScopeCacheStamp.Invalid;
        private float tableLayoutStripWidth = -1f;
        private int tableLayoutDisplay = -1;
        private int tableListedCount;

        /// One view-owned revision for every cache whose contents depend on the
        /// active pawn scope. Reading it observes map transitions first.
        internal int PawnListRevision => rosterState.PawnListRevision;
        private ScopeCacheStamp PawnListStamp => rosterState.PawnListStamp;

        // View-local table filters (never synced, never persisted).
        private const string SearchControlName = "WR_ColonistSearch";
        private bool focusSearch;

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
        private const float SkillColWidth = 200f;   // minimum; signal decorators may widen both columns
        private const int   SkillCols = 2;
        private const float CellH = 20f;
        private const float StatsPadding = 12f;     // top+bottom padding inside box
        private const float ColSepWidth = 2f;       // separator width
        private const float ColSepMargin = 16f;     // space on each side of separator
        private const float SkillDecoratorSize = 16f;
        private const float SkillDecoratorGap = 2f;
        private const float SkillLabelDecoratorGap = 4f;
        private const float SkillValueGap = 8f;
        private const float SkillValueWidth = 48f;

        public void Reset()
        {
            paletteScroll = Vector2.zero;
            tableScroll = Vector2.zero;
            selectedPawn = null;
            rosterState.Reset();
            ColonyGroupsDataSource.InvalidateSnapshot(); // fresh membership per window open
            roleCapabilityState.Invalidate();
            recommendationState.Reset();
            InvalidatePawnSnapshot();
            statsState.Reset(rosterState.SnapshotPawns());
            // Opening re-snapshots everything (stats would otherwise stay stale
            // across a reopen when nothing bumped the version in between).
            sizeStamp = chipLayoutStamp = roleTipStamp = rulesPassStamp
                = ScopeCacheStamp.Invalid;
            paletteStamp = -1;
            InvalidateTableLayout();
        }

        /// Language-only invalidation. User selection, filters, scroll positions,
        /// scope and disclosure state remain untouched.
        internal void InvalidateLanguageCaches()
        {
            colonistHeaderCache = null;
            sizeStamp = ScopeCacheStamp.Invalid;

            paletteStamp = -1;
            paletteChips.Clear();
            paletteLabels.Clear();

            statsState.InvalidateLanguageCaches();
            roleCapabilityState.Invalidate();
            chipLayouts.Clear();
            chipLayoutStamp = ScopeCacheStamp.Invalid;

            roleTipCache.Clear();
            roleTipStamp = ScopeCacheStamp.Invalid;
            recommendationState.InvalidateLanguageCaches();

            rosterState.InvalidateLanguageCaches();
            InvalidateTableLayout();
        }

        /// Reset-time only: every plan input (roles, assignments, pins, training,
        /// recommendation order, pawn membership) bumps UiVersion when its command
        /// EXECUTES — click-site invalidation would rebuild from pre-command state
        /// in MP and never fire on other clients.
        public void InvalidateRecommendationCache() => recommendationState.InvalidatePlan();

        /// The pawn's signals and derived skill buckets belong to the current
        /// explicit external snapshot generation.
        internal PawnSignalSnapshot SignalSnapshotFor(Pawn pawn)
            => statsState.SignalSnapshot(pawn);

        internal PawnExternalSnapshot ExternalSnapshotFor(Pawn pawn)
            => statsState.ExternalSnapshot(pawn);

        /// Checks only the event-driven UI revision. A matching generation does
        /// no pawn work; a changed revision recaptures every possible scope once.
        internal void RefreshExternalSnapshotIfNeeded()
        {
            if (!statsState.NeedsExternalSnapshotRefresh) return;

            // An authorized WorkRoles/time-rule event starts a complete new UI
            // generation. External integrations are not polled, but anything
            // they expose is re-read as part of that explicit generation.
            ColonyGroupsDataSource.InvalidateSnapshot();
            if (!statsState.RefreshExternalSnapshot(rosterState.SnapshotPawns())) return;

            // A command can bump UiVersion during an input pass, allowing one
            // of these consumers to rebuild before the next Layout installs
            // the new external generation. Clear them after installation so
            // no pre-refresh result can carry the new revision stamp.
            recommendationState.InvalidatePlan();
            roleCapabilityState.Invalidate();
            rosterState.InvalidateSnapshotConsumers();
            sizeStamp = ScopeCacheStamp.Invalid;
            chipLayouts.Clear();
            chipLayoutStamp = ScopeCacheStamp.Invalid;
            roleTipCache.Clear();
            roleTipStamp = ScopeCacheStamp.Invalid;
            rulesPassCache.Clear();
            rulesPassStamp = ScopeCacheStamp.Invalid;
            InvalidateTableLayout();
        }

        /// Window close: drop pawn-keyed snapshots so a save unloaded while the
        /// window is closed cannot stay pinned through them.
        internal void ReleaseSnapshots()
        {
            statsState.ReleaseSnapshots();
            roleCapabilityState.Invalidate();

            selectedPawn = null;
            recommendationState.ReleaseSnapshots();

            roleTipCache.Clear();
            roleTipStamp = ScopeCacheStamp.Invalid;
            rosterState.ReleaseSnapshots();
            chipLayouts.Clear();
            chipLayoutStamp = ScopeCacheStamp.Invalid;
            rulesPassCache.Clear();
            rulesPassStamp = ScopeCacheStamp.Invalid;
            InvalidateTableLayout();

            ColonyGroupsDataSource.InvalidateSnapshot();
            paletteChips.Clear();
            paletteLabels.Clear();
            paletteStamp = -1;
            colonistHeaderCache = null;
            sizeStamp = ScopeCacheStamp.Invalid;

            // Role labels are save-owned even though this cache is shared by
            // the table instances.
            abbrevCache = null;
            abbrevStamp = -1;
        }

        private void InvalidateTableLayout()
        {
            tableLayoutSections = null;
            tableLayoutRows.Clear();
            tableRowLayout = null;
            tableLayoutStamp = ScopeCacheStamp.Invalid;
            tableLayoutStripWidth = -1f;
            tableLayoutDisplay = -1;
            tableListedCount = 0;
        }

        // ----- Window sizing helpers -----

        /// <summary>Height of the stats panel for a given pawn (or generic if null).</summary>
        public float StatsPanelHeight(Pawn pawn = null)
        {
            int lineCount = 12;
            if (pawn != null)
                lineCount = statsState.Snapshot(pawn).Skills.Count;
            int rows = (lineCount + SkillCols - 1) / SkillCols;
            float portraitSection = PortraitDisplaySize + 2f + 20f; // portrait + gap + name label
            float skillSection = rows * CellH;
            float contentH = Mathf.Max(portraitSection, skillSection);
            return contentH + StatsPadding * 2f;
        }

        // Open-window snapshot of the desired window size: both walk every
        // pawn's chips through text measurement, so they recompute only when
        // the stamp, map, chip display, palette mode or skill columns change.
        private ScopeCacheStamp sizeStamp = ScopeCacheStamp.Invalid;
        private int sizeMapId = -1;
        private int sizeKey = -1;
        private float desiredWidthCache;
        private float desiredHeightCache;

        private void EnsureSizes()
        {
            IReadOnlyList<SkillDef> columns = rosterState.SkillColumns;
            int mapId = Find.CurrentMap?.uniqueID ?? -1;
            // Column IDENTITY, not count: swapping a column at the cap keeps the
            // count identical and must still invalidate.
            PaletteMode paletteMode = WorkRolesMod.Settings?.paletteMode ?? PaletteMode.Skills;
            int key = (((int)TableChips * 31 + (int)paletteMode) * 31 + columns.Count) * 31
                + (selectedPawn?.thingIDNumber ?? -1);
            foreach (var column in columns)
                key = key * 31 + (column?.shortHash ?? 0);
            ScopeCacheStamp stamp = PawnListStamp;
            if (sizeStamp == stamp && sizeMapId == mapId && sizeKey == key) return;
            sizeMapId = mapId;
            sizeKey = key;
            desiredWidthCache = ComputeDesiredWidth();
            desiredHeightCache = ComputeDesiredHeight();
            sizeStamp = PawnListStamp;
        }

        public float DesiredWidth()
        {
            if (RoleStore.Current == null || Find.CurrentMap == null) return DefaultWidth;
            EnsureSizes();
            return desiredWidthCache;
        }

        private float ComputeDesiredWidth()
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
                    RoleCapabilityPresentation capability =
                        roleCapabilityState.PresentationFor(
                            pawn, role, PawnListStamp, ExternalSnapshotFor(pawn));
                    w += TableChipWidth(store, role, a.pinned, capability) + ChipGap;
                }
                if (w > widestStrip) widestStrip = w;
            }
            float tableWidth = fixedLeft + SkillColumnsWidth() + widestStrip;
            float skillColumnWidth = SkillColWidth;
            if (selectedPawn != null)
                skillColumnWidth = statsState.Snapshot(selectedPawn).SkillColumnWidth;
            float statsWidth = StatsPadding * 2f + PortraitDisplaySize + 16f
                + SkillCols * skillColumnWidth
                + SkillCols * (ColSepMargin * 2f + ColSepWidth);
            return Mathf.Max(tableWidth, statsWidth);
        }

        public float DesiredHeight()
        {
            if (RoleStore.Current == null || Find.CurrentMap == null) return DefaultHeight;
            EnsureSizes();
            return desiredHeightCache;
        }

        private float ComputeDesiredHeight()
        {
            var store = RoleStore.Current;
            if (store == null || Find.CurrentMap == null) return DefaultHeight;

            float chrome = 80f;
            float paletteSection = PaletteHeight(store, desiredWidthCache - 16f - PaletteModeW) + 8f + FilterRowH + 4f;
            float statsPanel = StatsPanelHeight() + StatsPanelMargin;
            float tableContent = 0f;
            var pawns = ListedPawns();
            foreach (var pawn in pawns)
            {
                store.pawnSets.TryGetValue(pawn, out var set);
                var assignments = set?.assignments ?? new List<RoleAssignment>();
                float stripW = TableStripWidth(desiredWidthCache);
                float stripH = LayoutChips(stripW, assignments, store, pawn, result: null);
                tableContent += Mathf.Max(RowHeight, stripH + 8f);
            }
            return chrome + paletteSection + tableContent + statsPanel;
        }

        /// The one width formula for a table chip — measurement and layout must
        /// never disagree.
        private float TableChipWidth(RoleStore store, Role role, bool pinned,
            RoleCapabilityPresentation capability) =>
            RoleChipUI.WidthFor(role, showRemove: true, TableChips,
                AbbrevIfCompact(store, role), pinned, capability.WarningSeverity);

        /// The roles-column width used by both desired-height measurement and
        /// live table layout.
        private float TableStripWidth(float tableWidth) => Mathf.Max(300f,
            tableWidth - 16f - 264f - SkillColumnsWidth() - 28f);

        private float LayoutChips(float stripWidth, List<RoleAssignment> assignments,
            RoleStore store, Pawn pawn, List<RoleChipLayout> result)
        {
            float x = 0f, y = 0f;
            int line = 0;
            foreach (var a in assignments)
            {
                var role = store.RoleById(a.roleId);
                if (role == null) continue;
                RoleCapabilityPresentation capability =
                    roleCapabilityState.PresentationFor(
                        pawn, role, PawnListStamp, ExternalSnapshotFor(pawn));
                float w = TableChipWidth(store, role, a.pinned, capability);
                if (x + w > stripWidth && x > 0f)
                {
                    line++;
                    x = 0f;
                    y += RoleChipUI.Height + ChipGap;
                }
                result?.Add(new RoleChipLayout(a,
                    new Rect(x, y, w, RoleChipUI.Height), line, capability));
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
            float paletteH = PaletteHeight(store, rect.width - 16f - PaletteModeW);
            float filterTop = rect.y + paletteH + 8f;
            float tableTop = filterTop + FilterRowH + 4f;

            DrawPalette(new Rect(rect.x, rect.y, rect.width, paletteH), store);

            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + paletteH + 4f, rect.width, 2f),
                new Color(1f, 1f, 1f, 0.25f));

            DrawFilterRow(new Rect(rect.x, filterTop, rect.width, FilterRowH), store);
            DrawPawnTable(new Rect(rect.x, tableTop, rect.width, tableBottom - tableTop), store);
            DrawStatsPanel(new Rect(rect.x, tableBottom + StatsPanelMargin, rect.width, statsPanelH), store);

            RoleChipUI.DrawDragGhost(store);
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
                // The XP-frequency primary skill, not the first entry's work
                // type: an auxiliary lead entry (Hunter's finish-off) must not
                // relabel the cluster.
                var primary = DefDatabase<SkillDef>.GetNamedSilentFail(
                    RecsAdapter.PrimarySkillOf(root) ?? "");
                if (primary == null)
                    return unskilled ??= new PaletteCluster { label = "WR_ClusterUnskilled".Translate() };
                string label = primary.LabelCap;
                var cluster = skillClusters.FirstOrDefault(c => c.label == label);
                if (cluster == null)
                {
                    cluster = new PaletteCluster { label = label };
                    skillClusters.Add(cluster);
                }
                return cluster;
            }

            // Tree rows repeat a child under every covering root (including
            // virtual cross-group rows); the palette shows each role once,
            // clustered under its first real appearance.
            var seen = new HashSet<int>();
            foreach (var (role, parent, virtualRow) in RolesListState.BuildRoleTree(store).rows)
                if (!virtualRow && seen.Add(role.id))
                    ClusterFor(parent ?? role).roles.Add(role);

            var result = new List<PaletteCluster>();
            if (everyone != null) result.Add(everyone);
            result.AddRange(skillClusters);
            if (unskilled != null) result.Add(unskilled);
            return result;
        }

        /// Palette arrangement: skill clusters (default) or the role list's
        /// groups in player order (same sections the Roles tab shows).
        private static List<PaletteCluster> PaletteClusters(RoleStore store)
        {
            if (WorkRolesMod.Settings?.paletteMode != PaletteMode.Groups)
                return BuildPaletteClusters(store);
            return RolesListState.BuildSections(store, nested: true)
                .Where(section => section.rows.Count > 0)
                .Select(section => new PaletteCluster
                {
                    label = section.title,
                    // Real rows only, once each: virtual cross-group rows and
                    // same-group duplicates stay a role-tree display device.
                    roles = section.rows.Where(t => !t.virtualRow)
                        .Select(t => t.role).Distinct().ToList(),
                })
                .ToList();
        }

        /// Lays out the palette line by line: each cluster stays WHOLE on one
        /// line, placed on the earliest line with room (first fit), so later
        /// clusters back-fill earlier gaps instead of opening fresh lines.
        /// Splitting across lines is the fallback for a cluster wider than a
        /// full line by itself; a split cluster repeats its Tiny label on every
        /// line and leaves its last partial line open for back-fill. Returns
        /// content height; pass null lists to measure only.
        private static float LayoutPalette(RoleStore store, float rowWidth,
            List<(Role role, Rect rect)> chips, List<(string label, Rect rect)> labels)
        {
            float lineH = ClusterLabelH + 2f + RoleChipUI.Height;
            var cursors = new List<float>();   // per-line x cursor; index gives y
            float YOf(int line) => line * (lineH + ClusterGapY);

            foreach (var cluster in PaletteClusters(store))
            {
                if (cluster.roles.Count == 0) continue;
                Text.Font = GameFont.Tiny;
                float labelW = WrText.FitWidth(cluster.label);
                Text.Font = GameFont.Small;

                var widths = new List<float>(cluster.roles.Count);
                float chipsW = 0f;
                foreach (var role in cluster.roles)
                {
                    float chipW = RoleChipUI.WidthFor(role, showRemove: false);
                    widths.Add(chipW);
                    chipsW += (chipsW > 0f ? ChipGap : 0f) + chipW;
                }
                float clusterW = Mathf.Max(labelW, chipsW);

                if (clusterW <= rowWidth)
                {
                    int line = -1;
                    for (int i = 0; i < cursors.Count; i++)
                    {
                        float needed = (cursors[i] > 0f ? ClusterGapX : 0f) + clusterW;
                        if (cursors[i] + needed <= rowWidth) { line = i; break; }
                    }
                    if (line < 0)
                    {
                        cursors.Add(0f);
                        line = cursors.Count - 1;
                    }
                    float x = cursors[line] + (cursors[line] > 0f ? ClusterGapX : 0f);
                    float y = YOf(line);
                    labels?.Add((cluster.label, new Rect(x, y, labelW, ClusterLabelH)));
                    float cx = x;
                    for (int i = 0; i < widths.Count; i++)
                    {
                        chips?.Add((cluster.roles[i],
                            new Rect(cx, y + ClusterLabelH + 2f, widths[i], RoleChipUI.Height)));
                        cx += widths[i] + ChipGap;
                    }
                    cursors[line] = x + clusterW;
                }
                else
                {
                    // Fallback: wider than a full line. Wrap over fresh lines
                    // (the same flow the whole palette used to use).
                    cursors.Add(0f);
                    int line = cursors.Count - 1;
                    float x = 0f, segStart = 0f;
                    bool segmentOpen = false;
                    for (int i = 0; i < widths.Count; i++)
                    {
                        // Opening a segment needs room for the label AND its
                        // first chip; continuing one only for the next chip.
                        float needed = segmentOpen ? widths[i] : Mathf.Max(labelW, widths[i]);
                        if (x > 0f && x + needed > rowWidth)
                        {
                            cursors.Add(0f);
                            line = cursors.Count - 1;
                            x = 0f;
                            segmentOpen = false;
                        }
                        if (!segmentOpen)
                        {
                            labels?.Add((cluster.label,
                                new Rect(x, YOf(line), Mathf.Min(labelW, rowWidth - x), ClusterLabelH)));
                            segStart = x;
                            segmentOpen = true;
                        }
                        chips?.Add((cluster.roles[i],
                            new Rect(x, YOf(line) + ClusterLabelH + 2f, widths[i], RoleChipUI.Height)));
                        x += widths[i] + ChipGap;
                    }
                    cursors[line] = Mathf.Max(x - ChipGap, segStart + labelW);
                }
            }
            return cursors.Count == 0 ? 0f
                : cursors.Count * lineH + (cursors.Count - 1) * ClusterGapY;
        }

        // Open-window snapshot of the palette layout (it rebuilds the whole
        // role tree): single slot keyed by stamp, width and arrangement.
        private int paletteStamp = -1;
        private float paletteLayoutW = -1f;
        private int paletteLayoutMode = -1;
        private float paletteContentH;
        private readonly List<(Role role, Rect rect)> paletteChips = new List<(Role, Rect)>();
        private readonly List<(string label, Rect rect)> paletteLabels = new List<(string, Rect)>();

        private float PaletteLayout(RoleStore store, float rowWidth)
        {
            int mode = (int)(WorkRolesMod.Settings?.paletteMode ?? PaletteMode.Skills);
            if (paletteStamp != UiVersion.Current || paletteLayoutW != rowWidth || paletteLayoutMode != mode)
            {
                paletteStamp = UiVersion.Current;
                paletteLayoutW = rowWidth;
                paletteLayoutMode = mode;
                paletteChips.Clear();
                paletteLabels.Clear();
                paletteContentH = LayoutPalette(store, rowWidth, paletteChips, paletteLabels);
            }
            return paletteContentH;
        }

        private float PaletteHeight(RoleStore store, float rowWidth)
            => WorkRolesMod.Settings?.paletteMode == PaletteMode.Hidden
                ? 26f // just the mode button, so the palette can come back
                : Mathf.Min(PaletteLayout(store, rowWidth) + PalettePadding, PaletteMaxHeight);

        /// Width the palette mode button reserves in the panel's top-right.
        private const float PaletteModeW = 76f;

        private void DrawPalette(Rect rect, RoleStore store)
        {
            // Arrangement button, cycling Skills -> Groups -> Hidden. Hidden
            // collapses the palette to just this button.
            var modeRect = new Rect(rect.xMax - PaletteModeW + 6f, rect.y, PaletteModeW - 12f, 22f);
            var settings = WorkRolesMod.Settings;
            var mode = settings?.paletteMode ?? PaletteMode.Skills;
            TooltipHandler.TipRegion(modeRect, "WR_PaletteModeTip".Translate());
            if (Widgets.ButtonText(modeRect,
                    (mode == PaletteMode.Groups ? "WR_PaletteByGroups"
                    : mode == PaletteMode.Hidden ? "WR_PaletteHidden"
                    : "WR_PaletteBySkills").Translate())
                && settings != null)
            {
                settings.paletteMode = (PaletteMode)(((int)mode + 1) % 3);
                settings.Write();
            }
            if (mode == PaletteMode.Hidden) return;

            float rowWidth = rect.width - 16f - PaletteModeW;
            float contentHeight = PaletteLayout(store, rowWidth);
            var chips = paletteChips;
            var labels = paletteLabels;

            var scrollRect = new Rect(rect.x, rect.y, rect.width - PaletteModeW, rect.height);
            Widgets.BeginScrollView(scrollRect, ref paletteScroll, new Rect(0f, 0f, rowWidth, contentHeight));
            float visibleTop = paletteScroll.y;
            float visibleBottom = visibleTop + scrollRect.height;
            bool repaint = Event.current.type == EventType.Repaint;

            Text.Font = GameFont.Tiny;
            GUI.color = WrStyle.CaptionText;
            foreach (var (label, labelRect) in labels)
                if (repaint && labelRect.yMax >= visibleTop && labelRect.y <= visibleBottom)
                    Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            foreach (var (role, chipRect) in chips)
            {
                bool visible = chipRect.yMax >= visibleTop && chipRect.y <= visibleBottom;
                // The click closure allocates: create it only on the one pass
                // that can consume it (left mouse-down inside this chip).
                System.Action onClick = null;
                var pressEvent = Event.current;
                if (visible && pressEvent.type == EventType.MouseDown && pressEvent.button == 0
                    && chipRect.Contains(pressEvent.mousePosition))
                {
                    int capturedId = role.id;
                    // Shift-click appends the role to the selected colonist; plain
                    // click keeps toggling the role globally.
                    onClick = () =>
                    {
                        if (Event.current != null && Event.current.shift)
                        {
                            // TryGetValue, not SetFor: pawnSets is synced world
                            // state — a read-only check must not create entries
                            // locally outside the synced command.
                            var target = selectedPawn;
                            var checkStore = RoleStore.Current;
                            if (target != null && checkStore != null)
                            {
                                checkStore.pawnSets.TryGetValue(target, out var targetSet);
                                if (targetSet == null || !targetSet.assignments.Any(a => a.roleId == capturedId))
                                    RoleCommands.AssignRole(target, capturedId);
                            }
                        }
                        else
                        {
                            RoleCommands.ToggleRoleGlobal(capturedId);
                        }
                    };
                }
                var click = RoleChipUI.Draw(chipRect, role, role.enabled ? ChipStyle.Normal : ChipStyle.Disabled,
                    showRemove: false, dragSource: null, onClick: onClick,
                    paint: repaint && visible);
                if (visible && Mouse.IsOver(chipRect))
                    TooltipHandler.TipRegion(chipRect, RoleTipText(role, RoleTipContext.Palette));
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
            GUI.SetNextControlName(SearchControlName);
            rosterState.Search = Widgets.TextField(
                new Rect(rect.x + SearchLabelW + 4f, y, SearchW, SearchH),
                rosterState.Search);

            // Ctrl+F hands focus to the search box with any existing text
            // selected: typing replaces it, End keeps it and drops the caret
            // at the end.
            if (focusSearch)
            {
                GUI.FocusControl(SearchControlName);
                if (GUI.GetNameOfFocusedControl() == SearchControlName)
                {
                    var editor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
                    if (editor != null)
                    {
                        editor.SelectAll();
                        focusSearch = false;
                    }
                }
            }

            // Deleted roles drop the filter rather than filtering everyone out.
            rosterState.ValidateRoleFilter(store);

            float btnX = rect.x + SearchLabelW + 4f + SearchW + 12f;
            string btnLabel = rosterState.RoleFilterId == -1
                ? "WR_FilterAllRoles".Translate()
                : store.RoleById(rosterState.RoleFilterId).label;
            if (Widgets.ButtonText(new Rect(btnX, y, RoleBtnW, SearchH), btnLabel))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("WR_FilterAllRoles".Translate(),
                        () => rosterState.RoleFilterId = -1)
                };
                foreach (var role in store.roles.OrderBy(r => r.label, System.StringComparer.OrdinalIgnoreCase))
                {
                    int id = role.id;
                    options.Add(new FloatMenuOption(role.label,
                        () => rosterState.RoleFilterId = id));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Scope dropdown: which locations' pawns the table lists (options
            // come from the pawn snapshot — ListedPawns keeps them fresh).
            float scopeX = btnX + RoleBtnW + 8f;
            IReadOnlyList<ScopeOption> scopeOptions = rosterState.ScopeOptions;
            if (Widgets.ButtonText(new Rect(scopeX, y, RoleBtnW, SearchH),
                    ColonyScope.LabelOf(rosterState.Scope)))
            {
                var menu = new List<FloatMenuOption>();
                foreach (var option in scopeOptions)
                {
                    var captured = option;
                    var item = new FloatMenuOption(ColonyScope.LabelOf(option), () =>
                    {
                        rosterState.SelectScope(captured);
                    });
                    if (option.IsShip) item.tooltip = "WR_ShipTip".Translate();
                    menu.Add(item);
                }
                Find.WindowStack.Add(new FloatMenu(menu));
            }

            if (rosterState.FiltersActive)
            {
                var clearRect = new Rect(scopeX + RoleBtnW + 8f, y + (SearchH - 18f) / 2f, 18f, 18f);
                TooltipHandler.TipRegion(clearRect, "WR_ClearFilters".Translate());
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                {
                    rosterState.Search = "";
                    rosterState.RoleFilterId = -1;
                }
            }

            // Right cluster: grouping, Skills column picker, and the Display
            // options button. Display prefs are per-player ModSettings, never
            // world state.
            if (WorkRolesMod.Settings != null)
            {
                const float DisplayBtnW = 90f;
                var displayRect = new Rect(rect.xMax - DisplayBtnW, y, DisplayBtnW, SearchH);
                TooltipHandler.TipRegion(displayRect, "WR_DisplayOptions".Translate());
                if (Widgets.ButtonText(displayRect, "WR_DisplayButton".Translate()))
                {
                    string ChipsLabel(ChipDisplay d) =>
                        (TableChips == d ? "✓ " : "") +
                        (d == ChipDisplay.Compact ? "WR_ChipsCompact".Translate()
                        : d == ChipDisplay.Minimal ? "WR_ChipsMinimal".Translate()
                        : "WR_ChipsNormal".Translate());
                    void SetChips(ChipDisplay d) => profile.SetTableChips(d);
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        new FloatMenuOption(ChipsLabel(ChipDisplay.Normal), () => SetChips(ChipDisplay.Normal)),
                        new FloatMenuOption(ChipsLabel(ChipDisplay.Compact), () => SetChips(ChipDisplay.Compact)),
                        new FloatMenuOption(ChipsLabel(ChipDisplay.Minimal), () => SetChips(ChipDisplay.Minimal)),
                    }));
                }

                float groupRight = displayRect.x; // group button abuts Display when the skills UI is off
                if (profile.ShowSkills)
                {
                    const float SkillsBtnW = 110f;
                    var skillsRect = new Rect(displayRect.x - 8f - SkillsBtnW, y, SkillsBtnW, SearchH);
                    IReadOnlyList<SkillDef> columns = rosterState.SkillColumns;
                    string skillsLabel = columns.Count == 0
                        ? "WR_SkillsButton".Translate().ToString()
                        : "WR_SkillsButtonCount".Translate(columns.Count,
                            ColonistsRosterState.MaxSkillColumns).ToString();
                    if (Widgets.ButtonText(skillsRect, skillsLabel))
                    {
                        var options = new List<FloatMenuOption>();
                        foreach (var skill in DefDatabase<SkillDef>.AllDefsListForReading)
                        {
                            var captured = skill;
                            bool added = columns.Contains(skill);
                            string label = (added ? "✓ " : "") + skill.skillLabel.CapitalizeFirst();
                            options.Add(new FloatMenuOption(label, () =>
                            {
                                rosterState.ToggleSkillColumn(captured);
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                    groupRight = skillsRect.x;
                }

                // Grouping sits with the display controls: it changes how the
                // table renders, not which pawns it lists.
                const float GroupBtnW = 130f;
                var groupRect = new Rect(groupRight - 8f - GroupBtnW, y, GroupBtnW, SearchH);
                if (Widgets.ButtonText(groupRect, rosterState.CurrentGroupSource.Label))
                {
                    var menu = new List<FloatMenuOption>();
                    foreach (var source in GroupSources.All())
                    {
                        var captured = source;
                        menu.Add(new FloatMenuOption(
                            (profile.GetGroupBy() == captured.Key ? "✓ " : "") + captured.Label, () =>
                            {
                                profile.SetGroupBy(captured.Key);
                                ColonyGroupsDataSource.InvalidateSnapshot();
                            }));
                    }
                    Find.WindowStack.Add(new FloatMenu(menu));
                }
            }
        }

        private ChipDisplay TableChips => profile.GetTableChips();

        private string AbbrevIfCompact(RoleStore store, Role role) =>
            TableChips == ChipDisplay.Compact ? AbbrevFor(store, role) : null;

        // Role-catalog-scoped (not per view), so deliberately shared statics.
        private static Dictionary<int, string> abbrevCache;
        private static int abbrevStamp;

        private static string AbbrevFor(RoleStore store, Role role)
        {
            // Renames and role changes bump UiVersion, so the stamp replaces
            // the old per-call label-hash signature.
            if (abbrevCache == null || abbrevStamp != UiVersion.Current)
            {
                abbrevStamp = UiVersion.Current;
                abbrevCache = RoleAbbreviations.Build(
                    store.roles.Select(r => (r.id, r.label)).ToList());
            }
            return abbrevCache.TryGetValue(role.id, out var abbrev) ? abbrev : role.label;
        }

        // Abbreviation building lives in Core (RoleAbbreviations) with tests.

        private const float SkillCellContentW = 82f; // "12.37" + up to two signal decorators

        // CapitalizeFirst allocates for lowercase labels; header labels are
        // needed per column per pass, so memoize per def (language switch clears).
        private static readonly Dictionary<SkillDef, string> skillHeaderLabels =
            new Dictionary<SkillDef, string>();

        internal static string SkillHeaderLabel(SkillDef skill)
        {
            if (!skillHeaderLabels.TryGetValue(skill, out string label))
                skillHeaderLabels[skill] = label = skill.skillLabel.CapitalizeFirst();
            return label;
        }

        internal static void InvalidateSharedLanguageCaches()
        {
            skillHeaderLabels.Clear();
        }

        /// Header label (localized) or cell content, whichever is wider.
        internal float SkillColumnWidth(SkillDef skill)
        {
            Text.Font = GameFont.Small;
            return Mathf.Max(SkillCellContentW, WrText.FitWidth(SkillHeaderLabel(skill)) + 18f);
        }

        private float SkillColumnsWidth()
        {
            float w = 0f;
            foreach (SkillDef skill in rosterState.SkillColumns)
                w += SkillColumnWidth(skill);
            return w;
        }

        // Open-window snapshot of the unified role tips: handles are built once
        // per (role, context[, pawn]) per stamp, then activated at visible use sites.
        private readonly Dictionary<(int roleId, RoleTipContext context, Pawn pawn), StructuredTip> roleTipCache
            = new Dictionary<(int, RoleTipContext, Pawn), StructuredTip>();
        private ScopeCacheStamp roleTipStamp = ScopeCacheStamp.Invalid;

        /// The one role tooltip: palette chips, tree rows and assignment chips
        /// share the content; context varies the actions and pawn facts.
        internal string RoleTipText(Role role, RoleTipContext context, Pawn pawn = null)
        {
            var store = RoleStore.Current;
            if (store == null) return role.label;
            ScopeCacheStamp stamp = PawnListStamp;
            if (roleTipStamp != stamp)
                roleTipCache.Clear();
            var key = (role.id, context, pawn);
            if (!roleTipCache.TryGetValue(key, out StructuredTip tip))
            {
                int pawnId = pawn?.thingIDNumber ?? -1;
                roleTipCache[key] = tip = new StructuredTip(
                    $"role:{role.id}:{context}:{pawnId}",
                    BuildRoleTip(store, role, context, pawn));
            }
            roleTipStamp = PawnListStamp;
            return tip.Activate();
        }

        private TipModel BuildRoleTip(RoleStore store, Role role, RoleTipContext context, Pawn pawn)
        {
            var model = new TipModel
            {
                Title = role.label.Colorize(WrStyle.MinorAccent),
            };

            string stateText = (role.enabled ? "WR_RoleTipEnabled" : "WR_RoleTipDisabled")
                .Translate().ToString()
                .Colorize(role.enabled ? RoleStateEnabled : RoleStateDisabled);
            var markers = new List<string>();
            if (role.blocker) markers.Add("WR_BadgeBlocker".Translate());
            if (role.activeHours != Role.AllHours) markers.Add("WR_BadgeHours".Translate());
            if (role.locationTokens.Count > 0) markers.Add("WR_BadgeLocation".Translate());
            model.Badge = markers.Count > 0
                ? stateText + TipText.Dim(" · " + string.Join(" · ", markers))
                : stateText;

            var def = role.templateDefName == null ? null
                : DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
            if (!def?.description.NullOrEmpty() ?? false)
                model.AddSection().Text(def.description);

            var facts = model.AddSection();
            var skills = RecsAdapter.RelevantSkillsOf(role);
            if (skills.Count > 0)
                facts.Fact("WR_TipSkillsLabel".Translate(),
                    skills.Select(s => s.skillLabel.CapitalizeFirst()).ToCommaList());
            facts.Fact("WR_TipJobsLabel".Translate(), JobSummary(role));
            foreach (var path in store.trainingPaths)
            {
                int idx = path.roleIds.IndexOf(role.id);
                if (idx < 0) continue;
                int lo = path.bandMins[idx], hi = path.bandMaxes[idx];
                string band = hi >= SkillProgressionMath.MaxLevel ? lo + "+" : lo + "-" + hi;
                var target = store.RoleById(
                    TrainingPathPresentation.HighestBandRoleId(path));
                string recommend = "WR_TipTrainingRecommend".Translate(band);
                if (target?.id != role.id)
                    recommend += " " + "WR_TipTrainingPath".Translate(
                        (target?.label ?? path.name).Colorize(WrStyle.MinorAccent));
                facts.Fact("WR_TipTrainingHeader".Translate(), recommend);
            }

            string fits = BestFits(skills);
            if (!fits.NullOrEmpty())
                model.AddSection("WR_TipBestFitsLabel".Translate()).Text(fits);

            if (context == RoleTipContext.AssignmentChip && pawn != null)
            {
                store.pawnSets.TryGetValue(pawn, out var set);
                var assignment = set?.assignments.FirstOrDefault(a => a.roleId == role.id);
                TipSection state = null;
                // Same stamp and shared invalidation as the tip cache, so the
                // embedded capability sentence can never outlive its inputs.
                RoleCapabilityPresentation capability =
                    roleCapabilityState.PresentationFor(
                        pawn, role, PawnListStamp, ExternalSnapshotFor(pawn));
                if (capability.Tooltip != null)
                    (state = model.AddSection()).Text(TipText.Warning(capability.Tooltip));
                if (role.enabled && assignment?.enabled == true && !RulesPass(role, pawn))
                {
                    string reason = SuppressionReason(role, pawn);
                    if (!reason.NullOrEmpty())
                        (state ?? (state = model.AddSection())).Text(TipText.Warning(reason));
                }
                if (assignment?.pinned == true)
                    (state ?? model.AddSection()).Text("WR_PinnedTip".Translate(), dim: true);
            }

            var actions = model.AddSection();
            switch (context)
            {
                case RoleTipContext.Palette:
                    actions.Action("WR_ActClick".Translate(), "WR_ActPaletteClick".Translate())
                        .Action("WR_ActShiftClick".Translate(), "WR_ActPaletteShiftClick".Translate())
                        .Action("WR_ActDrag".Translate(), "WR_ActPaletteDrag".Translate());
                    break;
                case RoleTipContext.TreeRow:
                    actions.Action("WR_ActClick".Translate(), "WR_ActTreeClick".Translate())
                        .Action("WR_ActDrag".Translate(), "WR_ActTreeDrag".Translate())
                        .Action("WR_ActRightClick".Translate(), "WR_ActTreeRightClick".Translate());
                    break;
                case RoleTipContext.AssignmentChip:
                    actions.Action("WR_ActClick".Translate(), "WR_ActChipClick".Translate())
                        .Action("WR_ActRightClick".Translate(), "WR_ActChipRightClick".Translate())
                        .Action("WR_ActDrag".Translate(), "WR_ActChipDrag".Translate())
                        .Action("WR_ActX".Translate(), "WR_ActChipX".Translate());
                    break;
            }
            return model;
        }

        // Enabled/disabled badge tint; matches the verdict green/red family.
        private static readonly Color RoleStateEnabled = new Color(0.55f, 0.8f, 0.45f);
        private static readonly Color RoleStateDisabled = new Color(0.9f, 0.35f, 0.3f);

        /// Whole work types as "X (all jobs)", single jobs by display name;
        /// one line: capped so mega-roles don't flood the tooltip.
        private static string JobSummary(Role role)
        {
            const int Cap = 3;
            var parts = new List<string>();
            foreach (var entry in role.entries)
            {
                if (parts.Count == Cap)
                {
                    parts.Add("WR_TipMore".Translate(role.entries.Count - Cap).ToString());
                    break;
                }
                if (entry.Kind == JobEntryKind.WorkType)
                {
                    var workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.DefName);
                    parts.Add("WR_TipWholeType".Translate(
                        (workType?.gerundLabel ?? entry.DefName).CapitalizeFirst()).ToString());
                }
                else
                {
                    var giver = DefDatabase<WorkGiverDef>.GetNamedSilentFail(entry.DefName);
                    parts.Add(giver != null
                        ? WorkJobLabels.GiverDisplayName(giver)
                        : entry.DefName);
                }
            }
            return parts.ToCommaList();
        }

        /// Colonists best suited to the role's skills according to the same
        /// aggregated bucket verdicts consumed by recommendations. Verdict ties
        /// are broken by skill level. Capped at six.
        private string BestFits(List<SkillDef> skills)
        {
            if (skills.Count == 0) return null;
            var ranked = new List<(string label, SignalBucket bucket, int level)>();
            foreach (var pawn in ListedPawns())
            {
                var candidates = new List<SkillBucketCandidate>(skills.Count);
                foreach (var skill in skills)
                {
                    SkillLine line = statsState.SkillLineSnapshot(pawn, skill);
                    if (line.Disabled) continue;
                    candidates.Add(new SkillBucketCandidate(skill.defName, line.Level));
                }
                SkillBucketChoice best = SkillBucketRanking.Best(
                    SignalSnapshotFor(pawn).SkillBuckets, candidates);
                if (best == null || best.Bucket < SignalBucket.Strong) continue;
                string tag = SkillSignalPresentation.BucketLabel(best.Bucket);
                Color tierColor = SkillSignalPresentation.VerdictColor(best.Bucket);
                ranked.Add(($"{pawn.LabelShortCap} ({tag})".Colorize(tierColor),
                    best.Bucket, best.SkillLevel));
            }
            if (ranked.Count == 0) return null;
            var top = ranked
                .OrderByDescending(t => t.bucket)
                .ThenByDescending(t => t.level)
                .Take(6)
                .Select(t => t.label)
                .ToList();
            if (ranked.Count > 6)
                top.Add("WR_TipMore".Translate(ranked.Count - 6).ToString());
            return top.ToCommaList();
        }

        // ----- Colonist table -----

        /// The colonist table: a fixed header row above one scroll view of
        /// group sections and variable-height pawn rows.
        private void DrawPawnTable(Rect rect, RoleStore store)
        {
            IReadOnlyList<GroupSection<Pawn>> sections = rosterState.Sections(store);

            // Chip strips wrap against the roles column; everything else is
            // fixed-width, so the row-height estimate is exact.
            EstimatedStripWidth = TableStripWidth(rect.width);

            bool grouped = rosterState.Grouped;
            var outRect = new Rect(rect.x, rect.y + TableHeaderH, rect.width, rect.height - TableHeaderH);
            lastTableViewH = outRect.height;
            float viewW = outRect.width - 16f;
            EnsureTableLayout(sections, grouped, EstimatedStripWidth);

            if (tableListedCount == 0 && rosterState.FiltersActive)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = WrStyle.DimText;
                Widgets.Label(rect, "WR_NoFilterMatches".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            DrawTableHeader(new Rect(rect.x, rect.y, rect.width - 16f, TableHeaderH), store);

            float totalH = tableRowLayout?.ContentExtent ?? 0f;
            Widgets.BeginScrollView(outRect, ref tableScroll,
                new Rect(0f, 0f, viewW, totalH));
            VariableViewportRange visible = tableRowLayout.Calculate(
                tableScroll.y, outRect.height);
            for (int i = visible.Start; i < visible.EndExclusive; i++)
            {
                TableLayoutRow row = tableLayoutRows[i];
                float y = tableRowLayout.OffsetOf(i);
                float height = tableRowLayout.ExtentOf(i);
                var rowRect = new Rect(0f, y, viewW, height);
                if (row.Pawn == null) DrawGroupHeader(rowRect, row.Section);
                else DrawRow(rowRect, row.Pawn, store);
            }
            Widgets.EndScrollView();
        }

        private void EnsureTableLayout(
            IReadOnlyList<GroupSection<Pawn>> sections,
            bool grouped,
            float stripWidth)
        {
            ScopeCacheStamp stamp = PawnListStamp;
            int display = (int)TableChips;
            if (tableRowLayout != null
                && ReferenceEquals(tableLayoutSections, sections)
                && tableLayoutStamp == stamp
                && tableLayoutStripWidth == stripWidth
                && tableLayoutDisplay == display)
                return;

            tableLayoutSections = sections;
            tableLayoutStamp = stamp;
            tableLayoutStripWidth = stripWidth;
            tableLayoutDisplay = display;
            tableListedCount = 0;
            tableLayoutRows.Clear();

            var heights = new List<float>();
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                GroupSection<Pawn> section = sections[sectionIndex];
                tableListedCount += section.Members.Count;
                if (grouped)
                {
                    tableLayoutRows.Add(new TableLayoutRow(section, null));
                    heights.Add(GroupHeaderH);
                    if (rosterState.IsCollapsed(section.Key)) continue;
                }
                for (int pawnIndex = 0; pawnIndex < section.Members.Count; pawnIndex++)
                {
                    Pawn pawn = section.Members[pawnIndex];
                    tableLayoutRows.Add(new TableLayoutRow(null, pawn));
                    heights.Add(RowHeightOf(pawn));
                }
            }
            tableRowLayout = new VariableViewportLayout(heights);
        }

        /// Fixed header: Colonist (suffix names the default order; clicking
        /// clears a skill sort, or toggles Tab order/A-Z when none is active)
        // Cached header label ("Colonist" + order suffix): Translate + Colorize
        // concat is an allocation per pass otherwise.
        private string colonistHeaderCache;
        private ColonistOrder colonistHeaderOrder;

        /// and skill columns (click sorts by that skill, highest first — the
        /// sorting column's label renders in the passion yellow; X removes).
        private void DrawTableHeader(Rect rect, RoleStore store)
        {
            Text.Font = GameFont.Small;
            SkillDef sortSkill = rosterState.SortSkill;

            // Priority grid over every listed colonist (the filtered table set).
            var gridRect = new Rect(rect.xMax - 26f, rect.y + (rect.height - 18f) / 2f, 18f, 18f);
            TooltipHandler.TipRegion(gridRect, "WR_ShowPriorityGridTip".Translate());
            if (Widgets.ButtonImage(gridRect, TexButton.Info))
            {
                var listed = new List<Pawn>();
                foreach (GroupSection<Pawn> section in rosterState.Sections(store))
                    listed.AddRange(section.Members);
                Find.WindowStack.Add(new Dialog_PriorityGrid(listed));
                return;
            }

            var nameRect = new Rect(rect.x, rect.y, 264f, rect.height);
            var order = profile.GetColonistOrder();
            if (colonistHeaderCache == null || colonistHeaderOrder != order)
            {
                colonistHeaderOrder = order;
                string orderSuffix = order == ColonistOrder.Alphabetical
                    ? "WR_OrderSuffixAZ".Translate() : "WR_OrderSuffixBar".Translate();
                colonistHeaderCache = "WR_ColColonist".Translate() + " "
                    + orderSuffix.Colorize(new Color(1f, 1f, 1f, 0.45f));
            }
            Text.Anchor = TextAnchor.LowerLeft;
            // A-Z is an explicit colonist sort: mark the header like a sorting
            // skill column (bar order is the neutral default and stays white).
            if (sortSkill == null && order == ColonistOrder.Alphabetical)
                GUI.color = WrStyle.MinorAccent;
            Widgets.Label(new Rect(nameRect.x + 4f, nameRect.y, nameRect.width - 8f, nameRect.height - 2f),
                colonistHeaderCache);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawHighlightIfMouseover(nameRect);
            if (Widgets.ButtonInvisible(nameRect))
            {
                if (sortSkill != null) rosterState.SetSort("");
                else profile.SetColonistOrder(order == ColonistOrder.Alphabetical
                    ? ColonistOrder.ColonistBar : ColonistOrder.Alphabetical);
            }

            float x = rect.x + 264f;
            IReadOnlyList<SkillDef> columns = rosterState.SkillColumns;
            for (int i = 0; i < columns.Count; i++)
            {
                SkillDef skill = columns[i];
                float w = SkillColumnWidth(skill);
                var headerRect = new Rect(x, rect.y, w, rect.height);
                var closeRect = new Rect(headerRect.xMax - 16f, headerRect.yMax - 20f, 14f, 14f);
                if (Widgets.ButtonImage(closeRect, TexButton.CloseXSmall))
                {
                    RemoveSkillColumn(i);
                    return;
                }
                bool wrap = Text.WordWrap;
                Text.WordWrap = false;
                Text.Anchor = TextAnchor.LowerLeft;
                if (sortSkill == skill) GUI.color = WrStyle.MinorAccent; // marks the sort column
                Widgets.Label(new Rect(headerRect.x + 2f, headerRect.y, headerRect.width - 24f, headerRect.height - 2f),
                    SkillHeaderLabel(skill));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = wrap;
                var clickRect = new Rect(headerRect.x, headerRect.y, headerRect.width - 18f, headerRect.height);
                Widgets.DrawHighlightIfMouseover(clickRect);
                if (Widgets.ButtonInvisible(clickRect)) rosterState.SetSort(skill.defName);
                x += w;
            }
        }

        private void DrawGroupHeader(Rect rect, GroupSection<Pawn> section)
        {
            Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.06f));
            bool collapsed = rosterState.IsCollapsed(section.Key);
            var arrowRect = new Rect(rect.x + 6f, rect.y + (rect.height - 18f) / 2f, 18f, 18f);
            GUI.DrawTexture(arrowRect, collapsed ? TexButton.Reveal : TexButton.Collapse);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(arrowRect.xMax + 6f, rect.y, rect.width - arrowRect.xMax - 10f, rect.height),
                rosterState.SectionTitle(section.Key));
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawHighlightIfMouseover(rect);
            if (Widgets.ButtonInvisible(rect))
            {
                rosterState.ToggleCollapsed(section.Key);
                // This handler runs while the current visible-row snapshot is
                // being iterated. Mark it stale; the next IMGUI pass rebuilds.
                tableLayoutStamp = ScopeCacheStamp.Invalid;
            }
        }

        private void DrawRow(Rect rect, Pawn pawn, RoleStore store)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            WrText.LineHorizontal(rect.x, rect.y, rect.width);
            GUI.color = Color.white;

            if (pawn == selectedPawn)
                Widgets.DrawHighlightSelected(rect);
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                new LookTargets(pawn).Highlight(arrow: true, colonistBar: pawn.IsColonist);
            }

            float x = rect.x;
            DrawColonistCell(new Rect(x, rect.y, 264f, rect.height), pawn);
            x += 264f;
            foreach (SkillDef skill in rosterState.SkillColumns)
            {
                float w = SkillColumnWidth(skill);
                DrawSkillCell(new Rect(x, rect.y, w, rect.height), pawn, skill);
                x += w;
            }
            float rolesW = rect.xMax - 28f - x;
            DrawChipStrip(new Rect(x, rect.y, rolesW, rect.height), pawn, store, rolesW);
            x += rolesW;
            var plusRect = new Rect(x + 2f, rect.y + (rect.height - IconButton) / 2f, IconButton, IconButton);
            TooltipHandler.TipRegion(plusRect, "WR_AddRoleTip".Translate());
            if (Widgets.ButtonImage(plusRect, TexButton.Plus))
                OpenAddMenu(pawn, store);

            if (pawn.Downed)
            {
                GUI.color = new Color(1f, 0f, 0f, 0.5f);
                WrText.LineHorizontal(rect.x, rect.center.y, rect.width);
                GUI.color = Color.white;
            }
        }

        // ----- Keyboard navigation -----

        /// Keyboard input for the colonist table, on KeyDown while no text field
        /// owns the keyboard. Returns true when the event should be consumed.
        internal bool HandleKey(Event ev)
        {
            var store = RoleStore.Current;
            if (store == null) return false;

            if (WR_KeyBindingDefOf.WR_PrevColonist.KeyDownEvent) return MoveSelection(-1);
            if (WR_KeyBindingDefOf.WR_NextColonist.KeyDownEvent) return MoveSelection(+1);
            if (WR_KeyBindingDefOf.WR_FirstColonist.KeyDownEvent) return SelectEdge(first: true, ignoreGroups: ev.control);
            if (WR_KeyBindingDefOf.WR_LastColonist.KeyDownEvent) return SelectEdge(first: false, ignoreGroups: ev.control);
            if (WR_KeyBindingDefOf.WR_PrevPage.KeyDownEvent) return PageMove(-1);
            if (WR_KeyBindingDefOf.WR_NextPage.KeyDownEvent) return PageMove(+1);

            if (ev.control && ev.keyCode == KeyCode.C && selectedPawn != null)
            {
                store.pawnSets.TryGetValue(selectedPawn, out var toCopy);
                RoleClipboard.CopyFrom(store, toCopy);
                WrToast.Show("WR_CopiedRoles".Translate(selectedPawn.LabelShortCap),
                    MessageTypeDefOf.NeutralEvent);
                return true;
            }
            if (ev.control && ev.keyCode == KeyCode.V && selectedPawn != null && RoleClipboard.HasContent)
            {
                RoleCommands.PasteRoleSet(selectedPawn, RoleClipboard.Content);
                return true;
            }

            // Ctrl+F puts the caret in the search box.
            if (ev.control && ev.keyCode == KeyCode.F)
            {
                focusSearch = true;
                return true;
            }
            return false;
        }

        /// The sections keyboard navigation moves through: collapsed groups
        /// are skipped (their pawns aren't visible).
        private List<GroupSection<Pawn>> NavSections()
        {
            IReadOnlyList<GroupSection<Pawn>> sections = rosterState.Sections(RoleStore.Current);
            return rosterState.Grouped
                ? sections.Where(section => !rosterState.IsCollapsed(section.Key)).ToList()
                : sections.ToList();
        }

        private bool MoveSelection(int delta)
        {
            var order = NavSections().SelectMany(s => s.Members).ToList();
            if (order.Count == 0) return true;
            int idx = selectedPawn != null ? order.IndexOf(selectedPawn) : -1;
            int target = idx < 0
                ? (delta > 0 ? 0 : order.Count - 1)
                : Mathf.Clamp(idx + delta, 0, order.Count - 1);
            Select(order[target]);
            return true;
        }

        /// Home/End: first/last within the selected pawn's group when grouped;
        /// Ctrl (or no grouping) spans the whole list.
        private bool SelectEdge(bool first, bool ignoreGroups)
        {
            var sections = NavSections();
            var order = sections.SelectMany(s => s.Members).ToList();
            if (order.Count == 0) return true;
            var pool = order;
            if (!ignoreGroups && rosterState.Grouped)
                pool = sections.FirstOrDefault(s => s.Members.Contains(selectedPawn))?.Members ?? order;
            Select(first ? pool[0] : pool[pool.Count - 1]);
            return true;
        }

        /// PgUp/PgDn: the adjacent group when grouped, else one screenful
        /// measured with the renderer's row heights.
        private bool PageMove(int dir)
        {
            var sections = NavSections();
            var order = sections.SelectMany(s => s.Members).ToList();
            if (order.Count == 0) return true;
            if (rosterState.Grouped && sections.Count > 1)
            {
                int gi = sections.FindIndex(s => s.Members.Contains(selectedPawn));
                if (gi < 0) gi = dir > 0 ? -1 : sections.Count;
                gi = Mathf.Clamp(gi + dir, 0, sections.Count - 1);
                Select(sections[gi].Members[0]);
                return true;
            }
            int idx = Mathf.Max(0, order.IndexOf(selectedPawn));
            float view = Mathf.Max(100f, lastTableViewH);
            int target = idx;
            float used = 0f;
            while (target + dir >= 0 && target + dir < order.Count && used < view)
            {
                target += dir;
                used += RowHeightOf(order[target]);
            }
            Select(order[target]);
            return true;
        }

        private float RowHeightOf(Pawn pawn) =>
            Mathf.Max(RowHeight, Mathf.CeilToInt(StripHeightFor(pawn) + 8f));

        private void Select(Pawn pawn)
        {
            selectedPawn = pawn;
            EnsureSelectedVisible();
        }

        /// Mouse wheel over the window that no inner scroll view consumed
        /// scrolls the colonist table (instead of zooming the map). The scroll
        /// view clamps to its content on the next draw, so only the lower
        /// bound needs guarding here.
        internal void ScrollTable(float wheelDelta) =>
            tableScroll.y = Mathf.Max(0f, tableScroll.y + wheelDelta * 20f);

        /// Scrolls the selection into view; y offsets mirror the renderer
        /// (collapsed sections contribute their header only).
        private void EnsureSelectedVisible()
        {
            IReadOnlyList<GroupSection<Pawn>> sections =
                rosterState.Sections(RoleStore.Current);
            bool grouped = rosterState.Grouped;
            float y = 0f, top = -1f, bottom = -1f;
            foreach (var section in sections)
            {
                if (grouped)
                {
                    y += GroupHeaderH;
                    if (rosterState.IsCollapsed(section.Key)) continue;
                }
                foreach (var pawn in section.Members)
                {
                    float rowH = RowHeightOf(pawn);
                    if (pawn == selectedPawn) { top = y; bottom = y + rowH; }
                    y += rowH;
                }
            }
            if (top < 0f) return;
            if (top < tableScroll.y) tableScroll.y = top;
            else if (bottom > tableScroll.y + lastTableViewH) tableScroll.y = bottom - lastTableViewH;
        }

        internal void RemoveSkillColumn(int index)
            => rosterState.RemoveSkillColumn(index);

        internal void InvalidatePawnSnapshot() => rosterState.InvalidatePawnSnapshot();

        /// <summary>The colonist list under the active scope (no baby pawns).</summary>
        internal IReadOnlyList<Pawn> ListedPawns() => rosterState.ListedPawns();

        /// True when the listed pawns come from more than one map/caravan —
        /// colony planning wants a single location (Fix My Colony disables).
        internal bool ScopeSpansMultipleLocations
            => rosterState.SpansMultipleLocations;

        /// The portrait/name/copy/paste cell (the table's label column); clicking
        /// the portrait/name area selects the pawn for the stats panel.
        internal void DrawColonistCell(Rect rect, Pawn pawn)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            var portraitRect = new Rect(rect.x, rect.y + (rect.height - PortraitSize) / 2f, PortraitSize, PortraitSize);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, new Vector2(PortraitSize, PortraitSize), Rot4.South));

            var nameRect = new Rect(portraitRect.xMax + 6f, rect.y, NameWidth, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            // Slaves get the game's own sandy-yellow name color, as in vanilla lists.
            GUI.color = pawn.IsSlave ? PawnNameColorUtility.PawnNameColorOf(pawn) : Color.white;
            Widgets.Label(nameRect, pawn.LabelShortCap);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            var selectRect = new Rect(rect.x, rect.y, portraitRect.width + 6f + NameWidth, rect.height);
            if (Mouse.IsOver(selectRect))
                TooltipHandler.TipRegion(selectRect, pawn.GetTooltip());
            if (Widgets.ButtonInvisible(selectRect))
                selectedPawn = pawn;

            var copyRect = new Rect(nameRect.xMax + 2f, rect.y + (rect.height - IconButton) / 2f, IconButton, IconButton);
            var pasteRect = new Rect(copyRect.xMax + 2f, copyRect.y, IconButton, IconButton);
            TooltipHandler.TipRegion(copyRect, "WR_CopyRolesTip".Translate());
            if (Widgets.ButtonImage(copyRect, TexButton.Copy))
            {
                store.pawnSets.TryGetValue(pawn, out var toCopy);
                RoleClipboard.CopyFrom(store, toCopy);
                WrToast.Show("WR_CopiedRoles".Translate(pawn.LabelShortCap), MessageTypeDefOf.NeutralEvent);
            }
            Color pasteColor = RoleClipboard.HasContent ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            TooltipHandler.TipRegion(pasteRect, "WR_PasteRolesTip".Translate());
            if (Widgets.ButtonImage(pasteRect, TexButton.Paste, pasteColor) && RoleClipboard.HasContent)
                RoleCommands.PasteRoleSet(pawn, RoleClipboard.Content);
        }

        /// Chip-strip height for a pawn against the estimated chip-column width —
        /// the table row height comes from this.
        private readonly struct RoleChipLayout
        {
            internal RoleChipLayout(RoleAssignment assignment, Rect rect, int line,
                RoleCapabilityPresentation capability)
            {
                Assignment = assignment;
                Rect = rect;
                Line = line;
                Capability = capability;
            }

            internal RoleAssignment Assignment { get; }
            internal Rect Rect { get; }
            internal int Line { get; }
            internal RoleCapabilityPresentation Capability { get; }
        }

        // Open-window snapshot of per-pawn chip layouts at the table's strip
        // width: the table body (heights + chip rects) becomes dictionary reads.
        // Floored like EstimatedStripWidth so draw and measure share one key.
        private readonly Dictionary<Pawn, (List<RoleChipLayout> layout, float height)>
            chipLayouts = new Dictionary<Pawn, (List<RoleChipLayout>, float)>();
        private ScopeCacheStamp chipLayoutStamp = ScopeCacheStamp.Invalid;
        private float chipLayoutWidth = -1f;
        private int chipLayoutDisplay = -1;

        private (List<RoleChipLayout> layout, float height)
            ChipLayoutFor(Pawn pawn, RoleStore store, float stripWidth)
        {
            stripWidth = Mathf.Max(300f, stripWidth);
            ScopeCacheStamp stamp = PawnListStamp;
            if (chipLayoutStamp != stamp || chipLayoutWidth != stripWidth
                || chipLayoutDisplay != (int)TableChips)
            {
                chipLayouts.Clear();
                chipLayoutStamp = stamp;
                chipLayoutWidth = stripWidth;
                chipLayoutDisplay = (int)TableChips;
            }
            if (chipLayouts.TryGetValue(pawn, out var cached)) return cached;
            store.pawnSets.TryGetValue(pawn, out var set);
            var layout = new List<RoleChipLayout>();
            float height = set == null || set.assignments.Count == 0
                ? RoleChipUI.Height
                : LayoutChips(chipLayoutWidth, set.assignments, store, pawn, layout);
            var entry = (layout, height);
            chipLayouts[pawn] = entry;
            return entry;
        }

        internal float StripHeightFor(Pawn pawn)
        {
            var store = RoleStore.Current;
            if (store == null) return RoleChipUI.Height;
            return ChipLayoutFor(pawn, store, EstimatedStripWidth).height;
        }

        /// One skill cell: the same fractional level, signal-derived colour,
        /// decorators and combined structured tooltip as the bottom stats panel.
        internal void DrawSkillCell(Rect cell, Pawn pawn, SkillDef skill)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            SkillLine line = statsState.SkillLineSnapshot(pawn, skill);
            if (line.Disabled)
            {
                GUI.color = WrStyle.DisabledText;
                Widgets.Label(new Rect(cell.x + 2f, cell.y, 44f, cell.height), "-");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            ColonistSkillPresentation presentation = statsState.PresentationFor(
                pawn, line);
            Color textColor = ColonistStatsState.SkillTextColor(
                presentation.Line, presentation.SignalView.PassionTier);
            GUI.color = textColor;
            // Labels paint on Repaint only; layout and input passes only need
            // the cached presentation and hover region.
            if (Event.current.type == EventType.Repaint)
                Widgets.Label(new Rect(cell.x + 2f, cell.y, 44f, cell.height),
                    presentation.Line.ValueText);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            float ix = cell.x + 48f;
            foreach (Texture2D texture in presentation.SignalIcons)
            {
                GUI.DrawTexture(new Rect(ix, cell.y + (cell.height - 16f) / 2f,
                    16f, 16f), texture);
                ix += 18f;
            }

            if (presentation.Tooltip != null && Mouse.IsOver(cell))
                TooltipHandler.TipRegion(cell, presentation.Tooltip.Activate());
        }

        // Open-window snapshot of rule outcomes: Pass hits the map (gravship
        // lookup) per chip otherwise. Hour flips and map moves bump the stamp.
        // Keyed by role+pawn within this scope-owning view.
        private readonly Dictionary<(int roleId, Pawn pawn), bool> rulesPassCache
            = new Dictionary<(int, Pawn), bool>();
        private ScopeCacheStamp rulesPassStamp = ScopeCacheStamp.Invalid;

        private bool RulesPass(Role role, Pawn pawn)
        {
            if (!role.HasRules) return true;
            ScopeCacheStamp stamp = PawnListStamp;
            if (rulesPassStamp != stamp)
            {
                rulesPassCache.Clear();
                rulesPassStamp = stamp;
            }
            var key = (role.id, pawn);
            if (!rulesPassCache.TryGetValue(key, out bool pass))
                rulesPassCache[key] = pass = RoleRules.Pass(role, pawn);
            return pass;
        }

        internal void DrawChipStrip(Rect stripRect, Pawn pawn, RoleStore store, float stripWidth)
        {
            var (layout, stripContentHeight) = ChipLayoutFor(pawn, store, stripWidth);

            float yOffset = stripRect.y + (stripRect.height - stripContentHeight) / 2f;

            for (int chipIndex = 0; chipIndex < layout.Count; chipIndex++)
            {
                RoleChipLayout chip = layout[chipIndex];
                RoleAssignment assignment = chip.Assignment;
                Rect localRect = chip.Rect;
                RoleCapabilityPresentation capability = chip.Capability;
                var role = store.RoleById(assignment.roleId);
                if (role == null) continue;
                var chipRect = new Rect(stripRect.x + localRect.x, yOffset + localRect.y, localRect.width, localRect.height);

                bool chipEnabled = role.enabled && assignment.enabled;
                // Rules only matter once both toggles are on; suppression is absolute,
                // so a suppressed chip takes no body click (remove/drag still work).
                bool suppressed = chipEnabled && !RulesPass(role, pawn);
                ChipStyle style = !chipEnabled ? ChipStyle.Disabled
                    : suppressed ? ChipStyle.AutoOff
                    : ChipStyle.Normal;
                // The toggle closure allocates: create it only on the one pass
                // that can consume it (left mouse-down inside this chip).
                System.Action onClick = null;
                var pressEvent = Event.current;
                if (!suppressed && pressEvent.type == EventType.MouseDown && pressEvent.button == 0
                    && chipRect.Contains(pressEvent.mousePosition))
                {
                    Pawn capturedPawn = pawn;
                    Role capturedRole = role;
                    onClick = () =>
                    {
                        // Enabling a globally-disabled role via ToggleRoleForPawn
                        // re-enables it globally and turns it off for every other
                        // holder — too big a blast radius for a silent chip click.
                        if (!capturedRole.enabled)
                            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                            {
                                new FloatMenuOption(
                                    "WR_EnableHereOnly".Translate(capturedRole.label, capturedPawn.LabelShortCap),
                                    () => RoleCommands.ToggleRoleForPawn(capturedPawn, capturedRole.id)),
                                new FloatMenuOption(
                                    "WR_EnableGlobally".Translate(capturedRole.label),
                                    () => RoleCommands.ToggleRoleGlobal(capturedRole.id)),
                            }));
                        else
                            RoleCommands.ToggleRoleForPawn(capturedPawn, capturedRole.id);
                    };
                }
                // The chip's one tooltip: marker meanings are folded into it.
                if (Mouse.IsOver(chipRect))
                    TooltipHandler.TipRegion(chipRect,
                        RoleTipText(role, RoleTipContext.AssignmentChip, pawn));
                var click = RoleChipUI.Draw(chipRect, role, style,
                    showRemove: true, dragSource: pawn,
                    onClick: onClick,
                    display: TableChips, abbrev: AbbrevIfCompact(store, role),
                    pinned: assignment.pinned,
                    warningSeverity: capability.WarningSeverity);
                if (click == ChipClick.Remove) RoleCommands.RemoveRoleFromPawn(pawn, role.id);
                if (click == ChipClick.Context)
                {
                    Pawn menuPawn = pawn;
                    int menuRoleId = role.id;
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        new FloatMenuOption(
                            assignment.pinned ? "WR_UnpinAssignment".Translate() : "WR_PinAssignment".Translate(),
                            () => RoleCommands.ToggleAssignmentPin(menuPawn, menuRoleId))
                    }));
                }
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
                    int insertIndex = RoleDrag.ChipInsertIndex(
                        new Vector2(mouse.x - stripRect.x, mouse.y - yOffset),
                        layout, t => t.Rect);

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
                        Rect prevR = layout[prevIdx].Rect;
                        markerX = stripRect.x + prevR.xMax - ChipGap / 2f;
                        markerY = yOffset + prevR.y + 3f;
                        markerH = prevR.height - 6f;
                    }
                    Widgets.DrawBoxSolid(new Rect(markerX - 1f, markerY, 2f, markerH), new Color(1f, 1f, 1f, 0.9f));
                }
            }
        }

        private static string SuppressionReason(Role role, Pawn pawn)
        {
            switch (RoleRules.FailReason(role, pawn))
            {
                case RuleFailReason.OutsideHours: return "WR_SuppressedHours".Translate();
                case RuleFailReason.WrongLocation: return "WR_SuppressedLocation".Translate();
                default: return "";
            }
        }

        // ----- Stats panel -----

        private void DrawStatsPanel(Rect rect, RoleStore store)
        {
            Widgets.DrawBoxSolidWithOutline(
                rect, WrStyle.PanelBackground, WrStyle.PanelOutline);
            rect = rect.ContractedBy(StatsPadding);
            if (selectedPawn == null) return;

            // Left section: portrait framed + name below
            float portraitBoxSize = PortraitDisplaySize;
            var portraitFrameRect = new Rect(rect.x, rect.y, portraitBoxSize, portraitBoxSize);

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

            ColonistStatsSnapshot statsSnapshot = statsState.Snapshot(selectedPawn);
            float skillColWidth = statsSnapshot.SkillColumnWidth;

            // Two equally sized skill columns after the portrait, separators
            // between them and before Recommended Roles.
            float col1X = rect.x + portraitBoxSize + 16f;
            float col2X = col1X + skillColWidth + ColSepMargin + ColSepWidth + ColSepMargin;

            float sep12X = col1X + skillColWidth + ColSepMargin;
            if (sep12X + ColSepWidth <= rect.xMax)
            {
                Widgets.DrawBoxSolid(new Rect(sep12X, rect.y, ColSepWidth, rect.height),
                    new Color(1f, 1f, 1f, 0.4f));
            }

            float sep23X = col2X + skillColWidth + ColSepMargin;
            float recX = sep23X + ColSepWidth + ColSepMargin;
            if (sep23X + ColSepWidth <= rect.xMax)
            {
                Widgets.DrawBoxSolid(new Rect(sep23X, rect.y, ColSepWidth, rect.height),
                    new Color(1f, 1f, 1f, 0.4f));
            }

            IReadOnlyList<ColonistSkillPresentation> skills = statsSnapshot.Skills;
            if (skills.Count == 0) return;

            Text.Font = GameFont.Small;
            for (int i = 0; i < skills.Count; i++)
            {
                int col = i % SkillCols;
                int row = i / SkillCols;
                ColonistSkillPresentation presentation = skills[i];
                SkillLine line = presentation.Line;
                SkillSignalView signalView = presentation.SignalView;
                IReadOnlyList<Texture2D> signalIcons = presentation.SignalIcons;

                float cellX = (col == 0) ? col1X : col2X;
                float cellY = rect.y + row * CellH;

                if (col >= SkillCols) continue;

                Color textColor = ColonistStatsState.SkillTextColor(
                    line, signalView.PassionTier);

                float xCursor = cellX;

                // Skill label (wrap off: a long modded skill name must clip, not
                // wrap out of the single-line cell)
                GUI.color = textColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                string labelText = line.Label;
                float labelWidth = presentation.LabelWidth;
                float iconWidth = signalIcons.Count == 0 ? 0f
                    : SkillLabelDecoratorGap
                        + signalIcons.Count * SkillDecoratorSize
                        + (signalIcons.Count - 1) * SkillDecoratorGap;
                float labelMaxW = Mathf.Max(0f,
                    skillColWidth - iconWidth - SkillValueGap - SkillValueWidth);
                bool wrapWas = Text.WordWrap;
                Text.WordWrap = false;
                Widgets.Label(new Rect(xCursor, cellY, labelMaxW, CellH), labelText);
                Text.WordWrap = wrapWas;

                // Every signal with a resolved authored icon is rendered. Icons
                // deliberately have no individual tooltip; the cell owns one
                // combined structured tooltip for all skill and global signals.
                float iconX = xCursor + Mathf.Min(labelWidth, labelMaxW);
                if (signalIcons.Count > 0)
                {
                    iconX += SkillLabelDecoratorGap;
                    foreach (Texture2D texture in signalIcons)
                    {
                        GUI.color = Color.white;
                        GUI.DrawTexture(new Rect(iconX,
                            cellY + (CellH - SkillDecoratorSize) / 2f,
                            SkillDecoratorSize, SkillDecoratorSize), texture);
                        iconX += SkillDecoratorSize + SkillDecoratorGap;
                    }
                }

                float valueX = (col == 0)
                    ? (col1X + skillColWidth - SkillValueWidth)
                    : (col2X + skillColWidth - SkillValueWidth);
                GUI.color = textColor;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(valueX, cellY, SkillValueWidth, CellH), line.ValueText);

                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                var cellRect = new Rect(cellX, cellY, skillColWidth, CellH);
                StructuredTip signalTip = presentation.Tooltip;
                if (signalTip != null && Mouse.IsOver(cellRect))
                    TooltipHandler.TipRegion(cellRect, signalTip.Activate());
            }

            // Recommended Roles section: mirrors the Make It So outcome — kept roles
            // subtle, additions normal, removals struck — so the panel IS the preview
            // and the button applies directly.
            if (profile.ShowRecommendations && recX < rect.xMax)
            {
                float recW = rect.xMax - recX;
                ColonistRecommendationPreview preview = recommendationState.Preview(
                    store, selectedPawn, PawnListStamp, ExternalSnapshotFor);
                PawnFixPlan pawnPlan = preview.Plan;

                WrText.HeaderLabel(new Rect(recX, rect.y, recW, 28f), "WR_RecommendedRoles".Translate());

                // Chips wrapping below header; the bottom 28f is reserved for "Make It So".
                float chipBottom = rect.yMax - 28f;
                float chipY = rect.y + 28f;
                float chipX = recX;
                var chips = preview.Chips;
                for (int previewIndex = 0; previewIndex < chips.Count; previewIndex++)
                {
                    var (role, state, tip) = chips[previewIndex];
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
                            RoleCommands.RemoveRoleFromPawn(capturedPawn, capturedId);
                        if (state == Dialog_ChangesPreview.ChipState.Removed)
                        {
                            RoleChipUI.DrawRemovedOutline(chipRect);
                        }
                        if (Mouse.IsOver(chipRect))
                        {
                            StructuredTip structuredTip = preview.Line?.StructuredTipAt(previewIndex);
                            TooltipHandler.TipRegion(chipRect, structuredTip?.Activate() ?? tip
                                ?? (state == Dialog_ChangesPreview.ChipState.Removed
                                    ? "WR_WillBeRemoved".Translate()
                                    : "WR_AlreadyAssigned".Translate()));
                        }
                    }
                    else
                    {
                        // Closure only on the pass that can consume it (see the
                        // chip strip); captures declared inside the gate, or the
                        // display class would still allocate per iteration.
                        System.Action onClick = null;
                        var pressEvent = Event.current;
                        if (pressEvent.type == EventType.MouseDown && pressEvent.button == 0
                            && chipRect.Contains(pressEvent.mousePosition))
                        {
                            Pawn clickPawn = selectedPawn;
                            int clickId = role.id;
                            onClick = () => AssignAtRecommendedPosition(clickPawn, clickId);
                        }
                        RoleChipUI.Draw(chipRect, role, ChipStyle.Normal, showRemove: false,
                            dragSource: null, onClick: onClick);
                        if (tip != null && Mouse.IsOver(chipRect))
                        {
                            StructuredTip structuredTip = preview.Line?.StructuredTipAt(previewIndex);
                            TooltipHandler.TipRegion(chipRect,
                                structuredTip?.Activate() ?? tip);
                        }
                    }
                    chipX += chipW + ChipGap;
                }

                if (pawnPlan != null && pawnPlan.HasChanges)
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
            AssignAtRecommendedPosition(pawn, roleId, store,
                recommendationState.RecommendedRoles(store, pawn, selectedPawn,
                    PawnListStamp, ExternalSnapshotFor));
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

        /// Preview entries from the colony plan (all changed pawns, or just one).
        private List<Dialog_ChangesPreview.PawnPreview> BuildFixEntries(Pawn only)
        {
            RoleStore store = RoleStore.Current;
            return store == null
                ? new List<Dialog_ChangesPreview.PawnPreview>()
                : recommendationState.FixEntries(store, only, selectedPawn,
                    PawnListStamp, ExternalSnapshotFor);
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
            foreach (PawnFixPlan plan in recommendationState.Plans(selectedPawn,
                PawnListStamp, ExternalSnapshotFor))
            {
                if (only != null && plan.Pawn != only) continue;
                if (included != null && !included.Contains(plan.Pawn)) continue;
                if (!plan.HasChanges) continue;
                RoleCommands.PasteRoleSet(plan.Pawn, plan.Target);
            }
        }

        /// Opens the per-colonist change preview for Fix My Colony; applies to the
        /// preview's selected colonists on confirm. (Make It So needs no dialog:
        /// the Recommended Roles panel IS its preview.)
        public void ShowFixPreview()
            => Find.WindowStack.Add(new Dialog_ChangesPreview(
                () => "WR_FixMyColony".Translate(), BuildFixEntries(null),
                included => ApplyFix(null, included), () => RebuildFixEntries(null)));

        // ----- Helpers -----

        internal static void OpenAddMenu(Pawn pawn, RoleStore store)
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

    }
}
