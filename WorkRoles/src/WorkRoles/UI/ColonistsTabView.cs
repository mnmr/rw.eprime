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
        private readonly PawnSignalSnapshotCache pawnSignalSnapshots =
            new PawnSignalSnapshotCache();

        public ColonistsTabView(ColonistsViewProfile profile) => this.profile = profile;

        private Vector2 paletteScroll;
        private Pawn selectedPawn;

        // Our own table renderer: a fixed header row above a scroll view of
        // group sections and per-pawn rows (chip strips make row heights vary).
        private Vector2 tableScroll;
        private float lastTableViewH = 400f;
        private float EstimatedStripWidth = 300f;
        private const float TableHeaderH = 30f;
        private const float GroupHeaderH = 30f;

        // Pawn scope (session-local; defaults to the current location).
        private ScopeOption scope;

        // View-local table filters (never synced, never persisted).
        private const string SearchControlName = "WR_ColonistSearch";
        private bool focusSearch;
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

        // Text colours for skill level
        private static readonly Color ColorDisabled   = new Color(0.45f, 0.45f, 0.45f);
        private static readonly Color ColorLow        = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color ColorPassMajor  = new Color(1f, 0.65f, 0.2f);
        // Shared accent tint (also the role editor's tuning summary).
        internal static readonly Color ColorPassMinor  = new Color(0.95f, 0.9f, 0.55f);

        // Colony suggestion plan: the Recommended Roles panel, Make It So and Fix My
        // Colony all read this one plan, so they always agree. Computed lazily,
        // invalidated on any role/assignment change.
        private List<PawnFixPlan> planCache;
        private int planStamp = -1;
        private int planMapId = -1;

        public void Reset()
        {
            paletteScroll = Vector2.zero;
            tableScroll = Vector2.zero;
            selectedPawn = null;
            colonistFilter = "";
            roleFilterId = -1;
            ColonyGroupsDataSource.InvalidateSnapshot(); // fresh membership per window open
            pawnSignalSnapshots.Clear();
            skillPresentations.Clear();
            skillPresentationStamp = -1;
            InvalidateRecommendationCache();
            // Opening re-snapshots everything (stats would otherwise stay stale
            // across a reopen when nothing bumped the version in between).
            pawnsStamp = sizeStamp = paletteStamp = rulesPassStamp = statsStamp
                = chipLayoutStamp = sectionsStamp = roleTipStamp = -1;
        }

        /// Reset-time only: every plan input (roles, assignments, pins, training,
        /// recommendation order, pawn membership) bumps UiVersion when its command
        /// EXECUTES — click-site invalidation would rebuild from pre-command state
        /// in MP and never fire on other clients.
        public void InvalidateRecommendationCache() => planCache = null;

        /// The pawn's signals and derived skill buckets captured together on
        /// first request for this window open.
        internal PawnSignalSnapshot SignalSnapshotFor(Pawn pawn)
            => pawnSignalSnapshots.Get(pawn);

        /// Window close: drop pawn-keyed snapshots so a save unloaded while the
        /// window is closed cannot stay pinned through them.
        internal void ReleaseSnapshots()
        {
            pawnSignalSnapshots.Clear();
            skillPresentations.Clear();
            skillPresentationStamp = -1;
        }

        /// The colony plan is per location: anchored to the selected pawn's map
        /// (recommendations stay map-scoped even when the view spans locations).
        private List<PawnFixPlan> GetPlan()
        {
            var map = selectedPawn?.MapHeld ?? Find.CurrentMap;
            int mapId = map?.uniqueID ?? -1;
            if (planCache == null || planStamp != UiVersion.Current || planMapId != mapId)
            {
                planStamp = UiVersion.Current;
                planMapId = mapId;
                planCache = BuildColonyFixPlan(map);
            }
            return planCache;
        }

        private PawnFixPlan PlanFor(Pawn pawn) => GetPlan().FirstOrDefault(p => p.pawn == pawn);

        // Open-window snapshot of the selected pawn's recommendation preview —
        // PreviewEntry builds hash sets and chip lists otherwise per pass. The
        // plan-list reference is part of the key: GetPlan swaps in a new list
        // whenever the plan cache is invalidated.
        private int previewStamp = -1;
        private Pawn previewPawn;
        private List<PawnFixPlan> previewSource;
        private PawnFixPlan previewPlan;
        private List<(Role role, Dialog_ChangesPreview.ChipState state, string tip)> previewChips;
        private static readonly List<(Role, Dialog_ChangesPreview.ChipState, string)> NoChips
            = new List<(Role, Dialog_ChangesPreview.ChipState, string)>();

        private void EnsurePreview(RoleStore store, Pawn pawn)
        {
            var source = GetPlan();
            if (previewChips != null && previewStamp == UiVersion.Current
                && previewPawn == pawn && previewSource == source) return;
            previewStamp = UiVersion.Current;
            previewPawn = pawn;
            previewSource = source;
            previewPlan = source.FirstOrDefault(p => p.pawn == pawn);
            previewChips = previewPlan == null ? NoChips : PreviewEntry(store, previewPlan).lines[0].chips;
        }

        // ----- Window sizing helpers -----

        // Open-window snapshot of the selected pawn's stats-panel data.
        // Deliberately stale: skill XP drifts continuously with no change event
        // to hook, so a level-up shows on the next bump or window reopen.
        private int statsStamp = -1;
        private Pawn statsPawn;
        private List<SkillLine> statsLines;
        private float[] statsLabelWidths;
        private SkillSignalView[] statsSignalViews;
        private List<Texture2D>[] statsSignalIcons;
        private string[] statsSignalTips;
        private float statsSkillColWidth = SkillColWidth;

        private sealed class SkillPresentation
        {
            internal SkillLine Line;
            internal float LabelWidth;
            internal SkillSignalView SignalView;
            internal List<Texture2D> SignalIcons;
            internal string Tooltip;
        }

        // Shared open-window presentation snapshot for the bottom stats panel
        // and optional table skill columns. Signal reflection, icon lookup and
        // structured tooltip composition happen once per pawn and skill.
        private readonly Dictionary<(Pawn pawn, SkillDef skill), SkillPresentation>
            skillPresentations = new Dictionary<(Pawn, SkillDef), SkillPresentation>();
        private int skillPresentationStamp = -1;

        private SkillPresentation PresentationFor(Pawn pawn, SkillLine line)
        {
            if (skillPresentationStamp != UiVersion.Current)
            {
                skillPresentations.Clear();
                skillPresentationStamp = UiVersion.Current;
            }

            var key = (pawn, line.Def);
            if (skillPresentations.TryGetValue(key, out SkillPresentation cached))
                return cached;

            PawnSignalSnapshot pawnSnapshot = SignalSnapshotFor(pawn);
            SkillSignalView view = SignalPresentationPolicy.ForSkill(
                pawnSnapshot.Signals, line.Def?.defName);
            List<Texture2D> icons = SkillSignalPresentation.ResolveIcons(view);
            float labelWidth;
            using (new TextBlock(GameFont.Small))
                labelWidth = Text.CalcSize(line.Label).x;

            var result = new SkillPresentation
            {
                Line = line,
                LabelWidth = labelWidth,
                SignalView = view,
                SignalIcons = icons,
                Tooltip = SkillSignalPresentation.RegisterTooltip(
                    pawn,
                    line.Label,
                    line.ValueText,
                    SkillTextColor(line, view.PassionTier),
                    view,
                    pawnSnapshot.SkillBuckets.ForSkill(line.Def?.defName)?.Bucket),
            };
            skillPresentations.Add(key, result);
            return result;
        }

        private void EnsureStats(Pawn pawn)
        {
            if (statsStamp == UiVersion.Current && statsPawn == pawn) return;
            statsStamp = UiVersion.Current;
            statsPawn = pawn;
            statsLines = SkillsTip.Lines(pawn);
            statsLabelWidths = new float[statsLines.Count];
            statsSignalViews = new SkillSignalView[statsLines.Count];
            statsSignalIcons = new List<Texture2D>[statsLines.Count];
            statsSignalTips = new string[statsLines.Count];
            statsSkillColWidth = SkillColWidth;
            using (new TextBlock(GameFont.Small))
            {
                for (int i = 0; i < statsLines.Count; i++)
                {
                    SkillLine line = statsLines[i];
                    SkillPresentation presentation = PresentationFor(pawn, line);
                    statsLabelWidths[i] = presentation.LabelWidth;
                    statsSignalViews[i] = presentation.SignalView;
                    statsSignalIcons[i] = presentation.SignalIcons;
                    statsSignalTips[i] = presentation.Tooltip;

                    float iconWidth = presentation.SignalIcons.Count == 0 ? 0f
                        : SkillLabelDecoratorGap
                            + presentation.SignalIcons.Count * SkillDecoratorSize
                            + (presentation.SignalIcons.Count - 1) * SkillDecoratorGap;
                    float requiredWidth = statsLabelWidths[i] + iconWidth
                        + SkillValueGap + SkillValueWidth;
                    statsSkillColWidth = Mathf.Max(statsSkillColWidth, Mathf.Ceil(requiredWidth));
                }
            }
        }

        /// Text colour priority; passion tier comes from the signal snapshot.
        /// Shared by the skills grid and its tooltip badge so they cannot drift.
        private static Color SkillTextColor(SkillLine line, SignalPassionTier tier)
        {
            if (line.Disabled || line.Level <= 1) return ColorDisabled;
            if (line.Level <= 5) return ColorLow;
            if (tier == SignalPassionTier.Major) return ColorPassMajor;
            if (tier == SignalPassionTier.Minor) return ColorPassMinor;
            return Color.white;
        }

        /// <summary>Height of the stats panel for a given pawn (or generic if null).</summary>
        public float StatsPanelHeight(Pawn pawn = null)
        {
            int lineCount = 12;
            if (pawn != null)
            {
                EnsureStats(pawn);
                lineCount = statsLines.Count;
            }
            int rows = (lineCount + SkillCols - 1) / SkillCols;
            float portraitSection = PortraitDisplaySize + 2f + 20f; // portrait + gap + name label
            float skillSection = rows * CellH;
            float contentH = Mathf.Max(portraitSection, skillSection);
            return contentH + StatsPadding * 2f;
        }

        // Open-window snapshot of the desired window size: both walk every
        // pawn's chips through text measurement, so they recompute only when
        // the stamp, map, chip display or skill columns change.
        private int sizeStamp = -1;
        private int sizeMapId = -1;
        private int sizeKey = -1;
        private float desiredWidthCache;
        private float desiredHeightCache;

        private void EnsureSizes()
        {
            EnsureSkillColumnsLoaded();
            int mapId = Find.CurrentMap?.uniqueID ?? -1;
            // Column IDENTITY, not count: swapping a column at the cap keeps the
            // count identical and must still invalidate.
            int key = ((int)TableChips * 31 + skillColumns.Count) * 31
                + (selectedPawn?.thingIDNumber ?? -1);
            foreach (var column in skillColumns)
                key = key * 31 + (column?.shortHash ?? 0);
            if (sizeStamp == UiVersion.Current && sizeMapId == mapId && sizeKey == key) return;
            sizeStamp = UiVersion.Current;
            sizeMapId = mapId;
            sizeKey = key;
            desiredWidthCache = ComputeDesiredWidth();
            desiredHeightCache = ComputeDesiredHeight();
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
                    w += TableChipWidth(store, role, a.pinned) + ChipGap;
                }
                if (w > widestStrip) widestStrip = w;
            }
            float tableWidth = fixedLeft + SkillColumnsWidth() + widestStrip;
            float skillColumnWidth = SkillColWidth;
            if (selectedPawn != null)
            {
                EnsureStats(selectedPawn);
                skillColumnWidth = statsSkillColWidth;
            }
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
                float stripH = LayoutChips(stripW, assignments, store, result: null);
                tableContent += Mathf.Max(RowHeight, stripH + 8f);
            }
            return chrome + paletteSection + tableContent + statsPanel;
        }

        /// The one width formula for a table chip — measurement and layout must
        /// never disagree.
        private float TableChipWidth(RoleStore store, Role role, bool pinned) =>
            RoleChipUI.WidthFor(role, showRemove: true, TableChips, AbbrevIfCompact(store, role), pinned);

        /// The roles-column width used by both desired-height measurement and
        /// live table layout.
        private float TableStripWidth(float tableWidth) => Mathf.Max(300f,
            tableWidth - 16f - 264f - SkillColumnsWidth() - 28f);

        private float LayoutChips(float stripWidth, List<RoleAssignment> assignments, RoleStore store,
            List<(RoleAssignment assignment, Rect rect, int line)> result)
        {
            float x = 0f, y = 0f;
            int line = 0;
            foreach (var a in assignments)
            {
                var role = store.RoleById(a.roleId);
                if (role == null) continue;
                float w = TableChipWidth(store, role, a.pinned);
                if (x + w > stripWidth && x > 0f)
                {
                    line++;
                    x = 0f;
                    y += RoleChipUI.Height + ChipGap;
                }
                result?.Add((a, new Rect(x, y, w, RoleChipUI.Height), line));
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
            foreach (var (role, parent, virtualRow) in RolesTabView.BuildRoleTree(store).rows)
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
            return RolesTabView.BuildSections(store, nested: true)
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

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.60f, 0.62f, 0.64f);
            foreach (var (label, labelRect) in labels)
                Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            foreach (var (role, chipRect) in chips)
            {
                // The click closure allocates: create it only on the one pass
                // that can consume it (left mouse-down inside this chip).
                System.Action onClick = null;
                var pressEvent = Event.current;
                if (pressEvent.type == EventType.MouseDown && pressEvent.button == 0
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
                    showRemove: false, dragSource: null, onClick: onClick);
                if (Mouse.IsOver(chipRect))
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
            colonistFilter = Widgets.TextField(new Rect(rect.x + SearchLabelW + 4f, y, SearchW, SearchH), colonistFilter);

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

            // Scope dropdown: which locations' pawns the table lists (options
            // come from the pawn snapshot — ListedPawns keeps them fresh).
            float scopeX = btnX + RoleBtnW + 8f;
            ListedPawns();
            var scopeOptions = pawnsScopeOptions ?? new List<ScopeOption>();
            if (Widgets.ButtonText(new Rect(scopeX, y, RoleBtnW, SearchH), ColonyScope.LabelOf(scope)))
            {
                var menu = new List<FloatMenuOption>();
                foreach (var option in scopeOptions)
                {
                    var captured = option;
                    var item = new FloatMenuOption(ColonyScope.LabelOf(option), () =>
                    {
                        scope = captured;
                        InvalidatePawnSnapshot();
                    });
                    if (option.IsShip) item.tooltip = "WR_ShipTip".Translate();
                    menu.Add(item);
                }
                Find.WindowStack.Add(new FloatMenu(menu));
            }

            if (FiltersActive)
            {
                var clearRect = new Rect(scopeX + RoleBtnW + 8f, y + (SearchH - 18f) / 2f, 18f, 18f);
                TooltipHandler.TipRegion(clearRect, "WR_ClearFilters".Translate());
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                {
                    colonistFilter = "";
                    roleFilterId = -1;
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
                            options.Add(new FloatMenuOption(label, () =>
                            {
                                if (added) // re-selecting toggles the column off
                                {
                                    if (profile.GetSortColumn() == captured.defName) SetSort("");
                                    skillColumns.Remove(captured);
                                    SaveSkillColumns();
                                    return;
                                }
                                if (skillColumns.Count >= MaxSkillColumns)
                                {
                                    // Full: the oldest column (front of the list —
                                    // columns append in selection order) gives way.
                                    if (profile.GetSortColumn() == skillColumns[0].defName) SetSort("");
                                    skillColumns.RemoveAt(0);
                                }
                                skillColumns.Add(captured);
                                SaveSkillColumns();
                                SetSort(captured.defName); // adding a column sorts by it
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
                if (Widgets.ButtonText(groupRect, CurrentGroupSource.Label))
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

        // Skill columns (mirrored to the profile's storage so the table reopens
        // exactly as closed). Clicking a column header sorts by it — one fixed
        // order per column.
        private readonly List<SkillDef> skillColumns = new List<SkillDef>();
        private bool skillColumnsLoaded;
        private const int MaxSkillColumns = 3;
        private const float SkillCellContentW = 82f; // "12.37" + up to two signal decorators

        private void EnsureSkillColumnsLoaded()
        {
            if (skillColumnsLoaded) return;
            skillColumnsLoaded = true;
            var saved = profile.GetSkillColumns();
            if (saved != null)
                foreach (var defName in saved)
                {
                    var def = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
                    if (def != null && !skillColumns.Contains(def) && skillColumns.Count < MaxSkillColumns)
                        skillColumns.Add(def);
                }
            // A persisted sort must belong to a live column.
            string sort = profile.GetSortColumn();
            if (!sort.NullOrEmpty() && !skillColumns.Any(s => s.defName == sort))
                profile.SetSortColumn("");
        }

        private void SaveSkillColumns() =>
            profile.SetSkillColumns(skillColumns.Select(d => d.defName).ToList());

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

        internal static void ClearSkillHeaderLabelCache() => skillHeaderLabels.Clear();

        /// Header label (localized) or cell content, whichever is wider.
        internal float SkillColumnWidth(SkillDef skill)
        {
            Text.Font = GameFont.Small;
            return Mathf.Max(SkillCellContentW, WrText.FitWidth(SkillHeaderLabel(skill)) + 18f);
        }

        private float SkillColumnsWidth()
        {
            EnsureSkillColumnsLoaded();
            float w = 0f;
            foreach (var skill in skillColumns) w += SkillColumnWidth(skill);
            return w;
        }

        internal static float SkillSortValue(Pawn pawn, SkillDef skill)
        {
            var sr = pawn.skills?.GetSkill(skill);
            if (sr == null || sr.TotallyDisabled) return -1f;
            return sr.Level + Mathf.Clamp(sr.xpSinceLastLevel / sr.XpRequiredForLevelUp, 0f, 0.99f);
        }

        /// Table display order (view-only; plan logic is order-independent): the
        /// vanilla colonist bar's order — including manual drag-reordering — or
        /// A-Z; an active skill sort (header click) overrides, highest first.
        private List<Pawn> OrderedForDisplay(List<Pawn> pawns)
        {
            List<Pawn> ordered;
            if (profile.GetColonistOrder() == ColonistOrder.Alphabetical)
                ordered = pawns.OrderBy(p => p.LabelShortCap, System.StringComparer.OrdinalIgnoreCase).ToList();
            else
            {
                var bar = Find.ColonistBar?.GetColonistsInOrder();
                if (bar == null) ordered = pawns;
                else
                {
                    var pool = new HashSet<Pawn>(pawns);
                    ordered = bar.Where(pool.Contains).ToList();
                    if (ordered.Count < pawns.Count)
                        foreach (var pawn in pawns)
                            if (!ordered.Contains(pawn))
                                ordered.Add(pawn);
                }
            }

            var sortSkill = SortSkill();
            if (sortSkill != null)
                ordered = ordered.OrderByDescending(p => SkillSortValue(p, sortSkill)).ToList();
            return ordered;
        }

        private SkillDef SortSkill()
        {
            string sort = profile.GetSortColumn();
            return sort.NullOrEmpty() ? null : DefDatabase<SkillDef>.GetNamedSilentFail(sort);
        }

        private void SetSort(string column)
        {
            if (profile.GetSortColumn() == column) return;
            profile.SetSortColumn(column);
        }

        private bool FiltersActive =>
            !colonistFilter.NullOrEmpty() || roleFilterId != -1;

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
                        if (!role.blocker && role.CoversOrMatches(selected))
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

        // Open-window snapshot of the unified role tips: models are built and
        // registered once per (role, context[, pawn]) per stamp, never per pass.
        private readonly Dictionary<(int roleId, RoleTipContext context, Pawn pawn), string> roleTipCache
            = new Dictionary<(int, RoleTipContext, Pawn), string>();
        private int roleTipStamp = -1;

        /// The one role tooltip: palette chips, tree rows and assignment chips
        /// share the content; context varies the actions and pawn facts.
        internal string RoleTipText(Role role, RoleTipContext context, Pawn pawn = null)
        {
            var store = RoleStore.Current;
            if (store == null) return role.label;
            if (roleTipStamp != UiVersion.Current)
            {
                roleTipCache.Clear();
                roleTipStamp = UiVersion.Current;
            }
            var key = (role.id, context, pawn);
            if (!roleTipCache.TryGetValue(key, out var text))
                roleTipCache[key] = text =
                    Patches.Patch_ActiveTip_TipRect.Register(BuildRoleTip(store, role, context, pawn));
            return text;
        }

        private TipModel BuildRoleTip(RoleStore store, Role role, RoleTipContext context, Pawn pawn)
        {
            var model = new TipModel
            {
                Title = role.label.Colorize(ColorPassMinor),
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
                var target = store.RoleById(PathTargetRoleId(path));
                string recommend = "WR_TipTrainingRecommend".Translate(band);
                if (target?.id != role.id)
                    recommend += " " + "WR_TipTrainingPath".Translate(
                        (target?.label ?? path.name).Colorize(ColorPassMinor));
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
                if (role.enabled && assignment?.enabled == true && !RulesPass(role, pawn))
                {
                    string reason = SuppressionReason(role, pawn);
                    if (!reason.NullOrEmpty())
                        (state = model.AddSection()).Text(TipText.Warning(reason));
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

        /// The path's target: the role holding its highest band.
        private static int PathTargetRoleId(TrainingPath path)
        {
            int target = -1, bestMin = int.MinValue;
            for (int i = 0; i < path.roleIds.Count; i++)
                if (path.bandMins[i] > bestMin)
                {
                    bestMin = path.bandMins[i];
                    target = path.roleIds[i];
                }
            return target;
        }

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
                    parts.Add(giver != null ? RolesTabView.GetGiverDisplayName(giver) : entry.DefName);
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
                if (pawn.skills == null) continue;
                var candidates = new List<SkillBucketCandidate>(skills.Count);
                foreach (var skill in skills)
                {
                    var sr = pawn.skills.GetSkill(skill);
                    if (sr == null || sr.TotallyDisabled) continue;
                    candidates.Add(new SkillBucketCandidate(skill.defName, sr.Level));
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
            var sections = Sections(store);
            int listedCount = 0;
            foreach (var section in sections) listedCount += section.Members.Count;
            if (listedCount == 0 && FiltersActive)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(rect, "WR_NoFilterMatches".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Chip strips wrap against the roles column; everything else is
            // fixed-width, so the row-height estimate is exact.
            EstimatedStripWidth = TableStripWidth(rect.width);

            DrawTableHeader(new Rect(rect.x, rect.y, rect.width - 16f, TableHeaderH), store);

            bool grouped = Grouped;
            var outRect = new Rect(rect.x, rect.y + TableHeaderH, rect.width, rect.height - TableHeaderH);
            lastTableViewH = outRect.height;
            float viewW = outRect.width - 16f;

            float totalH = 0f;
            foreach (var section in sections)
            {
                if (grouped) totalH += GroupHeaderH;
                if (!grouped || !IsCollapsed(section.Key))
                    foreach (var pawn in section.Members)
                        totalH += RowHeightOf(pawn);
            }

            Widgets.BeginScrollView(outRect, ref tableScroll, new Rect(0f, 0f, viewW, totalH));
            float y = 0f;
            foreach (var section in sections)
            {
                if (grouped)
                {
                    if (y + GroupHeaderH >= tableScroll.y && y <= tableScroll.y + outRect.height)
                        DrawGroupHeader(new Rect(0f, y, viewW, GroupHeaderH), section);
                    y += GroupHeaderH;
                    if (IsCollapsed(section.Key)) continue;
                }
                foreach (var pawn in section.Members)
                {
                    float rowH = RowHeightOf(pawn);
                    if (y + rowH >= tableScroll.y && y <= tableScroll.y + outRect.height)
                        DrawRow(new Rect(0f, y, viewW, rowH), pawn, store);
                    y += rowH;
                }
            }
            Widgets.EndScrollView();
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
            var sortSkill = SortSkill();

            // Priority grid over every listed colonist (the filtered table set).
            var gridRect = new Rect(rect.xMax - 26f, rect.y + (rect.height - 18f) / 2f, 18f, 18f);
            TooltipHandler.TipRegion(gridRect, "WR_ShowPriorityGridTip".Translate());
            if (Widgets.ButtonImage(gridRect, TexButton.Info))
            {
                var listed = new List<Pawn>();
                foreach (var section in Sections(store))
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
                GUI.color = ColorPassMinor;
            Widgets.Label(new Rect(nameRect.x + 4f, nameRect.y, nameRect.width - 8f, nameRect.height - 2f),
                colonistHeaderCache);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawHighlightIfMouseover(nameRect);
            if (Widgets.ButtonInvisible(nameRect))
            {
                if (sortSkill != null) SetSort("");
                else profile.SetColonistOrder(order == ColonistOrder.Alphabetical
                    ? ColonistOrder.ColonistBar : ColonistOrder.Alphabetical);
            }

            float x = rect.x + 264f;
            for (int i = 0; i < skillColumns.Count; i++)
            {
                var skill = skillColumns[i];
                float w = SkillColumnWidth(skill);
                var headerRect = new Rect(x, rect.y, w, rect.height);
                var closeRect = new Rect(headerRect.xMax - 16f, headerRect.yMax - 20f, 14f, 14f);
                if (Widgets.ButtonImage(closeRect, TexButton.CloseXSmall))
                {
                    if (sortSkill == skill) SetSort("");
                    RemoveSkillColumn(i);
                    return;
                }
                bool wrap = Text.WordWrap;
                Text.WordWrap = false;
                Text.Anchor = TextAnchor.LowerLeft;
                if (sortSkill == skill) GUI.color = ColorPassMinor; // marks the sort column
                Widgets.Label(new Rect(headerRect.x + 2f, headerRect.y, headerRect.width - 24f, headerRect.height - 2f),
                    SkillHeaderLabel(skill));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = wrap;
                var clickRect = new Rect(headerRect.x, headerRect.y, headerRect.width - 18f, headerRect.height);
                Widgets.DrawHighlightIfMouseover(clickRect);
                if (Widgets.ButtonInvisible(clickRect)) SetSort(skill.defName);
                x += w;
            }
        }

        private void DrawGroupHeader(Rect rect, GroupSection<Pawn> section)
        {
            Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.06f));
            bool collapsed = IsCollapsed(section.Key);
            var arrowRect = new Rect(rect.x + 6f, rect.y + (rect.height - 18f) / 2f, 18f, 18f);
            GUI.DrawTexture(arrowRect, collapsed ? TexButton.Reveal : TexButton.Collapse);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(arrowRect.xMax + 6f, rect.y, rect.width - arrowRect.xMax - 10f, rect.height),
                sectionTitles.TryGetValue(section.Key, out var title)
                    ? title : section.Title + " (" + section.Members.Count + ")");
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawHighlightIfMouseover(rect);
            if (Widgets.ButtonInvisible(rect)) ToggleCollapsed(section.Key);
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
            foreach (var skill in skillColumns)
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

        // ----- Grouping -----

        private GroupSourceDef CurrentGroupSource
        {
            get
            {
                var sources = GroupSources.All();
                string key = profile.GetGroupBy();
                foreach (var source in sources)
                    if (source.Key == key) return source;
                return sources[0];
            }
        }

        private bool Grouped => CurrentGroupSource.Partition != null;

        // Open-window snapshot of the full display pipeline (filter -> order ->
        // group). Keyed on every UI-local input as plain fields (a composed key
        // string would itself allocate per pass); sim-side changes arrive via
        // the UiVersion stamp.
        private List<GroupSection<Pawn>> sectionsCache;
        private int sectionsStamp = -1;
        private int sectionsMapId = -1;
        private string sectionsFilter;
        private int sectionsRoleFilter;
        private string sectionsGroupBy;
        private string sectionsSort;
        private ColonistOrder sectionsOrder;

        private List<GroupSection<Pawn>> Sections(RoleStore store)
        {
            var pawns = ListedPawns();
            var order = profile.GetColonistOrder();
            if (sectionsCache == null || sectionsStamp != UiVersion.Current
                || sectionsMapId != pawnsMapId || sectionsFilter != colonistFilter
                || sectionsRoleFilter != roleFilterId || sectionsGroupBy != profile.GetGroupBy()
                || sectionsSort != profile.GetSortColumn() || sectionsOrder != order)
            {
                sectionsStamp = UiVersion.Current;
                sectionsMapId = pawnsMapId;
                sectionsFilter = colonistFilter;
                sectionsRoleFilter = roleFilterId;
                sectionsGroupBy = profile.GetGroupBy();
                sectionsSort = profile.GetSortColumn();
                sectionsOrder = order;
                sectionsCache = GroupedSections(OrderedForDisplay(FilteredPawns(pawns, store)));
                sectionTitles.Clear();
                foreach (var section in sectionsCache)
                    sectionTitles[section.Key] = section.Title + " (" + section.Members.Count + ")";
            }
            return sectionsCache;
        }

        /// "Title (N)" per section, composed with the snapshot — headers draw per pass.
        private readonly Dictionary<string, string> sectionTitles = new Dictionary<string, string>();

        /// The visible sections: one flat section, or Core-partitioned groups.
        private List<GroupSection<Pawn>> GroupedSections(List<Pawn> pawns)
        {
            var source = CurrentGroupSource;
            if (source.Partition == null)
                return new List<GroupSection<Pawn>>
                {
                    new GroupSection<Pawn> { Key = "", Title = "", Members = pawns }
                };
            return source.Partition(pawns);
        }

        // Group keys are already grouping-prefixed ("faction|Zorble"), so they
        // double as the persisted collapse ids.
        private bool IsCollapsed(string groupKey) =>
            profile.GetCollapsedGroups()?.Contains(groupKey) == true;

        private void ToggleCollapsed(string groupKey)
        {
            var collapsed = profile.GetCollapsedGroups();
            if (collapsed == null) return;
            if (!collapsed.Remove(groupKey))
                collapsed.Add(groupKey);
            profile.SetCollapsedGroups(collapsed);
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
                RoleClipboard.CopyFrom(toCopy);
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
            var sections = Sections(RoleStore.Current);
            return Grouped ? sections.Where(s => !IsCollapsed(s.Key)).ToList() : sections;
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
            if (!ignoreGroups && Grouped)
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
            if (Grouped && sections.Count > 1)
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
            var sections = Sections(RoleStore.Current);
            bool grouped = Grouped;
            float y = 0f, top = -1f, bottom = -1f;
            foreach (var section in sections)
            {
                if (grouped)
                {
                    y += GroupHeaderH;
                    if (IsCollapsed(section.Key)) continue;
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
        {
            EnsureSkillColumnsLoaded();
            if (index >= 0 && index < skillColumns.Count)
            {
                skillColumns.RemoveAt(index);
                SaveSkillColumns();
            }
        }

        // Open-window snapshot (see UiVersion): pawn list, scope options and the
        // spans-locations flag rebuild only when the stamp or current map moves.
        private int pawnsStamp = -1;
        private int pawnsMapId = -1;
        private List<Pawn> pawnsCache;
        private List<ScopeOption> pawnsScopeOptions;
        private bool pawnsSpans;

        internal void InvalidatePawnSnapshot() => pawnsStamp = -1;

        /// <summary>The colonist list under the active scope (no baby pawns).</summary>
        internal List<Pawn> ListedPawns()
        {
            if (Find.CurrentMap == null) return new List<Pawn>();
            int mapId = Find.CurrentMap.uniqueID;
            if (pawnsCache == null || pawnsStamp != UiVersion.Current || pawnsMapId != mapId)
            {
                pawnsStamp = UiVersion.Current;
                pawnsMapId = mapId;
                pawnsScopeOptions = ScopeEngine.BuildOptions(ColonyScope.Locations());
                scope = ScopeEngine.Revalidate(scope, pawnsScopeOptions);
                pawnsCache = profile.PawnsIn(scope);
                pawnsSpans = ScopeEngine.SpansMultipleLocations(
                    pawnsCache.Select(ColonyScope.LocationIdOf));
            }
            return pawnsCache;
        }

        /// True when the listed pawns come from more than one map/caravan —
        /// colony planning wants a single location (Fix My Colony disables).
        internal bool ScopeSpansMultipleLocations
        {
            get { ListedPawns(); return pawnsSpans; }
        }

        /// Colonists and slaves of one map, for location-scoped planning.
        private static List<Pawn> MapColonists(Map map)
        {
            if (map == null) return new List<Pawn>();
            return map.mapPawns.FreeColonistsSpawned
                .Concat(map.mapPawns.SlavesOfColonySpawned)
                .Where(p => !p.DevelopmentalStage.Baby())
                .Distinct()
                .ToList();
        }

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
                RoleClipboard.CopyFrom(toCopy);
                WrToast.Show("WR_CopiedRoles".Translate(pawn.LabelShortCap), MessageTypeDefOf.NeutralEvent);
            }
            Color pasteColor = RoleClipboard.HasContent ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            TooltipHandler.TipRegion(pasteRect, "WR_PasteRolesTip".Translate());
            if (Widgets.ButtonImage(pasteRect, TexButton.Paste, pasteColor) && RoleClipboard.HasContent)
                RoleCommands.PasteRoleSet(pawn, RoleClipboard.Content);
        }

        /// Chip-strip height for a pawn against the estimated chip-column width —
        /// the table row height comes from this.
        // Open-window snapshot of per-pawn chip layouts at the table's strip
        // width: the table body (heights + chip rects) becomes dictionary reads.
        // Floored like EstimatedStripWidth so draw and measure share one key.
        private readonly Dictionary<Pawn, (List<(RoleAssignment assignment, Rect rect, int line)> layout, float height)>
            chipLayouts = new Dictionary<Pawn, (List<(RoleAssignment, Rect, int)>, float)>();
        private int chipLayoutStamp = -1;
        private float chipLayoutWidth = -1f;
        private int chipLayoutDisplay = -1;

        private (List<(RoleAssignment assignment, Rect rect, int line)> layout, float height)
            ChipLayoutFor(Pawn pawn, RoleStore store, float stripWidth)
        {
            stripWidth = Mathf.Max(300f, stripWidth);
            if (chipLayoutStamp != UiVersion.Current || chipLayoutWidth != stripWidth
                || chipLayoutDisplay != (int)TableChips)
            {
                chipLayouts.Clear();
                chipLayoutStamp = UiVersion.Current;
                chipLayoutWidth = stripWidth;
                chipLayoutDisplay = (int)TableChips;
            }
            if (chipLayouts.TryGetValue(pawn, out var cached)) return cached;
            store.pawnSets.TryGetValue(pawn, out var set);
            var layout = new List<(RoleAssignment, Rect, int)>();
            float height = set == null || set.assignments.Count == 0
                ? RoleChipUI.Height
                : LayoutChips(chipLayoutWidth, set.assignments, store, layout);
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
            var sr = pawn.skills?.GetSkill(skill);
            if (sr == null || sr.TotallyDisabled)
            {
                GUI.color = ColorDisabled;
                Widgets.Label(new Rect(cell.x + 2f, cell.y, 44f, cell.height), "-");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            SkillPresentation presentation = PresentationFor(pawn, SkillsTip.Line(sr));
            Color textColor = SkillTextColor(
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
                TooltipHandler.TipRegion(cell, presentation.Tooltip);
        }

        // Open-window snapshot of rule outcomes: Pass hits the map (gravship
        // lookup) per chip otherwise. Hour flips and map moves bump the stamp.
        // Keyed by role+pawn (not per view), so deliberately shared statics.
        private static readonly Dictionary<(int roleId, Pawn pawn), bool> rulesPassCache
            = new Dictionary<(int, Pawn), bool>();
        private static int rulesPassStamp = -1;

        private static bool RulesPass(Role role, Pawn pawn)
        {
            if (!role.HasRules) return true;
            if (rulesPassStamp != UiVersion.Current)
            {
                rulesPassCache.Clear();
                rulesPassStamp = UiVersion.Current;
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
                var (assignment, localRect, _) = layout[chipIndex];
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
                var click = RoleChipUI.Draw(chipRect, role, style,
                    showRemove: true, dragSource: pawn,
                    onClick: onClick,
                    display: TableChips, abbrev: AbbrevIfCompact(store, role),
                    pinned: assignment.pinned);
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
                if (Mouse.IsOver(chipRect))
                    TooltipHandler.TipRegion(chipRect,
                        RoleTipText(role, RoleTipContext.AssignmentChip, pawn));
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
                        layout, t => t.rect);

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
            Widgets.DrawBoxSolidWithOutline(rect, new Color(0.08f, 0.08f, 0.08f, 0.9f), new Color(1f, 1f, 1f, 0.15f));
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

            EnsureStats(selectedPawn);
            float skillColWidth = statsSkillColWidth;

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

            var lines = statsLines;
            if (lines.Count == 0) return;

            Text.Font = GameFont.Small;
            for (int i = 0; i < lines.Count; i++)
            {
                int col = i % SkillCols;
                int row = i / SkillCols;
                var line = lines[i];
                SkillSignalView signalView = statsSignalViews[i];
                List<Texture2D> signalIcons = statsSignalIcons[i];

                float cellX = (col == 0) ? col1X : col2X;
                float cellY = rect.y + row * CellH;

                if (col >= SkillCols) continue;

                Color textColor = SkillTextColor(line, signalView.PassionTier);

                float xCursor = cellX;

                // Skill label (wrap off: a long modded skill name must clip, not
                // wrap out of the single-line cell)
                GUI.color = textColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                string labelText = line.Label;
                float labelWidth = statsLabelWidths[i];
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
                string signalTip = statsSignalTips[i];
                if (signalTip != null && Mouse.IsOver(cellRect))
                    TooltipHandler.TipRegion(cellRect, signalTip);
            }

            // Recommended Roles section: mirrors the Make It So outcome — kept roles
            // subtle, additions normal, removals struck — so the panel IS the preview
            // and the button applies directly.
            if (profile.ShowRecommendations && recX < rect.xMax)
            {
                float recW = rect.xMax - recX;
                EnsurePreview(store, selectedPawn);
                var pawnPlan = previewPlan;

                WrText.HeaderLabel(new Rect(recX, rect.y, recW, 28f), "WR_RecommendedRoles".Translate());

                // Chips wrapping below header; the bottom 28f is reserved for "Make It So".
                float chipBottom = rect.yMax - 28f;
                float chipY = rect.y + 28f;
                float chipX = recX;
                var chips = previewChips;
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
                            RoleCommands.RemoveRoleFromPawn(capturedPawn, capturedId);
                        if (state == Dialog_ChangesPreview.ChipState.Removed)
                        {
                            RoleChipUI.DrawRemovedOutline(chipRect);
                        }
                        if (Mouse.IsOver(chipRect))
                            TooltipHandler.TipRegion(chipRect, tip
                                ?? (state == Dialog_ChangesPreview.ChipState.Removed
                                    ? "WR_WillBeRemoved".Translate()
                                    : "WR_AlreadyAssigned".Translate()));
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
                            TooltipHandler.TipRegion(chipRect, tip);
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

        /// One wrapped chip line in target order: kept roles dimmed
        /// already-assigned style, added roles normal, removed roles struck
        /// corner-to-corner and slotted back in near their original position.
        private Dialog_ChangesPreview.PawnPreview PreviewEntry(RoleStore store, PawnFixPlan plan)
        {
            store.pawnSets.TryGetValue(plan.pawn, out var set);
            var existing = set?.assignments ?? new List<RoleAssignment>();
            var existingIds = new HashSet<int>(existing.Select(a => a.roleId));
            var targetIds = new HashSet<int>(plan.target.Select(a => a.roleId));
            SkillBucketSnapshot skillBuckets = SignalSnapshotFor(plan.pawn).SkillBuckets;

            var line = new Dialog_ChangesPreview.Line();
            foreach (var a in plan.target)
            {
                var role = store.RoleById(a.roleId);
                if (role == null) continue;
                bool kept = existingIds.Contains(a.roleId);
                var state = kept
                    ? Dialog_ChangesPreview.ChipState.Kept
                    : Dialog_ChangesPreview.ChipState.Added;
                plan.explanations.TryGetValue(role.id, out var explanation);
                line.chips.Add((role, state, RecommendationPresentation.RegisterTooltip(
                    store, role, state, explanation, skillBuckets)));
            }
            for (int i = 0; i < existing.Count; i++)
            {
                if (targetIds.Contains(existing[i].roleId)) continue;
                var role = store.RoleById(existing[i].roleId);
                if (role == null) continue;
                var state = Dialog_ChangesPreview.ChipState.Removed;
                plan.explanations.TryGetValue(role.id, out var explanation);
                line.chips.Insert(Mathf.Min(i, line.chips.Count),
                    (role, state, RecommendationPresentation.RegisterTooltip(
                        store, role, state, explanation, skillBuckets)));
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
                if (!plan.HasChanges) continue;
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
                if (!plan.HasChanges) continue;
                RoleCommands.PasteRoleSet(plan.pawn, plan.target);
            }
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
            public bool orderChanged;
            public bool HasChanges => added.Count > 0 || removed.Count > 0 || orderChanged;
            public Dictionary<int, RoleRecommendationExplanation> explanations =
                new Dictionary<int, RoleRecommendationExplanation>();
        }

        /// One engine run builds every pawn's plan: RecsAdapter projects the
        /// game state, RecsEngine.Run applies the rule pipeline, and this
        /// method only maps results to PawnFixPlans (diff + explanations).
        private List<PawnFixPlan> BuildColonyFixPlan(Map map)
        {
            var plans = new List<PawnFixPlan>();
            var store = RoleStore.Current;
            if (store == null) return plans;
            var pawns = MapColonists(map);
            var results = RecsEngine.Run(RecsAdapter.BuildColonyView(
                store, pawns, pawnSignalSnapshots.Get));

            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                store.pawnSets.TryGetValue(pawn, out var set);
                var existing = set?.assignments ?? new List<RoleAssignment>();
                var target = results[i].Assignments
                    .Select(a => new RoleAssignment
                    { roleId = a.RoleId, enabled = a.Enabled, pinned = a.Pinned })
                    .ToList();

                var plan = new PawnFixPlan
                {
                    pawn = pawn,
                    target = target,
                    orderChanged = !existing.Select(a => a.roleId)
                        .SequenceEqual(target.Select(a => a.roleId))
                };
                foreach (var kv in results[i].Explanations)
                    plan.explanations[kv.Key] = kv.Value;
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
