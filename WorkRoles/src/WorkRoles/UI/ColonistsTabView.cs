using System.Collections.Generic;
using System.Linq;
using RimShared.Common;
using RimShared.UiLib;
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
        private readonly ActivityState activityState;
        private readonly ColonistSelectedPanelState selectedPanelState;
        private readonly System.Func<Pawn, PawnExternalSnapshot>
            externalSnapshotProvider;

        public ColonistsTabView(ColonistsViewProfile profile)
        {
            this.profile = profile;
            recommendationState = new ColonistRecommendationState();
            statsState = new ColonistStatsState();
            rosterState = new ColonistsRosterState(profile, statsState.SkillSortValue);
            roleCapabilityState = new ColonistRoleCapabilityState();
            activityState = new ActivityState();
            selectedPanelState = new ColonistSelectedPanelState(activityState);
            externalSnapshotProvider = ExternalSnapshotFor;
        }

        private Vector2 paletteScroll;
        private Pawn? selectedPawn;

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
            internal TableLayoutRow(ColonistSectionSnapshot? section,
                Pawn? pawn, bool collapsed)
            {
                Section = section;
                Pawn = pawn;
                Collapsed = collapsed;
            }

            internal ColonistSectionSnapshot? Section { get; }
            internal Pawn? Pawn { get; }
            internal bool Collapsed { get; }
        }

        // Owner: Colonists window. Key: section snapshot identity, pawn-scope
        // stamp, strip width, chip-display mode, skill-caption toggle, and exact
        // row text metrics (row minimum height). Value: view-owned flattened
        // row geometry and group-collapse presentation; Pawn references are
        // stable external identities and the mutable builder buffers never
        // escape this view. Dependencies: section grouping/collapse, per-pawn
        // chip heights, and every key component.
        // Refresh: immediate on the next table draw after invalidation. Equality:
        // exact keys reuse row/layout identity. Teardown: InvalidateTableLayout
        // and ReleaseSnapshots release rows, heights, and source references.
        private ColonistSectionsSnapshot? tableLayoutSections;
        private readonly List<TableLayoutRow> tableLayoutRows = new List<TableLayoutRow>();
        private VariableViewportLayout? tableRowLayout;
        private ScopeCacheStamp tableLayoutStamp = ScopeCacheStamp.Invalid;
        private float tableLayoutStripWidth = -1f;
        private int tableLayoutDisplay = -1;
        private bool tableLayoutCaptions;
        private RowTextMetrics tableLayoutTextMetrics;
        private int tableListedCount;

        /// One view-owned revision for every cache whose contents depend on the
        /// active pawn scope. Reading it observes map transitions first.
        internal int PawnListRevision => rosterState.PawnListRevision;
        private ScopeCacheStamp PawnListStamp => rosterState.PawnListStamp;

        // View-local table filters (never synced, never persisted).
        private const string SearchControlName = "WR_ColonistSearch";
        private bool focusSearch;

        private const float PaletteMaxHeight = 260f;   // palette scrolls beyond this
        /// The first palette row is a caption whose line box carries roughly
        /// this much top leading before the first ink pixel. The panel's top
        /// inset is reduced by it so the visual gap to the caption matches the
        /// other sides' padding.
        private const float PaletteCaptionInkAllowance = 2f;
        private const float MinimumClusterLabelH = 15f;
        private const float ClusterGapX = 20f;
        private const float ClusterGapY = 4f;
        private const float FilterInputH = 24f;
        private const float FilterCaptionAdvance = 16f;
        private const float FilterCaptionGap = 1f;
        private const float RowHeight = 35f;
        /// Stacked name and caption line boxes may overlap by this much: glyph
        /// ink stops short of a line box's edges (ascender and descender
        /// leading), so the pair reads tighter than the sum of its line
        /// heights. Past it the row grows instead.
        private const float LineBoxOverlap = 4f;
        private const float PortraitSize = 30f;
        private const float NameWidth = 150f;
        private const float IconButton = 24f;
        private const float ChipGap = 4f;
        private const float StatsPanelMargin = 8f;
        /// Palette panel insets: sides and bottom breathe at 8px; the top
        /// keeps a tighter 6px so the caption hugs the panel edge.
        private const float PalettePanelPadding = 8f;
        private const float PalettePanelTopPadding = 6f;
        /// Design width: fixed chrome (tab strip + FMC button, filter row, editor
        /// swatch grid) fits at this size, so it doubles as the window's min width.
        internal const float DefaultWidth = 1010f;
        private const float DefaultHeight = 684f;

        private const float PortraitDisplaySize = 96f;
        // Locked-height stats panel (see TODO review note): the smaller frame
        // and the activity slot below the name borrow bottom padding instead of
        // growing the panel.
        private const float PortraitFrameSize = 88f;
        private const float PortraitFrameH = 96f;
        private const float PortraitNameH = 20f;
        private const float ActivitySlotH = 18f;

        // Stats panel layout constants
        private const float SkillColWidth = 200f;   // minimum; signal decorators may widen both columns
        private const int   SkillCols = 2;
        private const float CellH = 20f;
        private const float StatsPadding = 12f;     // top+bottom padding inside box
        private const float ColSepWidth = 2f;       // separator width
        private const float ColSepMargin = 12f;     // space on each side of separator
        private const float SkillDecoratorSize = 16f;
        private const float SkillDecoratorGap = 2f;
        private const float SkillLabelDecoratorGap = 4f;
        private const float SkillValueGap = 8f;
        private const float SkillValueWidth = 48f;
        // Small-font row labels paint one pixel above their geometric center.
        // Keep this as an explicit visual-only correction: row pitch, hit
        // regions, portraits, chips, and decorators retain their layout.
        private const float ColonistRowTextOffsetY = 1f;

        public void Reset()
        {
            paletteScroll = Vector2.zero;
            tableScroll = Vector2.zero;
            selectedPawn = null;
            // Opening adopts the player's in-game pawn selection (when listed)
            // and scrolls the selection into view centered.
            pendingSelectFromGame = true;
            pendingCenterSelected = true;
            rosterState.Reset();
            activityState.Release();
            selectedPanelState.Release();
            ColonyGroupsDataSource.InvalidateSnapshot(); // fresh membership per window open
            roleCapabilityState.Invalidate();
            recommendationState.Reset();
            InvalidateRoleVerdicts();
            InvalidatePawnSnapshot();
            statsState.Reset(rosterState.SnapshotPawns());
            // Opening re-snapshots everything (stats would otherwise stay stale
            // across a reopen when nothing bumped the version in between).
            sizeStamp = chipLayoutStamp = roleTipStamp = rulesPassStamp
                = ScopeCacheStamp.Invalid;
            skillColumnsWidthStamp = ScopeCacheStamp.Invalid;
            chipSequenceStamp = ScopeCacheStamp.Invalid;
            paletteSnapshot = null;
            paletteCatalog = null;
            paletteLayoutTips = null;
            paletteTips = null;
            paletteTipOwner = null;
            paletteTipScopeStamp = ScopeCacheStamp.Invalid;
            paletteTipLanguageRevision = -1;
            paletteTipDefinitionRevision = -1;
            paletteTipExternalGeneration = 0;
            paletteTipBuiltExternalGeneration = -1;
            paletteVerdictSnapshot = null;
            ReleaseTableHeaderSnapshot();
            InvalidateChromeSnapshot();
            InvalidateTableLayout();
        }

        /// Language-only invalidation. User selection, filters, scroll positions,
        /// scope and disclosure state remain untouched.
        internal void InvalidateLanguageCaches()
        {
            InvalidateChromeSnapshot();
            sizeStamp = ScopeCacheStamp.Invalid;
            sizeOwner = null;
            skillColumnsWidthStamp = ScopeCacheStamp.Invalid;

            paletteSnapshot = null;
            paletteCatalog = null;
            paletteLayoutTips = null;
            paletteTipLanguageRevision = -1;
            paletteVerdictSnapshot = null;

            statsState.InvalidateLanguageCaches();
            roleCapabilityState.Invalidate();
            activityState.Release();
            selectedPanelState.Release();
            InvalidateRoleVerdicts();
            chipLayouts.Clear();
            chipLayoutStamp = ScopeCacheStamp.Invalid;
            chipSequenceStamp = ScopeCacheStamp.Invalid;

            roleTipCache.Clear();
            roleTipStamp = ScopeCacheStamp.Invalid;
            recommendationState.InvalidateLanguageCaches();

            rosterState.InvalidateLanguageCaches();
            rowTextMetricsSmallLineHeight = -1f;
            rowTextMetricsCaptionLineHeight = -1f;
            filterMetricsInitialized = false;
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
            InvalidateRoleVerdicts();
            sizeStamp = ScopeCacheStamp.Invalid;
            skillColumnsWidthStamp = ScopeCacheStamp.Invalid;
            chipLayouts.Clear();
            chipLayoutStamp = ScopeCacheStamp.Invalid;
            chipSequenceStamp = ScopeCacheStamp.Invalid;
            roleTipCache.Clear();
            roleTipStamp = ScopeCacheStamp.Invalid;
            unchecked { paletteTipExternalGeneration++; }
            unchecked { tableHeaderExternalGeneration++; }
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
            activityState.Release();
            selectedPanelState.Release();

            selectedPawn = null;
            recommendationState.ReleaseSnapshots();
            InvalidateRoleVerdicts();

            roleTipCache.Clear();
            roleTipStamp = ScopeCacheStamp.Invalid;
            pawnTips.Clear();
            rosterState.ReleaseSnapshots();
            chipLayouts.Clear();
            chipLayoutOwner = null;
            chipLayoutStamp = ScopeCacheStamp.Invalid;
            chipSequences.Clear();
            chipSequenceOwner = null;
            chipSequenceCatalog = null;
            chipSequenceStamp = ScopeCacheStamp.Invalid;
            chipSequenceDisplay = -1;
            chipSequenceTuningRevision = -1;
            chipWidthGrid = default;
            rulesPassCache.Clear();
            rulesPassStamp = ScopeCacheStamp.Invalid;
            InvalidateTableLayout();

            ColonyGroupsDataSource.InvalidateSnapshot();
            paletteSnapshot = null;
            paletteCatalog = null;
            paletteLayoutTips = null;
            paletteLayoutW = -1f;
            paletteLayoutMode = -1;
            paletteTips = null;
            paletteTipOwner = null;
            paletteTipScopeStamp = ScopeCacheStamp.Invalid;
            paletteTipLanguageRevision = -1;
            paletteTipDefinitionRevision = -1;
            paletteTipExternalGeneration = 0;
            paletteTipBuiltExternalGeneration = -1;
            paletteVerdictSnapshot = null;
            ReleaseTableHeaderSnapshot();
            InvalidateChromeSnapshot();
            sizeStamp = ScopeCacheStamp.Invalid;
            sizeOwner = null;
            skillColumnsWidthStamp = ScopeCacheStamp.Invalid;

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
        public float StatsPanelHeight(Pawn? pawn = null)
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

        // Owner: Colonists window. Key: RoleStore/current-map identity,
        // pawn-scope stamp, chip display, palette mode, selected pawn, verdict
        // preference, ordered skill-column revision, and exact row text metrics.
        // Value: desired width and height scalars. Dependencies: every key plus
        // cached chip/text measurements. Refresh: immediate on the next size read
        // after key change. Equality: exact keys reuse both scalars. Teardown:
        // ReleaseSnapshots/language invalidation resets the stamp and values.
        private ScopeCacheStamp sizeStamp = ScopeCacheStamp.Invalid;
        private RoleStore? sizeOwner;
        private int sizeMapId = -1;
        private int sizeKey = -1;
        private RowTextMetrics sizeTextMetrics;
        private float desiredWidthCache;
        private float desiredHeightCache;

        private void EnsureSizes()
        {
            RoleStore? store = RoleStore.Current;
            if (store == null) return;
            int mapId = Find.CurrentMap?.uniqueID ?? -1;
            PaletteMode paletteMode = PaletteGrouping;
            int key = ((TableDisplayKey * 31 + (int)paletteMode) * 31
                    + rosterState.SkillColumnsRevision) * 31
                + (selectedPawn?.thingIDNumber ?? -1);
            // Captions decide the minimum row height, so they change the
            // content-driven window height.
            key = (key * 31 + (PaletteVerdicts ? 1 : 0)) * 31
                + (SkillCaptions ? 1 : 0);
            ScopeCacheStamp stamp = PawnListStamp;
            RowTextMetrics textMetrics = TextMetrics();
            if (ReferenceEquals(sizeOwner, store) && sizeStamp == stamp
                && sizeMapId == mapId && sizeKey == key
                && sizeTextMetrics.ContentEquals(textMetrics)) return;
            sizeOwner = store;
            sizeMapId = mapId;
            sizeKey = key;
            sizeTextMetrics = textMetrics;
            EnsureChipSequences(store);
            IReadOnlyList<Pawn> pawns = ListedPawns();
            desiredWidthCache = ComputeDesiredWidth(store, pawns);
            desiredHeightCache = ComputeDesiredHeight(store, pawns);
            sizeStamp = PawnListStamp;
        }

        public float DesiredWidth()
        {
            if (RoleStore.Current == null || Find.CurrentMap == null) return DefaultWidth;
            EnsureSizes();
            return desiredWidthCache;
        }

        private float ComputeDesiredWidth(RoleStore store,
            IReadOnlyList<Pawn> pawns)
        {
            // Fixed left columns: portrait | gap | name | gap | copy | gap | paste | gap | [+] | gap | trailing
            float fixedLeft = PortraitSize + 6f + NameWidth + 2f + IconButton + 2f + IconButton + 8f + IconButton + 4f + 16f;
            float widestStrip = 0f;
            bool grid = RoleChipUI.UsesGrid(TableChips);
            for (int i = 0; i < pawns.Count; i++)
            {
                ColonistChipSequenceSnapshot sequence = chipSequences[pawns[i]];
                float w = chipWidthGrid.UnwrappedWidth(sequence.Count,
                    sequence.UnwrappedWidth, ChipGap, grid);
                if (w > widestStrip) widestStrip = w;
            }
            float tableWidth = fixedLeft + SkillColumnsWidth() + widestStrip;
            float skillColumnWidth = SkillColWidth;
            if (selectedPawn != null)
                skillColumnWidth = statsState.Snapshot(selectedPawn).SkillColumnWidth;
            float statsWidth = StatsPadding * 2f + PortraitDisplaySize + 12f
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

        private float ComputeDesiredHeight(RoleStore store,
            IReadOnlyList<Pawn> pawns)
        {
            float chrome = 80f;
            float paletteSection = PaletteHeight(store,
                    desiredWidthCache - PalettePanelPadding * 2f - 16f)
                + PalettePanelTopPadding + PalettePanelPadding
                - PaletteCaptionInkAllowance
                + 8f + FilterMetrics().RowHeight + 4f;
            float statsPanel = StatsPanelHeight() + StatsPanelMargin;
            float tableContent = 0f;
            float stripW = TableStripWidth(desiredWidthCache);
            RowTextMetrics rowMetrics = TextMetrics();
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                float stripH = LayoutChips(stripW, chipSequences[pawn], pawn,
                    result: null);
                tableContent += ColonistRowMetrics.Height(
                    rowMetrics.MinRowHeight, rowMetrics.BlockHeight, stripH);
            }
            return chrome + paletteSection + tableContent + statsPanel;
        }

        private float TableChipWidth(RoleChipRenderData role,
            string? abbreviation, bool pinned, bool forcedOn,
            RoleCapabilityPresentation capability, Texture2D? icon) =>
            RoleChipUI.WidthFor(role, showRemove: true, TableChips,
                abbreviation, pinned, capability.WarningSeverity, forcedOn,
                // Suitability is meaningless for blockers: no verdict slot.
                verdictSlot: ColonistVerdicts && !role.Blocker, icon: icon);

        /// The roles-column width used by both desired-height measurement and
        /// live table layout.
        private float TableStripWidth(float tableWidth) => Mathf.Max(300f,
            tableWidth - 16f - 264f - SkillColumnsWidth() - 28f);

        private float LayoutChips(float stripWidth,
            ColonistChipSequenceSnapshot sequence, Pawn pawn,
            List<RoleChipLayout>? result)
        {
            float x = 0f, y = 0f;
            int line = 0;
            bool grid = RoleChipUI.UsesGrid(TableChips);
            for (int i = 0; i < sequence.Count; i++)
            {
                ColonistChipSourceSnapshot source = sequence.ChipAt(i);
                float w = chipWidthGrid.WidthFor(source.Width, grid);
                if (x + w > stripWidth && x > 0f)
                {
                    line++;
                    x = 0f;
                    y += RoleChipUI.Height + ChipGap;
                }
                if (result != null)
                {
                    result.Add(new RoleChipLayout(source.RenderData,
                        new Rect(x, y, w, RoleChipUI.Height), line,
                        source.Capability, source.GlobalEnabled, source.State,
                        source.Pinned, source.Suppressed,
                        source.Abbreviation, source.Icon,
                        RoleTip(source.RenderData.RoleId,
                            RoleTipContext.AssignmentChip, pawn),
                        source.PinToggleLabel, source.Verdict));
                }
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

            IReadOnlyList<Pawn> pawns = ListedPawns();
            if (pendingSelectFromGame)
            {
                pendingSelectFromGame = false;
                List<Pawn> gameSelection = Find.Selector.SelectedPawns;
                for (int i = 0; i < gameSelection.Count; i++)
                    if (ContainsPawn(pawns, gameSelection[i]))
                    {
                        selectedPawn = gameSelection[i];
                        break;
                    }
            }
            if (selectedPawn == null
                || !ContainsPawn(pawns, selectedPawn))
                selectedPawn = pawns.Count > 0 ? pawns[0] : null;

            float statsPanelH = StatsPanelHeight(selectedPawn);
            float tableBottom = rect.yMax - statsPanelH - StatsPanelMargin;
            float paletteH = PaletteHeight(store,
                rect.width - PalettePanelPadding * 2f - 16f);
            float palettePanelH = paletteH + PalettePanelTopPadding
                + PalettePanelPadding - PaletteCaptionInkAllowance;
            FilterControlMetrics filterMetrics = FilterMetrics();
            float filterTop = rect.y + palettePanelH + 8f;
            float tableTop = filterTop + filterMetrics.RowHeight + 4f;

            DrawPalettePanel(new Rect(rect.x, rect.y, rect.width,
                palettePanelH), store);

            DrawFilterRow(new Rect(rect.x, filterTop, rect.width,
                filterMetrics.RowHeight), store, filterMetrics);
            DrawPawnTable(new Rect(rect.x, tableTop, rect.width,
                tableBottom - tableTop), store);
            DrawStatsPanel(new Rect(rect.x, tableBottom + StatsPanelMargin, rect.width, statsPanelH), store);

            RoleChipUI.DrawDragGhost();
            RoleDrag.ResolveMouseUp();
        }

        private static bool ContainsPawn(IReadOnlyList<Pawn> pawns,
            Pawn target)
        {
            for (int i = 0; i < pawns.Count; i++)
                if (ReferenceEquals(pawns[i], target)) return true;
            return false;
        }

        // ----- Palette -----

        private readonly struct ColonistsScopeMenuOption
        {
            internal ColonistsScopeMenuOption(ScopeKind kind,
                string? locationId, string label, bool isShip,
                string? tooltip)
            {
                Kind = kind;
                LocationId = locationId;
                Label = label;
                IsShip = isShip;
                Tooltip = tooltip;
            }

            internal ScopeKind Kind { get; }
            internal string? LocationId { get; }
            internal string Label { get; }
            internal bool IsShip { get; }
            internal string? Tooltip { get; }

            internal ScopeOption ToScopeOption() => new ScopeOption
            {
                Kind = Kind,
                LocationId = LocationId,
                Label = Label,
                IsShip = IsShip,
            };

            internal bool ContentEquals(ColonistsScopeMenuOption other) =>
                Kind == other.Kind && IsShip == other.IsShip
                && string.Equals(LocationId, other.LocationId,
                    System.StringComparison.Ordinal)
                && string.Equals(Label, other.Label,
                    System.StringComparison.Ordinal)
                && string.Equals(Tooltip, other.Tooltip,
                    System.StringComparison.Ordinal);
        }

        private sealed class ColonistsChromeSnapshot
        {
            private readonly List<ColonistsScopeMenuOption> scopeOptions;

            internal ColonistsChromeSnapshot(
                ColonistsRosterCatalogSnapshot catalog,
                string searchCaption, string roleFilterCaption,
                string jobFilterCaption, string scopeCaption,
                string groupCaption, string skillColumnsCaption,
                string roleDisplayCaption, string noFilterMatchesLabel,
                string allRolesLabel,
                string roleFilterLabel, string anyJobLabel,
                string jobFilterLabel, string jobFilterShown,
                string scopeLabel, float scopeWidth,
                List<ColonistsScopeMenuOption> scopeOptions,
                bool hasSettings, string displayLabel,
                string normalChipsLabel, string compactChipsLabel,
                string iconsChipsLabel, string compactGridChipsLabel,
                string iconsGridChipsLabel, string skillsLabel,
                string groupKey, string groupLabel)
            {
                Catalog = catalog;
                SearchCaption = searchCaption;
                RoleFilterCaption = roleFilterCaption;
                JobFilterCaption = jobFilterCaption;
                ScopeCaption = scopeCaption;
                GroupCaption = groupCaption;
                SkillColumnsCaption = skillColumnsCaption;
                RoleDisplayCaption = roleDisplayCaption;
                NoFilterMatchesLabel = noFilterMatchesLabel;
                AllRolesLabel = allRolesLabel;
                RoleFilterLabel = roleFilterLabel;
                AnyJobLabel = anyJobLabel;
                JobFilterLabel = jobFilterLabel;
                JobFilterShown = jobFilterShown;
                ScopeLabel = scopeLabel;
                ScopeWidth = scopeWidth;
                this.scopeOptions = scopeOptions;
                HasSettings = hasSettings;
                DisplayLabel = displayLabel;
                NormalChipsLabel = normalChipsLabel;
                CompactChipsLabel = compactChipsLabel;
                IconsChipsLabel = iconsChipsLabel;
                CompactGridChipsLabel = compactGridChipsLabel;
                IconsGridChipsLabel = iconsGridChipsLabel;
                SkillsLabel = skillsLabel;
                GroupKey = groupKey;
                GroupLabel = groupLabel;
            }

            internal ColonistsRosterCatalogSnapshot Catalog { get; }
            internal string SearchCaption { get; }
            internal string RoleFilterCaption { get; }
            internal string JobFilterCaption { get; }
            internal string ScopeCaption { get; }
            internal string GroupCaption { get; }
            internal string SkillColumnsCaption { get; }
            internal string RoleDisplayCaption { get; }
            internal string NoFilterMatchesLabel { get; }
            internal string AllRolesLabel { get; }
            internal string RoleFilterLabel { get; }
            internal string AnyJobLabel { get; }
            internal string JobFilterLabel { get; }
            internal string JobFilterShown { get; }
            internal string ScopeLabel { get; }
            internal float ScopeWidth { get; }
            internal int ScopeOptionCount => scopeOptions.Count;
            internal ColonistsScopeMenuOption ScopeOptionAt(int index) =>
                scopeOptions[index];
            internal bool HasSettings { get; }
            internal string DisplayLabel { get; }
            internal string NormalChipsLabel { get; }
            internal string CompactChipsLabel { get; }
            internal string IconsChipsLabel { get; }
            internal string CompactGridChipsLabel { get; }
            internal string IconsGridChipsLabel { get; }
            internal string SkillsLabel { get; }
            internal string GroupKey { get; }
            internal string GroupLabel { get; }

            internal bool ContentEquals(ColonistsChromeSnapshot other)
            {
                if (other == null
                    || !ReferenceEquals(Catalog, other.Catalog)
                    || ScopeWidth != other.ScopeWidth
                    || HasSettings != other.HasSettings
                    || scopeOptions.Count != other.scopeOptions.Count
                    || !Same(SearchCaption, other.SearchCaption)
                    || !Same(RoleFilterCaption, other.RoleFilterCaption)
                    || !Same(JobFilterCaption, other.JobFilterCaption)
                    || !Same(ScopeCaption, other.ScopeCaption)
                    || !Same(GroupCaption, other.GroupCaption)
                    || !Same(SkillColumnsCaption,
                        other.SkillColumnsCaption)
                    || !Same(RoleDisplayCaption, other.RoleDisplayCaption)
                    || !Same(NoFilterMatchesLabel,
                        other.NoFilterMatchesLabel)
                    || !Same(AllRolesLabel, other.AllRolesLabel)
                    || !Same(RoleFilterLabel, other.RoleFilterLabel)
                    || !Same(AnyJobLabel, other.AnyJobLabel)
                    || !Same(JobFilterLabel, other.JobFilterLabel)
                    || !Same(JobFilterShown, other.JobFilterShown)
                    || !Same(ScopeLabel, other.ScopeLabel)
                    || !Same(DisplayLabel, other.DisplayLabel)
                    || !Same(NormalChipsLabel, other.NormalChipsLabel)
                    || !Same(CompactChipsLabel, other.CompactChipsLabel)
                    || !Same(IconsChipsLabel, other.IconsChipsLabel)
                    || !Same(CompactGridChipsLabel,
                        other.CompactGridChipsLabel)
                    || !Same(IconsGridChipsLabel,
                        other.IconsGridChipsLabel)
                    || !Same(SkillsLabel, other.SkillsLabel)
                    || !Same(GroupKey, other.GroupKey)
                    || !Same(GroupLabel, other.GroupLabel)) return false;
                for (int i = 0; i < scopeOptions.Count; i++)
                    if (!scopeOptions[i].ContentEquals(
                            other.scopeOptions[i])) return false;
                return true;
            }

            private static bool Same(string left, string right) =>
                string.Equals(left, right,
                    System.StringComparison.Ordinal);
        }

        // Owner: Colonists window. Key: RoleStore/catalog identity, selected
        // role/job/scope, pawn-scope/location/map revisions,
        // skill-column revision, chip-display/group preferences,
        // language/definition revisions, and settings availability. Value:
        // immutable detached palette/filter/table chrome labels, measurements,
        // and scope-menu command data. Dependencies: exactly the key plus
        // translated fixed labels and detached catalog labels. Refresh:
        // immediate on the next chrome read after a key change. Equality: an
        // exact equal rebuild preserves snapshot identity. Teardown: Reset,
        // language invalidation, and ReleaseSnapshots drop all source refs.
        private ColonistsChromeSnapshot? chromeSnapshot;
        private RoleStore? chromeOwner;
        private ColonistsRosterCatalogSnapshot? chromeCatalog;
        private int chromeRoleFilterId = int.MinValue;
        private string? chromeJobFilter;
        private ScopeKind chromeScopeKind;
        private string? chromeScopeLocationId;
        private int chromePawnListRevision = -1;
        private int chromeLocationRevision = -1;
        private int chromeMapId = -1;
        private int chromeSkillColumnsRevision = -1;
        private ChipDisplay chromeChipDisplay;
        private string? chromeGroupKey;
        private int chromeLanguageRevision = -1;
        private int chromeDefinitionRevision = -1;
        private bool chromeHasSettings;

        private void InvalidateChromeSnapshot()
        {
            chromeSnapshot = null;
            chromeOwner = null;
            chromeCatalog = null;
            chromeRoleFilterId = int.MinValue;
            chromeJobFilter = null;
            chromeScopeLocationId = null;
            chromePawnListRevision = -1;
            chromeLocationRevision = -1;
            chromeMapId = -1;
            chromeSkillColumnsRevision = -1;
            chromeGroupKey = null;
            chromeLanguageRevision = -1;
            chromeDefinitionRevision = -1;
            chromeHasSettings = false;
        }

        private ColonistsChromeSnapshot ChromeSnapshot(RoleStore store)
        {
            ColonistsRosterCatalogSnapshot catalog = rosterState.Catalog(store);
            int roleFilterId = rosterState.RoleFilterId;
            string? jobFilter = rosterState.JobFilterDefName;
            ScopeOption? scope = rosterState.Scope;
            ScopeKind scopeKind = scope?.Kind ?? ScopeKind.CurrentLocation;
            string? scopeLocationId = scope?.LocationId;
            int pawnListRevision = PawnListRevision;
            int locationRevision = ColonyScope.LocationRevision;
            int mapId = Find.CurrentMap?.uniqueID ?? -1;
            int skillColumnsRevision = rosterState.SkillColumnsRevision;
            ChipDisplay chipDisplay = TableChips;
            string groupKey = profile.GetGroupBy();
            int languageRevision = LanguageChangeCoordinator.Revision;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            bool hasSettings = WorkRolesMod.Settings != null;
            if (chromeSnapshot != null
                && ReferenceEquals(chromeOwner, store)
                && ReferenceEquals(chromeCatalog, catalog)
                && chromeRoleFilterId == roleFilterId
                && string.Equals(chromeJobFilter, jobFilter,
                    System.StringComparison.Ordinal)
                && chromeScopeKind == scopeKind
                && string.Equals(chromeScopeLocationId, scopeLocationId,
                    System.StringComparison.Ordinal)
                && chromePawnListRevision == pawnListRevision
                && chromeLocationRevision == locationRevision
                && chromeMapId == mapId
                && chromeSkillColumnsRevision == skillColumnsRevision
                && chromeChipDisplay == chipDisplay
                && string.Equals(chromeGroupKey, groupKey,
                    System.StringComparison.Ordinal)
                && chromeLanguageRevision == languageRevision
                && chromeDefinitionRevision == definitionRevision
                && chromeHasSettings == hasSettings)
                return chromeSnapshot;

            rosterState.ValidateRoleFilter(catalog);
            roleFilterId = rosterState.RoleFilterId;
            GameFont oldFont = Text.Font;
            ColonistsChromeSnapshot rebuilt;
            try
            {
                Text.Font = GameFont.Small;
                string allRoles = "WR_FilterAllRoles".Translate().ToString();
                string anyJob = "WR_FilterAnyJob".Translate().ToString();
                string roleLabel = roleFilterId == -1
                    ? allRoles
                    : catalog.RoleLabelOrNull(roleFilterId) ?? allRoles;
                string fullJobLabel = jobFilter == null
                    ? anyJob : catalog.JobLabelOrNull(jobFilter) ?? anyJob;
                string shownJobLabel = fullJobLabel.Truncate(115f);

                IReadOnlyList<ScopeOption> liveScopeOptions =
                    rosterState.ScopeOptions;
                scope = rosterState.Scope;
                string currentLocationId = ColonyScope.CurrentLocationId();
                string scopeLabel = ColonyScope.LabelOf(scope!); // seeded by the ScopeOptions read above
                float scopeWidth = Mathf.Max(135f,
                    WrText.FitWidth(scopeLabel) + 20f);
                string? shipTip = null;
                var scopeOptions = new List<ColonistsScopeMenuOption>(
                    liveScopeOptions.Count);
                for (int i = 0; i < liveScopeOptions.Count; i++)
                {
                    ScopeOption option = liveScopeOptions[i];
                    if (option.Kind == ScopeKind.Location
                        && option.LocationId == currentLocationId)
                        continue;
                    if (option.IsShip && shipTip == null)
                        shipTip = "WR_ShipTip".Translate().ToString();
                    scopeOptions.Add(new ColonistsScopeMenuOption(
                        option.Kind, option.LocationId,
                        ColonyScope.LabelOf(option), option.IsShip,
                        option.IsShip ? shipTip : null));
                }

                string checkNormal = (chipDisplay == ChipDisplay.Normal
                    ? "✓ " : "") + "WR_ChipsNormal".Translate();
                string checkCompact = (chipDisplay == ChipDisplay.Compact
                    ? "✓ " : "") + "WR_ChipsCompact".Translate();
                string checkIcons = (chipDisplay == ChipDisplay.Icons
                    ? "✓ " : "") + "WR_ChipsIcons".Translate();
                string checkCompactGrid =
                    (chipDisplay == ChipDisplay.CompactGrid ? "✓ " : "")
                    + "WR_ChipsCompactGrid".Translate();
                string checkIconsGrid =
                    (chipDisplay == ChipDisplay.IconsGrid ? "✓ " : "")
                    + "WR_ChipsIconsGrid".Translate();
                int skillCount = rosterState.SkillColumns.Count;
                string skillsLabel = skillCount == 0
                    ? "WR_SkillsButton".Translate().ToString()
                    : "WR_SkillsButtonCount".Translate(skillCount,
                        ColonistsRosterState.MaxSkillColumns).ToString();
                string groupLabel = catalog.GroupLabelOrNull(groupKey)
                    ?? groupKey;
                rebuilt = new ColonistsChromeSnapshot(catalog,
                    "WR_ColonistNameCaption".Translate().ToString(),
                    "WR_AssignedRoleCaption".Translate().ToString(),
                    "WR_CoveredJobCaption".Translate().ToString(),
                    "WR_LocationCaption".Translate().ToString(),
                    "WR_GroupByCaption".Translate().ToString(),
                    "WR_SkillColumnsCaption".Translate().ToString(),
                    "WR_RoleDisplayCaption".Translate().ToString(),
                    "WR_NoFilterMatches".Translate().ToString(), allRoles,
                    roleLabel, anyJob, fullJobLabel, shownJobLabel,
                    scopeLabel, scopeWidth, scopeOptions,
                    hasSettings,
                    "WR_DisplayButton".Translate().ToString(),
                    checkNormal, checkCompact, checkIcons, checkCompactGrid,
                    checkIconsGrid, skillsLabel, groupKey, groupLabel);
            }
            finally
            {
                Text.Font = oldFont;
            }

            if (!ReferenceEquals(chromeOwner, store)
                || chromeSnapshot == null
                || !chromeSnapshot.ContentEquals(rebuilt))
                chromeSnapshot = rebuilt;
            chromeOwner = store;
            chromeCatalog = catalog;
            chromeRoleFilterId = roleFilterId;
            chromeJobFilter = jobFilter;
            scope = rosterState.Scope;
            chromeScopeKind = scope?.Kind ?? ScopeKind.CurrentLocation;
            chromeScopeLocationId = scope?.LocationId;
            chromePawnListRevision = PawnListRevision;
            chromeLocationRevision = ColonyScope.LocationRevision;
            chromeMapId = Find.CurrentMap?.uniqueID ?? -1;
            chromeSkillColumnsRevision = rosterState.SkillColumnsRevision;
            chromeChipDisplay = chipDisplay;
            chromeGroupKey = groupKey;
            chromeLanguageRevision = languageRevision;
            chromeDefinitionRevision = definitionRevision;
            chromeHasSettings = hasSettings;
            return chromeSnapshot;
        }

        private readonly struct PaletteChipSnapshot
        {
            internal PaletteChipSnapshot(RoleChipRenderData chip, bool enabled,
                Rect rect, StructuredTip? tooltip)
            {
                Chip = chip;
                Enabled = enabled;
                Rect = rect;
                Tooltip = tooltip;
            }

            internal RoleChipRenderData Chip { get; }
            internal bool Enabled { get; }
            internal Rect Rect { get; }
            internal StructuredTip? Tooltip { get; }

            internal bool ContentEquals(PaletteChipSnapshot other) =>
                Enabled == other.Enabled && Chip.ContentEquals(other.Chip)
                && Rect.x == other.Rect.x && Rect.y == other.Rect.y
                && Rect.width == other.Rect.width
                && Rect.height == other.Rect.height
                && (ReferenceEquals(Tooltip, other.Tooltip)
                    || (Tooltip != null
                        && Tooltip.ContentEquals(other.Tooltip!))); // ContentEquals is null-safe
        }

        private readonly struct PaletteLabelSnapshot
        {
            internal PaletteLabelSnapshot(string label, Rect rect)
            {
                Label = label;
                Rect = rect;
            }

            internal string Label { get; }
            internal Rect Rect { get; }

            internal bool ContentEquals(PaletteLabelSnapshot other) =>
                string.Equals(Label, other.Label,
                    System.StringComparison.Ordinal)
                && Rect.x == other.Rect.x && Rect.y == other.Rect.y
                && Rect.width == other.Rect.width
                && Rect.height == other.Rect.height;
        }

        private sealed class PaletteLayoutSnapshot
        {
            private readonly List<PaletteChipSnapshot> chips;
            private readonly List<PaletteLabelSnapshot> labels;

            internal PaletteLayoutSnapshot(
                ColonistsRosterCatalogSnapshot catalog,
                List<PaletteChipSnapshot> chips,
                List<PaletteLabelSnapshot> labels, float contentHeight)
            {
                Catalog = catalog.ChipCatalog;
                this.chips = chips;
                this.labels = labels;
                ContentHeight = contentHeight;
            }

            internal ColonistChipCatalogSnapshot Catalog { get; }
            internal float ContentHeight { get; }
            internal int ChipCount => chips.Count;
            internal int LabelCount => labels.Count;
            internal PaletteChipSnapshot ChipAt(int index) => chips[index];
            internal PaletteLabelSnapshot LabelAt(int index) => labels[index];

            internal bool TryGetCatalogChip(int roleId,
                out RoleChipRenderData chip) =>
                Catalog.TryGet(roleId, out chip);

            internal bool ContentEquals(PaletteLayoutSnapshot other)
            {
                if (other == null
                    || !ReferenceEquals(Catalog, other.Catalog)
                    || ContentHeight != other.ContentHeight
                    || chips.Count != other.chips.Count
                    || labels.Count != other.labels.Count) return false;
                for (int i = 0; i < chips.Count; i++)
                    if (!chips[i].ContentEquals(other.chips[i])) return false;
                for (int i = 0; i < labels.Count; i++)
                    if (!labels[i].ContentEquals(other.labels[i])) return false;
                return true;
            }
        }

        private sealed class PaletteVerdictSnapshot
        {
            private readonly List<RoleChipVerdict> verdicts;

            internal PaletteVerdictSnapshot(List<RoleChipVerdict> verdicts)
            {
                this.verdicts = verdicts;
            }

            internal int Count => verdicts.Count;
            internal RoleChipVerdict VerdictAt(int index) => verdicts[index];

            internal bool ContentEquals(PaletteVerdictSnapshot other)
            {
                if (other == null || verdicts.Count != other.verdicts.Count)
                    return false;
                for (int i = 0; i < verdicts.Count; i++)
                {
                    RoleChipVerdict left = verdicts[i];
                    RoleChipVerdict right = other.verdicts[i];
                    if (left.Shown != right.Shown
                        || left.Bottom.r != right.Bottom.r
                        || left.Bottom.g != right.Bottom.g
                        || left.Bottom.b != right.Bottom.b
                        || left.Bottom.a != right.Bottom.a
                        || left.Top.r != right.Top.r
                        || left.Top.g != right.Top.g
                        || left.Top.b != right.Top.b
                        || left.Top.a != right.Top.a) return false;
                }
                return true;
            }
        }

        private sealed class PaletteTipSnapshot
        {
            internal static readonly PaletteTipSnapshot Empty =
                new PaletteTipSnapshot(
                    new Dictionary<int, StructuredTip>());

            private readonly Dictionary<int, StructuredTip> tips;

            internal PaletteTipSnapshot(Dictionary<int, StructuredTip> tips)
            {
                this.tips = tips;
            }

            internal StructuredTip? TipFor(int roleId) =>
                tips.TryGetValue(roleId, out StructuredTip tip) ? tip : null;

            internal bool ContentEquals(PaletteTipSnapshot other)
            {
                if (other == null || tips.Count != other.tips.Count)
                    return false;
                foreach (KeyValuePair<int, StructuredTip> pair in tips)
                    if (!other.tips.TryGetValue(pair.Key,
                            out StructuredTip otherTip)
                        || !pair.Value.ContentEquals(otherTip)) return false;
                return true;
            }
        }

        // Owner: Colonists window. Key: RoleStore identity, pawn-scope stamp,
        // language/definition revisions, and the installed external-pawn
        // generation revision. Value: immutable
        // producer-owned role-id to detached StructuredTip snapshot.
        // Dependencies: role configuration/training paths, enabled state,
        // translated labels and def descriptions, listed-pawn skill/signal
        // snapshots, and action labels. Refresh: immediate on the next palette
        // read after a key change or external generation installation. Equality:
        // exact equal tooltip contents preserve snapshot identity. Teardown:
        // Reset/ReleaseSnapshots drops the snapshot and all remembered keys.
        private PaletteTipSnapshot? paletteTips;
        private RoleStore? paletteTipOwner;
        private ScopeCacheStamp paletteTipScopeStamp = ScopeCacheStamp.Invalid;
        private int paletteTipLanguageRevision = -1;
        private int paletteTipDefinitionRevision = -1;
        private int paletteTipExternalGeneration;
        private int paletteTipBuiltExternalGeneration = -1;

        private PaletteTipSnapshot EnsurePaletteTips(RoleStore store)
        {
            ScopeCacheStamp stamp = PawnListStamp;
            int languageRevision = LanguageChangeCoordinator.Revision;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            if (paletteTips != null
                && ReferenceEquals(paletteTipOwner, store)
                && paletteTipScopeStamp == stamp
                && paletteTipLanguageRevision == languageRevision
                && paletteTipDefinitionRevision == definitionRevision
                && paletteTipBuiltExternalGeneration
                    == paletteTipExternalGeneration)
                return paletteTips;

            GameFont oldFont = Text.Font;
            PaletteTipSnapshot rebuilt;
            try
            {
                var rebuiltTips = new Dictionary<int, StructuredTip>(
                    store.roles.Count);
                for (int roleIndex = 0; roleIndex < store.roles.Count;
                        roleIndex++)
                {
                    Role role = store.roles[roleIndex];
                    rebuiltTips[role.id] = new StructuredTip(
                        $"role:{role.id}:{RoleTipContext.Palette}:-1:0",
                        BuildRoleTip(store, role,
                            RoleTipContext.Palette, null));
                }
                rebuilt = new PaletteTipSnapshot(rebuiltTips);
            }
            finally
            {
                Text.Font = oldFont;
            }
            if (!ReferenceEquals(paletteTipOwner, store)
                || paletteTips == null || !paletteTips.ContentEquals(rebuilt))
                paletteTips = rebuilt;
            paletteTipOwner = store;
            paletteTipScopeStamp = PawnListStamp;
            paletteTipLanguageRevision = languageRevision;
            paletteTipDefinitionRevision = definitionRevision;
            paletteTipBuiltExternalGeneration = paletteTipExternalGeneration;
            return paletteTips;
        }

        /// Lays out the palette line by line: each cluster stays WHOLE on one
        /// line, placed on the earliest line with room (first fit), so later
        /// clusters back-fill earlier gaps instead of opening fresh lines.
        /// Splitting across lines is the fallback for a cluster wider than a
        /// full line by itself; a split cluster repeats its Tiny label on every
        /// line and leaves its last partial line open for back-fill. Returns
        /// content height; pass null lists to measure only.
        private static float LayoutPalette(
            ColonistsRosterCatalogSnapshot catalog, PaletteMode mode,
            float rowWidth, List<PaletteChipSnapshot> chips,
            List<PaletteLabelSnapshot> labels, bool verdictSlots,
            PaletteTipSnapshot tips, TinyTextMetrics tinyMetrics)
        {
            float labelH = Mathf.Max(
                MinimumClusterLabelH, tinyMetrics.LineHeight);
            float lineH = MinimumClusterLabelH + 2f + RoleChipUI.Height;
            var cursors = new List<float>();   // per-line x cursor; index gives y
            float YOf(int line) => line * (lineH + ClusterGapY);

            int clusterCount = catalog.PaletteClusterCount(mode);
            for (int clusterIndex = 0; clusterIndex < clusterCount;
                    clusterIndex++)
            {
                ColonistPaletteClusterSnapshot cluster =
                    catalog.PaletteClusterAt(mode, clusterIndex);
                if (cluster.Count == 0) continue;
                float labelW = WrText.FitTinyWidth(cluster.Label);

                var widths = new List<float>(cluster.Count);
                float chipsW = 0f;
                for (int roleIndex = 0; roleIndex < cluster.Count; roleIndex++)
                {
                    ColonistPaletteRole role = cluster.RoleAt(roleIndex);
                    float chipW = RoleChipUI.WidthFor(role.Chip,
                        showRemove: false,
                        verdictSlot: verdictSlots && !role.Chip.Blocker);
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
                    labels.Add(new PaletteLabelSnapshot(cluster.Label,
                        new Rect(x, y, labelW, labelH)));
                    float cx = x;
                    for (int i = 0; i < widths.Count; i++)
                    {
                        ColonistPaletteRole role = cluster.RoleAt(i);
                        chips.Add(new PaletteChipSnapshot(role.Chip,
                            role.Enabled, new Rect(cx,
                                y + MinimumClusterLabelH + 2f, widths[i],
                                RoleChipUI.Height),
                            tips.TipFor(role.Chip.RoleId)));
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
                            // Commit the finished line's cursor before moving
                            // on: a 0 cursor reads as an empty line, and later
                            // clusters would back-fill on top of it.
                            cursors[line] = Mathf.Max(x - ChipGap, segStart + labelW);
                            cursors.Add(0f);
                            line = cursors.Count - 1;
                            x = 0f;
                            segmentOpen = false;
                        }
                        if (!segmentOpen)
                        {
                            labels.Add(new PaletteLabelSnapshot(cluster.Label,
                                new Rect(x, YOf(line),
                                    Mathf.Min(labelW, rowWidth - x),
                                    labelH)));
                            segStart = x;
                            segmentOpen = true;
                        }
                        ColonistPaletteRole role = cluster.RoleAt(i);
                        chips.Add(new PaletteChipSnapshot(role.Chip,
                            role.Enabled, new Rect(x,
                                YOf(line) + MinimumClusterLabelH + 2f, widths[i],
                                RoleChipUI.Height),
                            tips.TipFor(role.Chip.RoleId)));
                        x += widths[i] + ChipGap;
                    }
                    cursors[line] = Mathf.Max(x - ChipGap, segStart + labelW);
                }
            }
            return cursors.Count == 0 ? 0f
                : cursors.Count * lineH + (cursors.Count - 1) * ClusterGapY;
        }

        // Owner: Colonists window. Key: detached roster-catalog and palette-tip
        // snapshot identities, row width, palette mode, and verdict-slot
        // presence. Value: an immutable one-shot snapshot of detached
        // chip/label/tooltip render data. Dependencies: the shared catalog and
        // tooltip producer, effective Tiny metrics, cached text measurements,
        // width, mode, and slot policy.
        // Refresh: immediate on the next PaletteLayout key miss. Equality: exact
        // contents preserve snapshot identity. Teardown: ReleaseSnapshots and
        // language invalidation release the snapshot and source reference.
        private float paletteLayoutW = -1f;
        private int paletteLayoutMode = -1;
        private TinyTextMetrics paletteLayoutTextMetrics;
        private int paletteLayoutRevision;
        private ColonistsRosterCatalogSnapshot? paletteCatalog;
        private PaletteTipSnapshot? paletteLayoutTips;
        private PaletteLayoutSnapshot? paletteSnapshot;

        /// The verdict slot is reserved only while a colonist is selected, so
        /// an empty scope keeps chip labels flush like the toggle-off state.
        private bool PaletteVerdictSlots => PaletteVerdicts && selectedPawn != null;

        private PaletteLayoutSnapshot PaletteLayout(RoleStore store,
            float rowWidth)
        {
            ColonistsRosterCatalogSnapshot catalog = rosterState.Catalog(store);
            PaletteMode paletteMode = PaletteGrouping;
            PaletteTipSnapshot tips = EnsurePaletteTips(store);
            // Verdict slots ride the mode key: they widen every palette chip.
            int mode = (int)paletteMode * 2
                + (PaletteVerdictSlots ? 1 : 0);
            TinyTextMetrics tinyMetrics = TinyText.Metrics;
            if (paletteSnapshot == null
                || !ReferenceEquals(paletteCatalog, catalog)
                || !ReferenceEquals(paletteLayoutTips, tips)
                || paletteLayoutW != rowWidth || paletteLayoutMode != mode
                || paletteLayoutTextMetrics != tinyMetrics)
            {
                var chips = new List<PaletteChipSnapshot>();
                var labels = new List<PaletteLabelSnapshot>();
                GameFont oldFont = Text.Font;
                float height;
                try
                {
                    height = LayoutPalette(catalog, paletteMode, rowWidth,
                        chips, labels, PaletteVerdictSlots, tips, tinyMetrics);
                }
                finally
                {
                    Text.Font = oldFont;
                }
                var rebuilt = new PaletteLayoutSnapshot(catalog, chips,
                    labels, height);
                if (paletteSnapshot == null
                    || !paletteSnapshot.ContentEquals(rebuilt))
                {
                    paletteSnapshot = rebuilt;
                    paletteLayoutRevision++;
                }
                paletteCatalog = catalog;
                paletteLayoutTips = tips;
                paletteLayoutW = rowWidth;
                paletteLayoutMode = mode;
                paletteLayoutTextMetrics = tinyMetrics;
            }
            return paletteSnapshot;
        }

        // Owner: Colonists window. Key: palette layout revision, selected pawn,
        // and the verdict cache stamp. Value: badges parallel to paletteChips
        // (immutable structs). Dependencies: palette geometry, selection, the
        // verdict cache, and the palette toggle. Refresh: immediate on the next
        // palette draw after a key changes. Equality: equal verdict contents
        // preserve snapshot identity. Teardown: InvalidateRoleVerdicts and
        // ReleaseSnapshots release it.
        private PaletteVerdictSnapshot? paletteVerdictSnapshot;
        private Pawn? paletteVerdictPawn;
        private int paletteVerdictLayoutRevision = -1;
        private ScopeCacheStamp paletteVerdictScopeStamp = ScopeCacheStamp.Invalid;

        private PaletteVerdictSnapshot? EnsurePaletteVerdicts(
            PaletteLayoutSnapshot layout)
        {
            if (!PaletteVerdictSlots)
            {
                paletteVerdictSnapshot = null;
                paletteVerdictPawn = null;
                paletteVerdictLayoutRevision = -1;
                return null;
            }
            ScopeCacheStamp stamp = PawnListStamp;
            if (paletteVerdictLayoutRevision == paletteLayoutRevision
                && paletteVerdictPawn == selectedPawn
                && paletteVerdictScopeStamp == stamp
                && paletteVerdictSnapshot != null
                && paletteVerdictSnapshot.Count == layout.ChipCount)
                return paletteVerdictSnapshot;
            var verdictList = new List<RoleChipVerdict>(layout.ChipCount);
            Dictionary<int, (RoleChipVerdict Badge, SignalBucket Bucket)>? verdicts =
                VerdictsFor(selectedPawn!); // PaletteVerdictSlots implies a selection
            for (int i = 0; i < layout.ChipCount; i++)
            {
                PaletteChipSnapshot chip = layout.ChipAt(i);
                verdictList.Add(chip.Chip.Blocker
                    ? default : VerdictFrom(verdicts, chip.Chip.RoleId));
            }
            var rebuilt = new PaletteVerdictSnapshot(verdictList);
            if (paletteVerdictSnapshot == null
                || !paletteVerdictSnapshot.ContentEquals(rebuilt))
                paletteVerdictSnapshot = rebuilt;
            paletteVerdictLayoutRevision = paletteLayoutRevision;
            paletteVerdictPawn = selectedPawn;
            paletteVerdictScopeStamp = stamp;
            return paletteVerdictSnapshot;
        }

        private float PaletteHeight(RoleStore store, float rowWidth)
        {
            PaletteLayoutSnapshot layout = PaletteLayout(store, rowWidth);
            return Mathf.Min(layout.ContentHeight, PaletteMaxHeight);
        }

        /// The palette grouping normalized for display: the retired Hidden
        /// value from older client settings reads as Skills.
        private static PaletteMode PaletteGrouping
        {
            get
            {
                PaletteMode mode = WorkRolesMod.Settings?.paletteMode
                    ?? PaletteMode.Skills;
                return mode == PaletteMode.Hidden ? PaletteMode.Skills : mode;
            }
        }

        /// The palette sits in a dark panel; together with the stats panel it
        /// frames the table and its filter controls, which stay on the plain
        /// section background (the scroll-edge fade texture matches that).
        /// The top inset gives back the caption's ink allowance so every side
        /// shows the same visual padding.
        private void DrawPalettePanel(Rect rect, RoleStore store)
        {
            Widgets.DrawBoxSolidWithOutline(rect, WrStyle.PanelBackground,
                WrStyle.PanelOutline);
            DrawPalette(new Rect(
                rect.x + PalettePanelPadding,
                rect.y + PalettePanelTopPadding - PaletteCaptionInkAllowance,
                rect.width - PalettePanelPadding * 2f,
                rect.height - PalettePanelTopPadding - PalettePanelPadding
                    + PaletteCaptionInkAllowance), store);
        }

        private void DrawPalette(Rect rect, RoleStore store)
        {
            float rowWidth = rect.width - 16f;
            PaletteLayoutSnapshot layout = PaletteLayout(store, rowWidth);
            PaletteVerdictSnapshot? verdictSnapshot =
                EnsurePaletteVerdicts(layout);
            float contentHeight = layout.ContentHeight;

            var scrollRect = rect;
            Widgets.BeginScrollView(scrollRect, ref paletteScroll, new Rect(0f, 0f, rowWidth, contentHeight));
            try
            {
            float visibleTop = paletteScroll.y;
            float visibleBottom = visibleTop + scrollRect.height;
            bool repaint = Event.current.type == EventType.Repaint;

            GUI.color = WrStyle.CaptionText;
            for (int labelIndex = 0; labelIndex < layout.LabelCount;
                    labelIndex++)
            {
                PaletteLabelSnapshot label = layout.LabelAt(labelIndex);
                Rect labelRect = label.Rect;
                if (repaint && labelRect.yMax >= visibleTop && labelRect.y <= visibleBottom)
                    TinyText.CompactCaption(labelRect, label.Label);
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            for (int chipIndex = 0; chipIndex < layout.ChipCount; chipIndex++)
            {
                PaletteChipSnapshot role = layout.ChipAt(chipIndex);
                Rect chipRect = role.Rect;
                bool visible = chipRect.yMax >= visibleTop && chipRect.y <= visibleBottom;
                // The click closure allocates: create it only on the one pass
                // that can consume it (left mouse-down inside this chip).
                System.Action? onClick = null;
                var pressEvent = Event.current;
                if (visible && pressEvent.type == EventType.MouseDown && pressEvent.button == 0
                    && chipRect.Contains(pressEvent.mousePosition))
                {
                    int capturedId = role.Chip.RoleId;
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
                var click = RoleChipUI.Draw(chipRect, role.Chip,
                    role.Enabled ? ChipStyle.Normal : ChipStyle.Disabled,
                    showRemove: false, dragSource: null, onClick: onClick,
                    paint: repaint && visible,
                    strikes: RoleChipStrikes.GlobalOff,
                    verdict: verdictSnapshot != null
                        && chipIndex < verdictSnapshot.Count
                        ? verdictSnapshot.VerdictAt(chipIndex) : default);
                if (visible && Mouse.IsOver(chipRect))
                    StructuredTipPresenter.TipRegion(
                        chipRect, role.Tooltip!); // TipRegion null-checks
            }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        // ----- Filter row -----

        private readonly struct FilterControlMetrics
        {
            internal FilterControlMetrics(CaptionedControlRowLayout layout)
            {
                CaptionVisualHeight = layout.CaptionVisualHeight;
                CaptionAdvance = layout.CaptionAdvance;
                RowHeight = layout.RowHeight;
            }

            internal float CaptionVisualHeight { get; }
            internal float CaptionAdvance { get; }
            internal float RowHeight { get; }
        }

        // Owner: Colonists window. Key: tiny-font support and the exact active
        // caption line height. Value: immutable caption font/row geometry.
        // Dependencies: the player's tiny-font preference, language/font
        // support, and UI-scale font metrics. Refresh: immediate on the next
        // metrics read after either key changes. Equality: exact key hits reuse
        // the value. Teardown: the view owns no external resources; language
        // invalidation resets the remembered line height.
        private FilterControlMetrics filterMetrics;
        private bool filterMetricsInitialized;
        private TinyTextMetrics filterMetricsKey;

        private FilterControlMetrics FilterMetrics()
        {
            TinyTextMetrics tinyMetrics = TinyText.Metrics;
            if (filterMetricsInitialized && filterMetricsKey == tinyMetrics)
                return filterMetrics;
            filterMetricsInitialized = true;
            filterMetricsKey = tinyMetrics;
            CaptionedControlRowLayout layout =
                CaptionedControlRowLayout.Calculate(tinyMetrics.LineHeight,
                    FilterCaptionAdvance, FilterInputH, FilterCaptionGap);
            filterMetrics = new FilterControlMetrics(layout);
            return filterMetrics;
        }

        private static void DrawControlCaption(Rect rect, string text,
            FilterControlMetrics metrics)
        {
            Text.Anchor = TextAnchor.LowerLeft;
            GUI.color = WrStyle.CaptionText;
            rect.y += metrics.CaptionAdvance - metrics.CaptionVisualHeight;
            TinyText.Label(rect, text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawFilterRow(Rect rect, RoleStore store,
            FilterControlMetrics metrics)
        {
            using var guiState = new GuiStateScope(capture: true);
            ColonistsChromeSnapshot chrome = ChromeSnapshot(store);
            const float SearchW = 140f;
            const float RoleBtnW = 135f;
            float y = rect.y + metrics.CaptionAdvance + FilterCaptionGap;

            var searchRect = new Rect(rect.x, y, SearchW, FilterInputH);
            DrawControlCaption(new Rect(searchRect.x, rect.y,
                searchRect.width, metrics.CaptionVisualHeight),
                chrome.SearchCaption, metrics);
            WrTips.Key("WR_ColonistSearchTip").Region(searchRect);
            GUI.SetNextControlName(SearchControlName);
            rosterState.Search = Widgets.TextField(searchRect,
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

            float btnX = searchRect.xMax + 8f;
            var roleRect = new Rect(btnX, y, RoleBtnW, FilterInputH);
            DrawControlCaption(new Rect(roleRect.x, rect.y, roleRect.width,
                metrics.CaptionVisualHeight), chrome.RoleFilterCaption, metrics);
            WrTips.Key("WR_ColonistRoleFilterTip").Region(roleRect);
            if (Widgets.ButtonText(roleRect, chrome.RoleFilterLabel))
                OpenRoleFilterMenu(chrome);

            // Job filter: pawns whose assigned roles cover the selected job.
            float jobX = btnX + RoleBtnW + 8f;
            var jobBtnRect = new Rect(jobX, y, RoleBtnW, FilterInputH);
            DrawControlCaption(new Rect(jobBtnRect.x, rect.y,
                jobBtnRect.width, metrics.CaptionVisualHeight),
                chrome.JobFilterCaption, metrics);
            WrTips.Key("WR_ColonistJobFilterTip",
                chrome.JobFilterLabel).Region(jobBtnRect);
            if (Widgets.ButtonText(jobBtnRect, chrome.JobFilterShown))
                OpenJobFilterMenu(chrome);

            // Scope dropdown: which locations' pawns the table lists (options
            // and labels come from the chrome snapshot).
            // Long location names widen the button; RoleBtnW is only the minimum.
            float scopeX = jobX + RoleBtnW + 8f;
            float scopeW = chrome.ScopeWidth;
            var scopeRect = new Rect(scopeX, y, scopeW, FilterInputH);
            DrawControlCaption(new Rect(scopeRect.x, rect.y,
                scopeRect.width, metrics.CaptionVisualHeight), chrome.ScopeCaption,
                metrics);
            WrTips.Key("WR_ColonistScopeTip").Region(scopeRect);
            if (Widgets.ButtonText(scopeRect, chrome.ScopeLabel))
                OpenScopeMenu(chrome);

            if (rosterState.FiltersActive)
            {
                var clearRect = new Rect(scopeX + scopeW + 8f,
                    y + (FilterInputH - 18f) / 2f, 18f, 18f);
                WrTips.Key("WR_ColonistClearFiltersTip").Region(clearRect);
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                {
                    rosterState.Search = "";
                    rosterState.RoleFilterId = -1;
                    rosterState.JobFilterDefName = null;
                }
            }

            // Right cluster: grouping, Skills column picker, and the Display
            // options button. Display prefs are per-player ModSettings, never
            // world state.
            if (chrome.HasSettings)
            {
                const float DisplayBtnW = 90f;
                var displayRect = new Rect(rect.xMax - DisplayBtnW, y,
                    DisplayBtnW, FilterInputH);
                DrawControlCaption(new Rect(displayRect.x, rect.y,
                    displayRect.width, metrics.CaptionVisualHeight),
                    chrome.RoleDisplayCaption, metrics);
                WrTips.Key("WR_ColonistRoleDisplayTip").Region(displayRect);
                if (Widgets.ButtonText(displayRect, chrome.DisplayLabel))
                    OpenDisplayMenu(chrome);

                float groupRight = displayRect.x; // group button abuts Display when the skills UI is off
                if (profile.ShowSkills)
                {
                    const float SkillsBtnW = 110f;
                    var skillsRect = new Rect(displayRect.x - 8f - SkillsBtnW,
                        y, SkillsBtnW, FilterInputH);
                    DrawControlCaption(new Rect(skillsRect.x, rect.y,
                        skillsRect.width, metrics.CaptionVisualHeight),
                        chrome.SkillColumnsCaption, metrics);
                    WrTips.Key("WR_ColonistSkillColumnsTip").Region(skillsRect);
                    if (Widgets.ButtonText(skillsRect, chrome.SkillsLabel))
                        OpenSkillColumnsMenu(chrome);
                    groupRight = skillsRect.x;
                }

                // Grouping sits with the display controls: it changes how the
                // table renders, not which pawns it lists.
                const float GroupBtnW = 130f;
                var groupRect = new Rect(groupRight - 8f - GroupBtnW, y,
                    GroupBtnW, FilterInputH);
                DrawControlCaption(new Rect(groupRect.x, rect.y,
                    groupRect.width, metrics.CaptionVisualHeight),
                    chrome.GroupCaption, metrics);
                WrTips.Key("WR_ColonistGroupTip").Region(groupRect);
                if (Widgets.ButtonText(groupRect, chrome.GroupLabel))
                    OpenGroupMenu(chrome);
            }
        }

        private void OpenRoleFilterMenu(ColonistsChromeSnapshot chrome)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption(chrome.AllRolesLabel,
                    () => rosterState.RoleFilterId = -1)
            };
            ColonistsRosterCatalogSnapshot catalog = chrome.Catalog;
            for (int optionIndex = 0;
                    optionIndex < catalog.RoleOptionCount; optionIndex++)
            {
                ColonistRoleFilterOption option =
                    catalog.RoleOptionAt(optionIndex);
                int id = option.RoleId;
                options.Add(new FloatMenuOption(option.Label,
                    () => rosterState.RoleFilterId = id));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenJobFilterMenu(ColonistsChromeSnapshot chrome)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption(chrome.AnyJobLabel,
                    () => rosterState.JobFilterDefName = null),
            };
            ColonistsRosterCatalogSnapshot catalog = chrome.Catalog;
            for (int optionIndex = 0;
                    optionIndex < catalog.JobOptionCount; optionIndex++)
            {
                ColonistJobFilterOption option =
                    catalog.JobOptionAt(optionIndex);
                string defName = option.DefName;
                options.Add(new FloatMenuOption(option.Label,
                    () => rosterState.JobFilterDefName = defName));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenScopeMenu(ColonistsChromeSnapshot chrome)
        {
            var options = new List<FloatMenuOption>(chrome.ScopeOptionCount);
            for (int optionIndex = 0;
                    optionIndex < chrome.ScopeOptionCount; optionIndex++)
            {
                ColonistsScopeMenuOption option =
                    chrome.ScopeOptionAt(optionIndex);
                var item = new FloatMenuOption(option.Label,
                    () => rosterState.SelectScope(option.ToScopeOption()));
                item.tooltip = option.Tooltip;
                options.Add(item);
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenDisplayMenu(ColonistsChromeSnapshot chrome)
        {
            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
            {
                new FloatMenuOption(chrome.NormalChipsLabel,
                    () => profile.SetTableChips(ChipDisplay.Normal)),
                new FloatMenuOption(chrome.CompactChipsLabel,
                    () => profile.SetTableChips(ChipDisplay.Compact)),
                new FloatMenuOption(chrome.IconsChipsLabel,
                    () => profile.SetTableChips(ChipDisplay.Icons)),
                new FloatMenuOption(chrome.CompactGridChipsLabel,
                    () => profile.SetTableChips(ChipDisplay.CompactGrid)),
                new FloatMenuOption(chrome.IconsGridChipsLabel,
                    () => profile.SetTableChips(ChipDisplay.IconsGrid)),
            }));
        }

        private void OpenSkillColumnsMenu(ColonistsChromeSnapshot chrome)
        {
            ColonistsRosterCatalogSnapshot catalog = chrome.Catalog;
            ColonistSkillColumnsSnapshot columns = rosterState.SkillColumns;
            var options = new List<FloatMenuOption>();
            for (int optionIndex = 0;
                    optionIndex < catalog.SkillOptionCount; optionIndex++)
            {
                ColonistSkillFilterOption option =
                    catalog.SkillOptionAt(optionIndex);
                SkillDef skill = option.Skill;
                string label = (columns.Contains(skill) ? "✓ " : "")
                    + option.Label;
                options.Add(new FloatMenuOption(label,
                    () => rosterState.ToggleSkillColumn(skill)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenGroupMenu(ColonistsChromeSnapshot chrome)
        {
            ColonistsRosterCatalogSnapshot catalog = chrome.Catalog;
            var options = new List<FloatMenuOption>();
            for (int optionIndex = 0;
                    optionIndex < catalog.GroupOptionCount; optionIndex++)
            {
                ColonistGroupOption option =
                    catalog.GroupOptionAt(optionIndex);
                string groupKey = option.Key;
                options.Add(new FloatMenuOption(
                    (string.Equals(chrome.GroupKey, groupKey,
                        System.StringComparison.Ordinal) ? "✓ " : "")
                        + option.Label, () =>
                    {
                        profile.SetGroupBy(groupKey);
                        ColonyGroupsDataSource.InvalidateSnapshot();
                    }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private ChipDisplay TableChips => profile.GetTableChips();

        /// Chip-display key for row caches: mode plus the verdict toggle, since
        /// the verdict slot changes every table chip's width.
        private int TableDisplayKey =>
            ((int)TableChips << 1) | (ColonistVerdicts ? 1 : 0);

        // Trailing air so the last decorator never sits flush against the
        // neighbouring column or the chip strip.
        private const float SkillColumnPad = 8f;

        // Owner: process definition catalog. Key: SkillDef reference identity.
        // Value: immutable capitalized skill-label strings. Dependencies: the
        // definition label and language. Refresh: lazy per definition on a miss.
        // Equality: a hit preserves string identity. Teardown:
        // InvalidateSharedLanguageCaches clears every definition entry.
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

        /// Header label (localized) or the generation's widest cell content,
        /// whichever is wider, plus trailing air.
        internal float SkillColumnWidth(SkillDef skill)
        {
            Text.Font = GameFont.Small;
            return Mathf.Max(statsState.RosterCellWidth(skill),
                WrText.FitWidth(SkillHeaderLabel(skill)) + 18f) + SkillColumnPad;
        }

        // Owner: Colonists window. Key: pawn-scope stamp and skill-column
        // revision. Value: the aggregate cached column width scalar.
        // Dependencies: selected definitions, language/text measurements, and
        // roster cell-width snapshots. Refresh: immediate on the next width read.
        // Equality: exact key hits reuse the scalar without enumerating columns.
        // Teardown: reset/language/external/release invalidation resets the stamp.
        private ScopeCacheStamp skillColumnsWidthStamp =
            ScopeCacheStamp.Invalid;
        private int skillColumnsWidthRevision = -1;
        private float skillColumnsWidthCache;

        private float SkillColumnsWidth()
        {
            ScopeCacheStamp stamp = PawnListStamp;
            int revision = rosterState.SkillColumnsRevision;
            if (skillColumnsWidthStamp == stamp
                && skillColumnsWidthRevision == revision)
                return skillColumnsWidthCache;
            float w = 0f;
            ColonistSkillColumnsSnapshot columns = rosterState.SkillColumns;
            for (int i = 0; i < columns.Count; i++)
                w += SkillColumnWidth(columns.At(i));
            skillColumnsWidthStamp = stamp;
            skillColumnsWidthRevision = revision;
            skillColumnsWidthCache = w;
            return skillColumnsWidthCache;
        }

        // Owner: Colonists window. Key: role id, context, optional Pawn identity,
        // and activity plus definition revisions within the pawn-scope stamp.
        // Value: immutable
        // StructuredTip models. Dependencies: UiVersion, pawn-list revision,
        // language, definitions, role/assignment facts, pawn activity where applicable, and
        // the tip registry epoch (a cleared registry ignores older tips, so an
        // epoch-stale hit rebuilds). Refresh: lazy on a key miss; the whole
        // table clears on stamp change. Equality: exact key hits preserve tip
        // identity. Teardown: ReleaseSnapshots/language invalidation clears all
        // tips and the stamp.
        private readonly Dictionary<(int roleId, RoleTipContext context, Pawn? pawn, int activityRevision), StructuredTip> roleTipCache
            = new Dictionary<(int, RoleTipContext, Pawn?, int), StructuredTip>();
        private ScopeCacheStamp roleTipStamp = ScopeCacheStamp.Invalid;
        private int roleTipTuningRevision = -1;
        private int roleTipDefinitionRevision = -1;

        /// The one role tooltip: palette chips, tree rows and assignment chips
        /// share the content; context varies the actions and pawn facts.
        /// Callers must re-Activate on every hovered pass (the registry retires
        /// models not touched in the latest repaint generation).
        internal StructuredTip? RoleTip(Role? role, RoleTipContext context, Pawn? pawn = null)
            => role == null ? null : RoleTip(role.id, context, pawn);

        private StructuredTip? RoleTip(int roleId, RoleTipContext context,
            Pawn? pawn = null)
        {
            var store = RoleStore.Current;
            if (store == null) return null;
            ScopeCacheStamp stamp = PawnListStamp;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            // The chip tip embeds verdict buckets and promotion thresholds,
            // both functions of the shared recommendation tuning.
            if (roleTipStamp != stamp
                || roleTipTuningRevision != store.RecommendationTuningRevision
                || roleTipDefinitionRevision != definitionRevision)
                roleTipCache.Clear();
            roleTipTuningRevision = store.RecommendationTuningRevision;
            roleTipDefinitionRevision = definitionRevision;
            // The assignment tip embeds the pawn's current activity, so a job
            // transition (revision bump) must produce a fresh tip.
            int activityRevision = context == RoleTipContext.AssignmentChip && pawn != null
                ? ActivityTracker.RevisionOf(pawn) : 0;
            var key = (roleId, context, pawn, activityRevision);
            if (!roleTipCache.TryGetValue(key, out StructuredTip tip))
            {
                Role? role = store.RoleById(roleId);
                if (role == null) return null;
                int pawnId = pawn?.thingIDNumber ?? -1;
                roleTipCache[key] = tip = new StructuredTip(
                    $"role:{roleId}:{context}:{pawnId}:{activityRevision}",
                    BuildRoleTip(store, role, context, pawn));
            }
            roleTipStamp = PawnListStamp;
            return tip;
        }

        private TipModel BuildRoleTip(RoleStore store, Role role, RoleTipContext context, Pawn? pawn)
        {
            var model = new TipModel
            {
                Title = role.label.Colorize(WrStyle.MinorAccent),
            };

            // Chip tips carry the pawn dimension so the badge names which of
            // the two independent toggles (global, this colonist) is off.
            RoleAssignment? assignment = null;
            if (context == RoleTipContext.AssignmentChip && pawn != null)
            {
                store.pawnSets.TryGetValue(pawn, out var chipSet);
                assignment = chipSet?.assignments.FirstOrDefault(a => a.roleId == role.id);
            }
            var chipState = assignment?.state ?? AssignmentState.Enabled;
            bool active = RoleActivation.IsActive(role.enabled, chipState);
            string stateKey =
                chipState == AssignmentState.ForceOn
                    ? (role.enabled ? "WR_RoleTipForcedOn"
                        : "WR_RoleTipForcedOnGlobalOff")
                : active ? "WR_RoleTipEnabled"
                : chipState == AssignmentState.Enabled ? "WR_RoleTipDisabled"
                : role.enabled ? "WR_RoleTipDisabledHere"
                : "WR_RoleTipDisabledBoth";
            string stateText = stateKey.Translate().ToString()
                .Colorize(active ? RoleStateEnabled : RoleStateDisabled);
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
                model.AddSection().Text(def!.description); // null def short-circuits above

            var facts = model.AddSection();
            var skills = RecsAdapter.RelevantSkillsOf(role);
            // The chip context replaces the plain skill list with the per-skill
            // suitability sections below.
            if (context != RoleTipContext.AssignmentChip && skills.Count > 0)
                facts.Fact("WR_TipSkillsLabel".Translate(),
                    skills.Select(s => s.skillLabel.CapitalizeFirst()).ToCommaList());
            facts.Fact("WR_TipJobsLabel".Translate(), JobSummary(role));
            // Paths sharing this role at the same skill gate merge onto one
            // line (Crafter sits in several paths at an identical band).
            string? ownBand = null;
            var trainingBands = new List<(string Band, List<string> Owners)>();
            foreach (var owner in store.roles)
            {
                int idx = owner.trainingRoleIds.IndexOf(role.id);
                if (idx < 0) continue;
                int lo = owner.trainingMins[idx], hi = owner.trainingMaxes[idx];
                string band = hi >= SkillProgressionMath.MaxLevel ? lo + "+" : lo + "-" + hi;
                if (owner.id == role.id)
                {
                    ownBand = band;
                    continue;
                }
                int at = trainingBands.FindIndex(entry => entry.Band == band);
                if (at < 0)
                    trainingBands.Add((band, new List<string> { owner.label }));
                else
                    trainingBands[at].Owners.Add(owner.label);
            }
            if (ownBand != null)
                facts.Fact("WR_TipTrainingHeader".Translate(),
                    "WR_TipTrainingRecommend".Translate(ownBand));
            foreach ((string band, List<string> owners) in trainingBands)
                facts.Fact("WR_TipTrainingHeader".Translate(),
                    "WR_TipTrainingRecommend".Translate(band) + " "
                    + "WR_TipTrainingPath".Translate(owners
                        .Select(label => label.Colorize(WrStyle.MinorAccent))
                        .ToCommaList()));

            if (context != RoleTipContext.AssignmentChip)
            {
                List<string>? fits = BestFits(skills);
                if (fits != null && fits.Count > 0)
                {
                    // Tier lines share the value column; only the first row
                    // carries the "Best fits" label.
                    var fitsSection = model.AddSection();
                    for (int i = 0; i < fits.Count; i++)
                        fitsSection.Fact(
                            i == 0 ? "WR_TipBestFitsLabel".Translate().ToString() : "",
                            fits[i]);
                }
            }

            if (context == RoleTipContext.AssignmentChip && pawn != null)
            {
                // The colonist below a separator: their live activity, the
                // role's skills one per line with level and star pair colored
                // as in the skills panel, then the pawn's verdict for the role
                // (the chip badge's bucket).
                TipSection colonist = model.AddSection();
                colonist.Fact(pawn.LabelShortCap,
                    ActivityState.ActivityPhrase(pawn, store),
                    labelColor: WrStyle.MinorAccent);
                for (int i = 0; i < skills.Count; i++)
                {
                    ColonistSkillPresentation skillFacts = statsState.PresentationFor(
                        pawn, statsState.SkillLineSnapshot(pawn, skills[i]));
                    var segments = new List<TipInlineSegment>
                    {
                        new TipInlineSegment(
                            skillFacts.Line.Label + " ", Color.white),
                        new TipInlineSegment(
                            skillFacts.Line.ValueText,
                            ColonistStatsState.SkillTextColor(
                                skillFacts.Line, skillFacts.SignalView.PassionTier)),
                    };
                    if (skillFacts.VerdictStars.Shown)
                    {
                        segments.Add(new TipInlineSegment(
                            WorkRolesTex.Star, skillFacts.VerdictStars.Bottom, gap: 4f));
                        segments.Add(new TipInlineSegment(
                            WorkRolesTex.Star, skillFacts.VerdictStars.Top, gap: 1f));
                    }
                    colonist.Inline(segments,
                        i == 0 ? "WR_TipSkillsLabel".Translate().ToString() : "");
                }
                if (skills.Count > 0
                    && TryVerdictBucket(pawn, role.id, out SignalBucket roleBucket))
                    colonist.Fact("WR_SignalVerdict".Translate(),
                        SkillSignalPresentation.BucketLabel(roleBucket),
                        SkillSignalPresentation.VerdictColor(roleBucket));
                TipSection? state = null;
                // Same stamp and shared invalidation as the tip cache, so the
                // embedded capability sentence can never outlive its inputs.
                RoleCapabilityPresentation capability =
                    roleCapabilityState.PresentationFor(
                        pawn, role, PawnListStamp, ExternalSnapshotFor(pawn));
                if (capability.Tooltip != null)
                    (state = model.AddSection()).Text(TipText.Warning(capability.Tooltip));
                if (assignment != null
                    && RoleActivation.IsActive(role.enabled, assignment.state)
                    && !RulesPass(role, pawn))
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
                        .Action("WR_ActDrag".Translate(), "WR_ActChipDrag".Translate());
                    break;
            }
            return model;
        }

        // Enabled/disabled badge tint; matches the verdict green/red family.
        private static readonly Color RoleStateEnabled = new Color(0.55f, 0.8f, 0.45f);
        private static readonly Color RoleStateDisabled = new Color(0.9f, 0.35f, 0.3f);

        /// Whole work types as "X (all jobs)", single jobs by display name;
        /// one line: capped so mega-roles don't flood the tooltip. Composites
        /// list their member roles instead of jobs.
        private static string JobSummary(Role role)
        {
            const int Cap = 3;
            var parts = new List<string>();
            if (role.composite)
            {
                var store = RoleStore.Current;
                foreach (int memberId in role.memberRoleIds)
                {
                    Role? member = store?.RoleById(memberId);
                    if (member == null) continue;
                    if (parts.Count == Cap)
                    {
                        parts.Add("WR_TipMore".Translate(
                            role.memberRoleIds.Count - Cap).ToString());
                        break;
                    }
                    parts.Add(member.label);
                }
                return parts.ToCommaList();
            }
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

        // One value-column line stays comfortably inside the tooltip without
        // wrapping (fact values size the tip to their unwrapped width).
        private const float BestFitsValueBudget = 380f;

        /// Colonists best suited to the role's skills according to the same
        /// aggregated bucket verdicts consumed by recommendations. Verdict ties
        /// are broken by skill level. At most six names, grouped into one
        /// value-column line per tier ("Exceptional: A, B"); trailing names
        /// drop into the final "+N more" until every line fits unwrapped.
        private List<string>? BestFits(List<SkillDef> skills)
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
                SkillBucketChoice? best = SkillBucketRanking.Best(
                    SignalSnapshotFor(pawn).SkillBuckets, candidates);
                if (best == null || best.Bucket < SignalBucket.Strong) continue;
                ranked.Add((pawn.LabelShortCap, best.Bucket, best.SkillLevel));
            }
            if (ranked.Count == 0) return null;
            var top = ranked
                .OrderByDescending(t => t.bucket)
                .ThenByDescending(t => t.level)
                .Take(6)
                .ToList();
            int overflow = ranked.Count - top.Count;

            var tiers = new List<(SignalBucket bucket, List<string> names)>();
            for (int i = 0; i < top.Count;)
            {
                SignalBucket bucket = top[i].bucket;
                var names = new List<string>();
                while (i < top.Count && top[i].bucket == bucket)
                    names.Add(top[i++].label);
                tiers.Add((bucket, names));
            }

            // Width fitting measures plain text against the value-column
            // budget; color markup is emit-time only.
            Text.Font = GameFont.Small;
            string Plain((SignalBucket bucket, List<string> names) tier, int more) =>
                SkillSignalPresentation.BucketLabel(tier.bucket) + ": "
                + tier.names.ToCommaList()
                + (more > 0 ? ", " + "WR_TipMore".Translate(more).ToString() : "");
            for (int i = 0; i < tiers.Count; i++)
                while (tiers[i].names.Count > 1
                       && WrText.FitWidth(Plain(tiers[i], 0)) > BestFitsValueBudget)
                {
                    tiers[i].names.RemoveAt(tiers[i].names.Count - 1);
                    overflow++;
                }
            var lastTier = tiers[tiers.Count - 1];
            while (overflow > 0 && lastTier.names.Count > 1
                   && WrText.FitWidth(Plain(lastTier, overflow)) > BestFitsValueBudget)
            {
                lastTier.names.RemoveAt(lastTier.names.Count - 1);
                overflow++;
            }

            var lines = new List<string>(tiers.Count);
            for (int i = 0; i < tiers.Count; i++)
            {
                int more = i == tiers.Count - 1 ? overflow : 0;
                string label = SkillSignalPresentation.BucketLabel(tiers[i].bucket)
                    .Colorize(SkillSignalPresentation.VerdictColor(tiers[i].bucket));
                lines.Add(label + ": " + tiers[i].names.ToCommaList()
                    + (more > 0
                        ? ", " + TipText.Dim("WR_TipMore".Translate(more).ToString())
                        : ""));
            }
            return lines;
        }

        // ----- Colonist table -----

        /// The colonist table: a fixed header row above one scroll view of
        /// group sections and variable-height pawn rows.
        private void DrawPawnTable(Rect rect, RoleStore store)
        {
            ColonistSectionsSnapshot sections = rosterState.Sections(store);

            // Chip strips wrap against the roles column; everything else is
            // fixed-width, so the row-height estimate is exact.
            EstimatedStripWidth = TableStripWidth(rect.width);
            ColonistTableHeaderLayout tableGeometry =
                ColonistTableHeaderLayout.Calculate(
                    rect.x, rect.y, rect.width);

            bool grouped = sections.Grouped;
            var outRect = new Rect(rect.x, rect.y + TableHeaderH, rect.width, rect.height - TableHeaderH);
            lastTableViewH = outRect.height;
            float viewW = tableGeometry.ScrollContentWidth;
            EnsureTableLayout(sections, grouped, EstimatedStripWidth);
            if (pendingCenterSelected)
            {
                pendingCenterSelected = false;
                CenterSelectedRow();
            }

            if (tableListedCount == 0 && rosterState.FiltersActive)
            {
                ColonistsChromeSnapshot chrome = ChromeSnapshot(store);
                TextAnchor oldAnchor = Text.Anchor;
                Color oldColor = GUI.color;
                try
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = WrStyle.DimText;
                    Widgets.Label(rect, chrome.NoFilterMatchesLabel);
                }
                finally
                {
                    Text.Anchor = oldAnchor;
                    GUI.color = oldColor;
                }
                return;
            }

            DrawTableHeader(new Rect(rect.x, rect.y,
                tableGeometry.HeaderWidth, TableHeaderH), store,
                tableGeometry);

            float totalH = tableRowLayout?.ContentExtent ?? 0f;
            Widgets.BeginScrollView(outRect, ref tableScroll,
                new Rect(0f, 0f, viewW, totalH));
            try
            {
            VariableViewportRange visible = tableRowLayout!.Calculate( // built by EnsureTableLayout above
                tableScroll.y, outRect.height);
            for (int i = visible.Start; i < visible.EndExclusive; i++)
            {
                TableLayoutRow row = tableLayoutRows[i];
                float y = tableRowLayout.OffsetOf(i);
                float height = tableRowLayout.ExtentOf(i);
                var rowRect = new Rect(0f, y, viewW, height);
                if (row.Pawn == null) DrawGroupHeader(rowRect, row);
                else DrawRow(rowRect, row.Pawn, store);
            }
            }
            finally
            {
                Widgets.EndScrollView();
            }
            DrawScrollEdgeFades(outRect, tableScroll.y, totalH);
        }

        /// 20px fade bands hinting at off-screen rows: a shadow at the edge
        /// stepping to transparent, drawn only when content extends past it.
        private static void DrawScrollEdgeFades(Rect outRect, float scrollY, float contentH)
        {
            if (contentH <= outRect.height) return;
            if (scrollY > 0f) DrawEdgeFade(outRect, top: true);
            if (scrollY + outRect.height < contentH - 1f) DrawEdgeFade(outRect, top: false);
        }

        private const float FadePx = 20f;

        private static void DrawEdgeFade(Rect outRect, bool top)
        {
            var rect = new Rect(outRect.x,
                top ? outRect.y : outRect.yMax - FadePx, outRect.width, FadePx);
            if (top) GUI.DrawTexture(rect, WorkRolesTex.ScrollEdgeFade);
            else GUI.DrawTextureWithTexCoords(rect, WorkRolesTex.ScrollEdgeFade,
                new Rect(0f, 1f, 1f, -1f));
        }

        private void EnsureTableLayout(
            ColonistSectionsSnapshot sections,
            bool grouped,
            float stripWidth)
        {
            ScopeCacheStamp stamp = PawnListStamp;
            int display = TableDisplayKey;
            // Captions raise the minimum row height whenever the caption line
            // falls back to the Small font, so row extents depend on them.
            bool captions = SkillCaptions;
            RowTextMetrics textMetrics = TextMetrics();
            if (tableRowLayout != null
                && ReferenceEquals(tableLayoutSections, sections)
                && tableLayoutStamp == stamp
                && tableLayoutStripWidth == stripWidth
                && tableLayoutDisplay == display
                && tableLayoutCaptions == captions
                && tableLayoutTextMetrics.ContentEquals(textMetrics))
                return;

            tableLayoutSections = sections;
            tableLayoutStamp = stamp;
            tableLayoutStripWidth = stripWidth;
            tableLayoutDisplay = display;
            tableLayoutCaptions = captions;
            tableLayoutTextMetrics = textMetrics;
            tableListedCount = 0;
            tableLayoutRows.Clear();

            var heights = new List<float>();
            for (int sectionIndex = 0; sectionIndex < sections.Count;
                    sectionIndex++)
            {
                ColonistSectionSnapshot section =
                    sections.SectionAt(sectionIndex);
                tableListedCount += section.Count;
                if (grouped)
                {
                    bool collapsed = rosterState.IsCollapsed(section.Key);
                    tableLayoutRows.Add(new TableLayoutRow(section, null,
                        collapsed));
                    heights.Add(GroupHeaderH);
                    if (collapsed) continue;
                }
                for (int pawnIndex = 0; pawnIndex < section.Count;
                        pawnIndex++)
                {
                    Pawn pawn = section.PawnAt(pawnIndex);
                    tableLayoutRows.Add(new TableLayoutRow(null, pawn,
                        collapsed: false));
                    heights.Add(RowHeightOf(pawn));
                }
            }
            tableRowLayout = new VariableViewportLayout(heights);
        }

        private readonly struct ColonistTableHeaderColumnSnapshot
        {
            internal ColonistTableHeaderColumnSnapshot(string defName,
                string label, float width, bool sorted)
            {
                DefName = defName;
                Label = label;
                Width = width;
                Sorted = sorted;
            }

            internal string DefName { get; }
            internal string Label { get; }
            internal float Width { get; }
            internal bool Sorted { get; }

            internal bool ContentEquals(
                ColonistTableHeaderColumnSnapshot other) =>
                Width == other.Width && Sorted == other.Sorted
                && string.Equals(DefName, other.DefName,
                    System.StringComparison.Ordinal)
                && string.Equals(Label, other.Label,
                    System.StringComparison.Ordinal);
        }

        private sealed class ColonistTableHeaderSnapshot
        {
            private readonly List<ColonistTableHeaderColumnSnapshot> columns;

            internal ColonistTableHeaderSnapshot(string colonistLabel,
                ColonistOrder order, bool hasSkillSort,
                List<ColonistTableHeaderColumnSnapshot> columns)
            {
                ColonistLabel = colonistLabel;
                Order = order;
                HasSkillSort = hasSkillSort;
                this.columns = columns;
            }

            internal string ColonistLabel { get; }
            internal ColonistOrder Order { get; }
            internal bool HasSkillSort { get; }
            internal int ColumnCount => columns.Count;
            internal ColonistTableHeaderColumnSnapshot ColumnAt(int index) =>
                columns[index];

            internal bool ContentEquals(ColonistTableHeaderSnapshot other)
            {
                if (other == null || Order != other.Order
                    || HasSkillSort != other.HasSkillSort
                    || columns.Count != other.columns.Count
                    || !string.Equals(ColonistLabel, other.ColonistLabel,
                        System.StringComparison.Ordinal)) return false;
                for (int i = 0; i < columns.Count; i++)
                    if (!columns[i].ContentEquals(other.columns[i]))
                        return false;
                return true;
            }
        }

        // Owner: Colonists window. Key: RoleStore, ordered skill-column identity,
        // sort/order preferences, language/definition revisions, and the
        // explicit external-pawn snapshot generation. Value:
        // immutable colonist/skill header labels, measured widths, sort flags,
        // and stable def-name command identifiers in a producer-owned buffer.
        // Dependencies: exactly the key plus translated labels, cached text
        // metrics, and generation-scoped roster-cell widths. Refresh: immediate
        // on the next header read after a key change; external refresh is event
        // driven. Equality: exact equal contents preserve identity; store changes
        // force republishing for ownership partitioning. Teardown: Reset and
        // ReleaseSnapshots drop the snapshot and every retained source reference.
        private ColonistTableHeaderSnapshot? tableHeaderSnapshot;
        private RoleStore? tableHeaderOwner;
        private ColonistSkillColumnsSnapshot? tableHeaderColumns;
        private string? tableHeaderSortColumn;
        private ColonistOrder tableHeaderOrder;
        private int tableHeaderLanguageRevision = -1;
        private int tableHeaderDefinitionRevision = -1;
        private int tableHeaderExternalGeneration;
        private int tableHeaderBuiltExternalGeneration = -1;

        private void ReleaseTableHeaderSnapshot()
        {
            tableHeaderSnapshot = null;
            tableHeaderOwner = null;
            tableHeaderColumns = null;
            tableHeaderSortColumn = null;
            tableHeaderLanguageRevision = -1;
            tableHeaderDefinitionRevision = -1;
            tableHeaderExternalGeneration = 0;
            tableHeaderBuiltExternalGeneration = -1;
        }

        private ColonistTableHeaderSnapshot TableHeaderSnapshot(
            RoleStore store)
        {
            ColonistSkillColumnsSnapshot columns = rosterState.SkillColumns;
            string sortColumn = profile.GetSortColumn();
            ColonistOrder order = profile.GetColonistOrder();
            int languageRevision = LanguageChangeCoordinator.Revision;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            int externalGeneration = tableHeaderExternalGeneration;
            if (tableHeaderSnapshot != null
                && ReferenceEquals(tableHeaderOwner, store)
                && ReferenceEquals(tableHeaderColumns, columns)
                && string.Equals(tableHeaderSortColumn, sortColumn,
                    System.StringComparison.Ordinal)
                && tableHeaderOrder == order
                && tableHeaderLanguageRevision == languageRevision
                && tableHeaderDefinitionRevision == definitionRevision
                && tableHeaderBuiltExternalGeneration == externalGeneration)
                return tableHeaderSnapshot;

            bool hasSkillSort = columns.IndexOfDefName(sortColumn) >= 0;
            var rebuiltColumns =
                new List<ColonistTableHeaderColumnSnapshot>(columns.Count);
            string colonistLabel;
            GameFont oldFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                string orderSuffix = order == ColonistOrder.Alphabetical
                    ? "WR_OrderSuffixAZ".Translate().ToString()
                    : "WR_OrderSuffixBar".Translate().ToString();
                colonistLabel = "WR_ColColonist".Translate() + " "
                    + orderSuffix.Colorize(
                        new Color(1f, 1f, 1f, 0.45f));
                for (int i = 0; i < columns.Count; i++)
                {
                    SkillDef skill = columns.At(i);
                    rebuiltColumns.Add(
                        new ColonistTableHeaderColumnSnapshot(
                            skill.defName, SkillHeaderLabel(skill),
                            SkillColumnWidth(skill), hasSkillSort
                                && string.Equals(sortColumn, skill.defName,
                                    System.StringComparison.Ordinal)));
                }
            }
            finally
            {
                Text.Font = oldFont;
            }

            var rebuilt = new ColonistTableHeaderSnapshot(colonistLabel,
                order, hasSkillSort, rebuiltColumns);
            if (!ReferenceEquals(tableHeaderOwner, store)
                || tableHeaderSnapshot == null
                || !tableHeaderSnapshot.ContentEquals(rebuilt))
                tableHeaderSnapshot = rebuilt;
            tableHeaderOwner = store;
            tableHeaderColumns = columns;
            tableHeaderSortColumn = sortColumn;
            tableHeaderOrder = order;
            tableHeaderLanguageRevision = languageRevision;
            tableHeaderDefinitionRevision = definitionRevision;
            tableHeaderBuiltExternalGeneration = externalGeneration;
            return tableHeaderSnapshot;
        }

        /// Fixed header: Colonist (suffix names the default order; clicking
        /// clears a skill sort, or toggles Tab order/A-Z when none is active)
        /// and skill columns (click sorts by that skill, highest first — the
        /// sorting column's label renders in the passion yellow; X removes).
        private void DrawTableHeader(Rect rect, RoleStore store,
            ColonistTableHeaderLayout layout)
        {
            ColonistTableHeaderSnapshot header = TableHeaderSnapshot(store);
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWordWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;

                // Priority grid over every listed colonist (the filtered table set).
                var gridRect = new Rect(layout.PriorityGridLeft,
                    layout.PriorityGridTop, layout.PriorityGridWidth,
                    layout.PriorityGridHeight);
                WrTips.Key("WR_ShowPriorityGridTip").Region(gridRect);
                if (Widgets.ButtonImage(gridRect, WorkRolesTex.PriorityGrid))
                {
                    List<Pawn> listed = rosterState.Sections(store).CopyPawns();
                    Find.WindowStack.Add(new Dialog_PriorityGrid(listed));
                    return;
                }

                var nameRect = new Rect(rect.x, rect.y, 264f, rect.height);
                Text.Anchor = TextAnchor.LowerLeft;
                // A-Z is an explicit colonist sort: mark the header like a sorting
                // skill column (bar order is the neutral default and stays white).
                if (!header.HasSkillSort
                    && header.Order == ColonistOrder.Alphabetical)
                    GUI.color = WrStyle.MinorAccent;
                Widgets.Label(new Rect(nameRect.x + 4f, nameRect.y, nameRect.width - 8f, nameRect.height - 2f),
                    header.ColonistLabel);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.DrawHighlightIfMouseover(nameRect);
                if (Widgets.ButtonInvisible(nameRect))
                {
                    if (header.HasSkillSort) rosterState.SetSort("");
                    else profile.SetColonistOrder(
                        header.Order == ColonistOrder.Alphabetical
                            ? ColonistOrder.ColonistBar
                            : ColonistOrder.Alphabetical);
                }

                float x = rect.x + 264f;
                for (int i = 0; i < header.ColumnCount; i++)
                {
                    ColonistTableHeaderColumnSnapshot column = header.ColumnAt(i);
                    float w = column.Width;
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
                    if (column.Sorted) GUI.color = WrStyle.MinorAccent;
                    Widgets.Label(new Rect(headerRect.x + 2f, headerRect.y, headerRect.width - 24f, headerRect.height - 2f),
                        column.Label);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.WordWrap = wrap;
                    var clickRect = new Rect(headerRect.x, headerRect.y, headerRect.width - 18f, headerRect.height);
                    Widgets.DrawHighlightIfMouseover(clickRect);
                    if (Widgets.ButtonInvisible(clickRect))
                        rosterState.SetSort(column.DefName);
                    x += w;
                }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWordWrap;
                GUI.color = oldColor;
            }
        }

        private void DrawGroupHeader(Rect rect, TableLayoutRow row)
        {
            ColonistSectionSnapshot section = row.Section!; // header rows always carry a section
            Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.06f));
            var arrowRect = new Rect(rect.x + 6f, rect.y + (rect.height - 18f) / 2f, 18f, 18f);
            GUI.DrawTexture(arrowRect, row.Collapsed
                ? TexButton.Reveal : TexButton.Collapse);
            TextAnchor oldAnchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(arrowRect.xMax + 6f, rect.y, rect.width - arrowRect.xMax - 10f, rect.height),
                    section.Title);
            }
            finally
            {
                Text.Anchor = oldAnchor;
            }
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
            Color oldColor = GUI.color;
            try
            {
                ColonistRowSnapshot publishedRow = RowSnapshotFor(
                    pawn, store, EstimatedStripWidth);

                GUI.color = new Color(1f, 1f, 1f, 0.2f);
                WrText.LineHorizontal(rect.x, rect.y, rect.width);
                GUI.color = oldColor;

                // Snapped and one device pixel taller than the row so both
                // hairline separators sit on highlighted background (the
                // unsnapped quad missed the top one by rasterization phase).
                if (pawn == selectedPawn)
                    Widgets.DrawHighlightSelected(
                        PixelBox.RowHighlightSpan(rect));
                else if (Mouse.IsOver(rect))
                {
                    Widgets.DrawHighlight(PixelBox.RowHighlightSpan(rect));
                    TargetHighlighter.Highlight(publishedRow.Pawn,
                        arrow: true, colonistBar: publishedRow.IsColonist);
                }

                float x = rect.x + 264f;
                for (int columnIndex = 0;
                        columnIndex < publishedRow.SkillCount; columnIndex++)
                {
                    ColonistSkillCellSnapshot skill =
                        publishedRow.SkillAt(columnIndex);
                    DrawSkillCell(new Rect(x, rect.y, skill.Width,
                        rect.height), publishedRow, skill);
                    x += skill.Width;
                }
                float rolesW = rect.xMax - 28f - x;
                DrawColonistCell(new Rect(rect.x, rect.y, 264f,
                    rect.height), publishedRow);
                DrawChipStrip(new Rect(x, rect.y, rolesW, rect.height),
                    publishedRow, rolesW);
                x += rolesW;
                var plusRect = new Rect(x + 2f,
                    rect.y + (rect.height - IconButton) / 2f,
                    IconButton, IconButton);
                WrTips.Key("WR_AddRoleTip").Region(plusRect);
                if (Widgets.ButtonImage(plusRect, TexButton.Plus))
                    OpenAddMenu(pawn, store);

                if (publishedRow.Downed)
                {
                    GUI.color = new Color(1f, 0f, 0f, 0.5f);
                    WrText.LineHorizontal(rect.x, rect.center.y, rect.width);
                    GUI.color = oldColor;
                }
            }
            finally
            {
                GUI.color = oldColor;
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
        private List<ColonistSectionSnapshot> NavSections()
        {
            ColonistSectionsSnapshot sections =
                rosterState.Sections(RoleStore.Current!); // HandleKey verified the store
            var result = new List<ColonistSectionSnapshot>(sections.Count);
            for (int i = 0; i < sections.Count; i++)
            {
                ColonistSectionSnapshot section = sections.SectionAt(i);
                if (!sections.Grouped
                    || !rosterState.IsCollapsed(section.Key))
                    result.Add(section);
            }
            return result;
        }

        private static List<Pawn> FlattenSections(
            List<ColonistSectionSnapshot> sections)
        {
            var result = new List<Pawn>();
            for (int sectionIndex = 0; sectionIndex < sections.Count;
                    sectionIndex++)
            {
                ColonistSectionSnapshot section = sections[sectionIndex];
                for (int pawnIndex = 0; pawnIndex < section.Count; pawnIndex++)
                    result.Add(section.PawnAt(pawnIndex));
            }
            return result;
        }

        private bool MoveSelection(int delta)
        {
            List<Pawn> order = FlattenSections(NavSections());
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
            List<Pawn> order = FlattenSections(sections);
            if (order.Count == 0) return true;
            var pool = order;
            if (!ignoreGroups && rosterState.Sections(
                    RoleStore.Current!).Grouped) // HandleKey verified the store
                for (int i = 0; i < sections.Count; i++)
                    if (sections[i].Contains(selectedPawn!)) // null selection never matches
                    {
                        pool = sections[i].CopyPawns();
                        break;
                    }
            Select(first ? pool[0] : pool[pool.Count - 1]);
            return true;
        }

        /// PgUp/PgDn: the adjacent group when grouped, else one screenful
        /// measured with the renderer's row heights.
        private bool PageMove(int dir)
        {
            var sections = NavSections();
            List<Pawn> order = FlattenSections(sections);
            if (order.Count == 0) return true;
            if (rosterState.Sections(RoleStore.Current!).Grouped // HandleKey verified the store
                && sections.Count > 1)
            {
                int gi = sections.FindIndex(s => s.Contains(selectedPawn!)); // null selection never matches
                if (gi < 0) gi = dir > 0 ? -1 : sections.Count;
                gi = Mathf.Clamp(gi + dir, 0, sections.Count - 1);
                Select(sections[gi].PawnAt(0));
                return true;
            }
            int idx = selectedPawn == null
                ? 0
                : Mathf.Max(0, order.IndexOf(selectedPawn));
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

        private float RowHeightOf(Pawn pawn)
        {
            RowTextMetrics metrics = TextMetrics();
            return ColonistRowMetrics.Height(metrics.MinRowHeight,
                metrics.BlockHeight, StripHeightFor(pawn));
        }

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

        // Open-time behavior consumed on the first table draw (Reset arms both).
        private bool pendingSelectFromGame;
        private bool pendingCenterSelected;

        /// Scrolls the selected colonist's row as close to the viewport's
        /// vertical center as the top clamp allows; the scroll view clamps
        /// the bottom on the next draw. No-op while the row is hidden
        /// (collapsed group) or the layout is not built yet.
        private void CenterSelectedRow()
        {
            if (tableRowLayout == null || selectedPawn == null) return;
            for (int i = 0; i < tableLayoutRows.Count; i++)
            {
                if (tableLayoutRows[i].Pawn != selectedPawn) continue;
                float top = tableRowLayout.OffsetOf(i);
                float height = tableRowLayout.ExtentOf(i);
                tableScroll.y = Mathf.Max(0f,
                    top - (lastTableViewH - height) / 2f);
                return;
            }
        }

        /// Scrolls the selection into view; y offsets mirror the renderer
        /// (collapsed sections contribute their header only).
        private void EnsureSelectedVisible()
        {
            ColonistSectionsSnapshot sections =
                rosterState.Sections(RoleStore.Current!); // HandleKey verified the store
            bool grouped = sections.Grouped;
            float y = 0f, top = -1f, bottom = -1f;
            for (int sectionIndex = 0; sectionIndex < sections.Count;
                    sectionIndex++)
            {
                ColonistSectionSnapshot section =
                    sections.SectionAt(sectionIndex);
                if (grouped)
                {
                    y += GroupHeaderH;
                    if (rosterState.IsCollapsed(section.Key)) continue;
                }
                for (int pawnIndex = 0; pawnIndex < section.Count;
                        pawnIndex++)
                {
                    Pawn pawn = section.PawnAt(pawnIndex);
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
        internal void DrawColonistCell(Rect rect, ColonistRowSnapshot row)
        {
            var portraitRect = new Rect(rect.x,
                rect.y + (rect.height - PortraitSize) / 2f,
                PortraitSize, PortraitSize);
            GUI.DrawTexture(portraitRect, row.Portrait);

            // The name and caption line boxes stack as one block, centered on
            // whole pixels: each box keeps its full line height (a shorter box
            // clips glyph rows at unsnapped UI scales) and the two overlap by
            // the leading their ink never reaches.
            bool hasCaption = row.CaptionCount > 0;
            RowTextMetrics boxes = row.LineBoxes;
            float blockH = hasCaption ? boxes.BlockHeight : boxes.NameBox;
            float blockTop = rect.y
                + Mathf.Floor((rect.height - blockH) / 2f)
                + ColonistRowTextOffsetY;
            var nameRect = new Rect(portraitRect.xMax + 6f, blockTop,
                NameWidth, boxes.NameBox);
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                // Slaves get the game's own sandy-yellow name color, as in
                // vanilla lists.
                GUI.color = row.NameColor;
                Widgets.Label(nameRect, row.Label);

                if (hasCaption)
                {
                    float captionX = nameRect.x;
                    float captionY = nameRect.yMax - LineBoxOverlap;
                    float captionH = boxes.CaptionBox;
                    for (int i = 0; i < row.CaptionCount; i++)
                    {
                        RowCaptionSegment segment = row.CaptionAt(i);
                        GUI.color = segment.AbbrevColor;
                        TinyText.Label(new Rect(captionX, captionY,
                            segment.AbbrevWidth + 4f, captionH),
                            segment.Abbrev);
                        captionX += segment.AbbrevWidth;
                        GUI.color = segment.LevelColor;
                        TinyText.Label(new Rect(captionX, captionY,
                            segment.LevelWidth + 4f, captionH),
                            segment.Level);
                        captionX += segment.LevelWidth;
                        if (i < row.CaptionCount - 1)
                        {
                            GUI.color = WrStyle.CaptionText;
                            TinyText.Label(new Rect(captionX, captionY,
                                segment.TrailingWidth + 4f, captionH),
                                segment.Trailing);
                            captionX += segment.TrailingWidth;
                        }
                    }
                }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }

            var selectRect = new Rect(rect.x, rect.y, portraitRect.width + 6f + NameWidth, rect.height);
            if (Mouse.IsOver(selectRect))
                PawnTip(row.Pawn).Region(selectRect);
            if (Widgets.ButtonInvisible(selectRect))
                selectedPawn = row.Pawn;

            var copyRect = new Rect(nameRect.xMax + 2f,
                rect.y + (rect.height - IconButton) / 2f,
                IconButton, IconButton);
            var pasteRect = new Rect(copyRect.xMax + 2f, copyRect.y, IconButton, IconButton);
            WrTips.Key("WR_CopyRolesTip").Region(copyRect);
            if (Widgets.ButtonImage(copyRect, TexButton.Copy))
            {
                row.CopyAssignmentsToClipboard();
                WrToast.Show(row.CopiedToast, MessageTypeDefOf.NeutralEvent);
            }
            Color pasteColor = RoleClipboard.HasContent ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            WrTips.Key("WR_PasteRolesTip").Region(pasteRect);
            if (Widgets.ButtonImage(pasteRect, TexButton.Paste, pasteColor) && RoleClipboard.HasContent)
                RoleCommands.PasteRoleSet(row.Pawn, RoleClipboard.Content);
        }

        /// Chip-strip height inputs are published separately from width-specific
        /// row geometry so sizing and drawing share one detached projection.
        internal readonly struct ColonistChipSourceSnapshot
        {
            internal ColonistChipSourceSnapshot(RoleChipRenderData renderData,
                float width, RoleCapabilityPresentation capability,
                bool globalEnabled, AssignmentState state, bool pinned,
                bool suppressed, string? abbreviation, Texture2D? icon,
                string pinToggleLabel, RoleChipVerdict verdict)
            {
                RenderData = renderData;
                Width = width;
                Capability = capability;
                GlobalEnabled = globalEnabled;
                State = state;
                Pinned = pinned;
                Suppressed = suppressed;
                Abbreviation = abbreviation;
                Icon = icon;
                PinToggleLabel = pinToggleLabel;
                Verdict = verdict;
            }

            internal RoleChipRenderData RenderData { get; }
            internal float Width { get; }
            internal RoleCapabilityPresentation Capability { get; }
            internal bool GlobalEnabled { get; }
            internal AssignmentState State { get; }
            internal bool Pinned { get; }
            internal bool Suppressed { get; }
            internal string? Abbreviation { get; }
            internal Texture2D? Icon { get; }
            internal string PinToggleLabel { get; }
            internal RoleChipVerdict Verdict { get; }

            internal bool ContentEquals(ColonistChipSourceSnapshot other)
            {
                if (!RenderData.ContentEquals(other.RenderData)
                    || Width != other.Width
                    || GlobalEnabled != other.GlobalEnabled
                    || State != other.State || Pinned != other.Pinned
                    || Suppressed != other.Suppressed
                    || !string.Equals(Abbreviation, other.Abbreviation,
                        System.StringComparison.Ordinal)
                    || !ReferenceEquals(Icon, other.Icon)
                    || !string.Equals(PinToggleLabel, other.PinToggleLabel,
                        System.StringComparison.Ordinal)) return false;
                if (Capability.WarningSeverity
                        != other.Capability.WarningSeverity
                    || !string.Equals(Capability.Tooltip,
                        other.Capability.Tooltip,
                        System.StringComparison.Ordinal)) return false;
                return Verdict.Shown == other.Verdict.Shown
                    && Verdict.Bottom.r == other.Verdict.Bottom.r
                    && Verdict.Bottom.g == other.Verdict.Bottom.g
                    && Verdict.Bottom.b == other.Verdict.Bottom.b
                    && Verdict.Bottom.a == other.Verdict.Bottom.a
                    && Verdict.Top.r == other.Verdict.Top.r
                    && Verdict.Top.g == other.Verdict.Top.g
                    && Verdict.Top.b == other.Verdict.Top.b
                    && Verdict.Top.a == other.Verdict.Top.a;
            }
        }

        internal sealed class ColonistChipSequenceSnapshot
        {
            private readonly RoleStore owner;
            private readonly List<ColonistChipSourceSnapshot> chips;
            private readonly List<RoleAssignment> assignments;

            internal ColonistChipSequenceSnapshot(RoleStore owner,
                List<ColonistChipSourceSnapshot> chips,
                List<RoleAssignment> assignments, float unwrappedWidth)
            {
                this.owner = owner;
                this.chips = chips;
                this.assignments = assignments;
                UnwrappedWidth = unwrappedWidth;
            }

            internal int Count => chips.Count;
            internal float UnwrappedWidth { get; }
            internal ColonistChipSourceSnapshot ChipAt(int index) =>
                chips[index];

            internal void IncludeWidths(ref RoleChipWidthGrid grid)
            {
                for (int i = 0; i < chips.Count; i++)
                    grid.Include(chips[i].Width);
            }

            internal void CopyAssignmentsToClipboard() =>
                RoleClipboard.CopyFromSnapshot(owner, assignments);

            internal bool ContentEquals(ColonistChipSequenceSnapshot other)
            {
                if (other == null || UnwrappedWidth != other.UnwrappedWidth
                    || chips.Count != other.chips.Count
                    || assignments.Count != other.assignments.Count)
                    return false;
                for (int i = 0; i < chips.Count; i++)
                    if (!chips[i].ContentEquals(other.chips[i])) return false;
                for (int i = 0; i < assignments.Count; i++)
                {
                    RoleAssignment left = assignments[i];
                    RoleAssignment right = other.assignments[i];
                    if (left.roleId != right.roleId || left.state != right.state
                        || left.pinned != right.pinned) return false;
                }
                return true;
            }
        }

        // Owner: Colonists window. Key: RoleStore/catalog identity, pawn-scope
        // stamp, chip-display mode, verdict preference, and recommendation tuning.
        // Value: immutable width-independent detached chip sequences per listed
        // pawn plus the aggregate widest assigned-chip width for grid modes.
        // Dependencies: assignments, role presentation and icon textures,
        // capability/rules, verdicts, external pawn facts, language, definitions,
        // and every key component.
        // Refresh: immediate as one generation on the next sequence read. Equality:
        // equal per-pawn rebuilds preserve snapshot identity. Teardown:
        // ReleaseSnapshots/language/external invalidation releases the dictionary.
        private Dictionary<Pawn, ColonistChipSequenceSnapshot> chipSequences =
            new Dictionary<Pawn, ColonistChipSequenceSnapshot>();
        private RoleStore? chipSequenceOwner;
        private ColonistsRosterCatalogSnapshot? chipSequenceCatalog;
        private ScopeCacheStamp chipSequenceStamp = ScopeCacheStamp.Invalid;
        private int chipSequenceDisplay = -1;
        private int chipSequenceTuningRevision = -1;
        private RoleChipWidthGrid chipWidthGrid;

        private void EnsureChipSequences(RoleStore store)
        {
            ScopeCacheStamp stamp = PawnListStamp;
            ColonistsRosterCatalogSnapshot catalog = rosterState.Catalog(store);
            int display = TableDisplayKey;
            int tuningRevision = store.RecommendationTuningRevision;
            if (ReferenceEquals(chipSequenceOwner, store)
                && ReferenceEquals(chipSequenceCatalog, catalog)
                && chipSequenceStamp == stamp
                && chipSequenceDisplay == display
                && chipSequenceTuningRevision == tuningRevision) return;

            IReadOnlyList<Pawn> pawns = ListedPawns();
            var rebuilt = new Dictionary<Pawn, ColonistChipSequenceSnapshot>(
                pawns.Count);
            var rebuiltWidthGrid = new RoleChipWidthGrid();
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                ColonistChipSequenceSnapshot candidate =
                    BuildChipSequence(store, catalog, pawn);
                candidate.IncludeWidths(ref rebuiltWidthGrid);
                if (ReferenceEquals(chipSequenceOwner, store)
                    && chipSequences.TryGetValue(pawn,
                        out ColonistChipSequenceSnapshot previous)
                    && previous.ContentEquals(candidate))
                    candidate = previous;
                rebuilt[pawn] = candidate;
            }
            chipSequences = rebuilt;
            chipWidthGrid = rebuiltWidthGrid;
            chipSequenceOwner = store;
            chipSequenceCatalog = catalog;
            chipSequenceStamp = stamp;
            chipSequenceDisplay = display;
            chipSequenceTuningRevision = tuningRevision;
        }

        private ColonistChipSequenceSnapshot BuildChipSequence(RoleStore store,
            ColonistsRosterCatalogSnapshot catalog, Pawn pawn)
        {
            store.pawnSets.TryGetValue(pawn, out PawnRoleSet? set);
            int capacity = set?.assignments.Count ?? 0;
            var chips = new List<ColonistChipSourceSnapshot>(capacity);
            var assignments = new List<RoleAssignment>(capacity);
            Dictionary<int, (RoleChipVerdict Badge, SignalBucket Bucket)>? verdicts =
                ColonistVerdicts ? VerdictsFor(pawn) : null;
            string pinLabel = "WR_PinAssignment".Translate().ToString();
            string unpinLabel = "WR_UnpinAssignment".Translate().ToString();
            float unwrappedWidth = 0f;
            ChipDisplay display = TableChips;
            for (int i = 0; i < capacity; i++)
            {
                RoleAssignment assignment = set!.assignments[i]; // capacity > 0 implies a set
                Role? role = store.RoleById(assignment.roleId);
                if (role == null || !catalog.TryGetChip(assignment.roleId,
                        out RoleChipRenderData renderData)) continue;
                RoleCapabilityPresentation capability =
                    roleCapabilityState.PresentationFor(pawn, role,
                        PawnListStamp, ExternalSnapshotFor(pawn));
                string? abbreviation = RoleChipUI.NeedsAbbreviation(display)
                    ? catalog.AbbreviationFor(assignment.roleId) : null;
                Texture2D? icon = RoleChipUI.UsesIcons(display)
                    ? catalog.IconFor(assignment.roleId) : null;
                bool forcedOn = assignment.state == AssignmentState.ForceOn;
                float width = TableChipWidth(renderData, abbreviation,
                    assignment.pinned, forcedOn, capability, icon);
                bool chipEnabled = RoleActivation.IsActive(role.enabled,
                    assignment.state);
                chips.Add(new ColonistChipSourceSnapshot(renderData, width,
                    capability, role.enabled, assignment.state,
                    assignment.pinned,
                    chipEnabled && !RulesPass(role, pawn), abbreviation,
                    icon, assignment.pinned ? unpinLabel : pinLabel,
                    renderData.Blocker ? default(RoleChipVerdict)
                        : VerdictFrom(verdicts, assignment.roleId)));
                assignments.Add(new RoleAssignment
                {
                    roleId = assignment.roleId,
                    state = assignment.state,
                    pinned = assignment.pinned,
                });
                unwrappedWidth += width + ChipGap;
            }
            return new ColonistChipSequenceSnapshot(store, chips,
                assignments, unwrappedWidth);
        }

        private ColonistChipSequenceSnapshot ChipSequenceFor(Pawn pawn,
            RoleStore store)
        {
            EnsureChipSequences(store);
            return chipSequences[pawn];
        }

        /// One positioned chip in a width-specific colonist row snapshot.
        internal readonly struct RoleChipLayout
        {
            internal RoleChipLayout(RoleChipRenderData renderData, Rect rect,
                int line, RoleCapabilityPresentation capability,
                bool globalEnabled, AssignmentState state, bool pinned,
                bool suppressed, string? abbreviation, Texture2D? icon,
                StructuredTip? tooltip, string pinToggleLabel,
                RoleChipVerdict verdict)
            {
                RenderData = renderData;
                Rect = rect;
                Line = line;
                Capability = capability;
                GlobalEnabled = globalEnabled;
                State = state;
                Pinned = pinned;
                Suppressed = suppressed;
                Abbreviation = abbreviation;
                Icon = icon;
                Tooltip = tooltip;
                PinToggleLabel = pinToggleLabel;
                Verdict = verdict;
            }

            internal RoleChipRenderData RenderData { get; }
            internal Rect Rect { get; }
            internal int Line { get; }
            internal RoleCapabilityPresentation Capability { get; }
            internal bool GlobalEnabled { get; }
            internal AssignmentState State { get; }
            internal bool Pinned { get; }
            internal bool Suppressed { get; }
            internal string? Abbreviation { get; }
            internal Texture2D? Icon { get; }
            internal StructuredTip? Tooltip { get; }
            internal string PinToggleLabel { get; }
            internal RoleChipVerdict Verdict { get; }
        }

        /// One caption entry under the colonist name: the skill abbreviation
        /// in the verdict color, the level in the shared skill-value color,
        /// and an optional dim trailing separator. Widths premeasured in Tiny.
        internal readonly struct RowCaptionSegment
        {
            internal RowCaptionSegment(string abbrev, float abbrevWidth,
                Color abbrevColor, string level, float levelWidth,
                Color levelColor, string trailing, float trailingWidth)
            {
                Abbrev = abbrev;
                AbbrevWidth = abbrevWidth;
                AbbrevColor = abbrevColor;
                Level = level;
                LevelWidth = levelWidth;
                LevelColor = levelColor;
                Trailing = trailing;
                TrailingWidth = trailingWidth;
            }

            internal string Abbrev { get; }
            internal float AbbrevWidth { get; }
            internal Color AbbrevColor { get; }
            internal string Level { get; }
            internal float LevelWidth { get; }
            internal Color LevelColor { get; }
            internal string Trailing { get; }
            internal float TrailingWidth { get; }
        }

        /// Detached presentation for one configured skill column in a
        /// colonist row. The immutable presentation owns the icon buffer; this
        /// projection exposes it only through bounded indexed reads.
        internal readonly struct ColonistSkillCellSnapshot
        {
            private readonly ColonistSkillPresentation? presentation;

            internal ColonistSkillCellSnapshot(float width, bool disabled,
                string valueText, Color textColor,
                ColonistSkillPresentation? presentation)
            {
                Width = width;
                Disabled = disabled;
                ValueText = valueText;
                TextColor = textColor;
                this.presentation = presentation;
            }

            internal float Width { get; }
            internal bool Disabled { get; }
            internal string ValueText { get; }
            internal Color TextColor { get; }
            internal int IconCount => presentation?.SignalIcons.Count ?? 0;
            internal Texture2D IconAt(int index) =>
                presentation!.SignalIcons[index]; // IconCount > 0 implies a presentation
            internal StructuredTip? Tooltip => presentation?.Tooltip;
        }

        internal sealed class ColonistRowSnapshot
        {
            private static readonly System.Func<RoleChipLayout, Rect>
                ChipRect = chip => chip.Rect;
            private readonly ColonistChipSequenceSnapshot sequence;
            private readonly List<RoleChipLayout> chips;
            private readonly List<RowCaptionSegment> caption;
            private readonly ColonistSkillCellSnapshot[] skills;

            internal ColonistRowSnapshot(Pawn pawn,
                Texture portrait, string label, Color nameColor,
                bool isColonist, bool downed, ChipDisplay chipDisplay,
                string copiedToast, int activityRevision, int activeRoleId,
                ColonistChipSequenceSnapshot sequence,
                List<RoleChipLayout> chips,
                float stripHeight, List<RowCaptionSegment> caption,
                RowTextMetrics lineBoxes, ColonistSkillCellSnapshot[] skills)
            {
                LineBoxes = lineBoxes;
                this.sequence = sequence;
                Pawn = pawn;
                Portrait = portrait;
                Label = label;
                NameColor = nameColor;
                IsColonist = isColonist;
                Downed = downed;
                ChipDisplay = chipDisplay;
                CopiedToast = copiedToast;
                ActivityRevision = activityRevision;
                ActiveRoleId = activeRoleId;
                this.chips = chips;
                StripHeight = stripHeight;
                this.caption = caption;
                this.skills = skills;
            }

            internal Pawn Pawn { get; }
            internal Texture Portrait { get; }
            internal string Label { get; }
            internal Color NameColor { get; }
            internal bool IsColonist { get; }
            internal bool Downed { get; }
            internal ChipDisplay ChipDisplay { get; }
            internal string CopiedToast { get; }
            internal int ActivityRevision { get; }
            internal int ActiveRoleId { get; }
            internal float StripHeight { get; }
            /// Line boxes the name and caption were laid out against.
            internal RowTextMetrics LineBoxes { get; }
            internal int ChipCount => chips.Count;
            internal RoleChipLayout ChipAt(int index) => chips[index];
            internal int CaptionCount => caption.Count;
            internal RowCaptionSegment CaptionAt(int index) => caption[index];
            internal int SkillCount => skills.Length;
            internal ColonistSkillCellSnapshot SkillAt(int index) =>
                skills[index];

            internal bool HasRole(int roleId)
            {
                for (int i = 0; i < chips.Count; i++)
                    if (chips[i].RenderData.RoleId == roleId) return true;
                return false;
            }

            internal int ChipInsertIndex(Vector2 point) =>
                RoleDrag.ChipInsertIndex(point, chips, ChipRect);

            internal void CopyAssignmentsToClipboard() =>
                sequence.CopyAssignmentsToClipboard();
        }

        // Per-surface verdict badge toggles (client-side display preferences;
        // every cache whose contents they change keys on them directly).
        private static bool ColonistVerdicts =>
            WorkRolesMod.Settings?.verdictsOnColonistChips ?? true;
        private static bool PaletteVerdicts =>
            WorkRolesMod.Settings?.verdictsInPalette ?? true;

        /// Best-skills caption under the colonist name (client-side; only the
        /// row snapshots consume it).
        private static bool SkillCaptions =>
            WorkRolesMod.Settings?.colonistSkillCaptions ?? true;

        // Owner: Colonists window. Key: pawn-scope stamp over the listed pawns.
        // Value: per-pawn roleId-to-badge maps built from the engine's BestSignal
        // aggregation; letters/colors resolved at build, immutable after publish.
        // Dependencies: role catalog and assignments (UiVersion via the stamp),
        // pawn scope, external pawn facts, language (letters). Refresh: immediate
        // on the next read after invalidation. Equality: a matching stamp reuses
        // map identity. Teardown: Reset/ReleaseSnapshots, language invalidation,
        // and external-snapshot refresh clear the table.
        private readonly Dictionary<Pawn, Dictionary<int, (RoleChipVerdict Badge, SignalBucket Bucket)>> roleVerdicts =
            new Dictionary<Pawn, Dictionary<int, (RoleChipVerdict, SignalBucket)>>();
        private ScopeCacheStamp roleVerdictStamp = ScopeCacheStamp.Invalid;

        private void InvalidateRoleVerdicts()
        {
            roleVerdicts.Clear();
            roleVerdictStamp = ScopeCacheStamp.Invalid;
            chipSequenceStamp = ScopeCacheStamp.Invalid;
            paletteVerdictSnapshot = null;
            paletteVerdictPawn = null;
            paletteVerdictLayoutRevision = -1;
        }

        private Dictionary<int, (RoleChipVerdict Badge, SignalBucket Bucket)>? VerdictsFor(Pawn pawn)
        {
            ScopeCacheStamp stamp = PawnListStamp;
            if (roleVerdictStamp != stamp)
            {
                roleVerdicts.Clear();
                roleVerdictStamp = stamp;
                var store = RoleStore.Current;
                if (store != null)
                {
                    var pawns = new List<Pawn>(ListedPawns());
                    List<Dictionary<int, SignalBucket>> suitability =
                        RoleSuitability.Verdicts(RecsAdapter.BuildColonyView(
                            store, pawns, ExternalSnapshotFor));
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        var map = new Dictionary<int, (RoleChipVerdict, SignalBucket)>(
                            suitability[i].Count);
                        foreach (var pair in suitability[i])
                            map[pair.Key] = (
                                SkillSignalPresentation.VerdictBadge(pair.Value),
                                pair.Value);
                        roleVerdicts[pawns[i]] = map;
                    }
                }
            }
            return roleVerdicts.TryGetValue(pawn, out var result) ? result : null;
        }

        private static RoleChipVerdict VerdictFrom(
            Dictionary<int, (RoleChipVerdict Badge, SignalBucket Bucket)>? verdicts, int roleId) =>
            verdicts != null && verdicts.TryGetValue(roleId, out (RoleChipVerdict Badge, SignalBucket Bucket) verdict)
                ? verdict.Badge : default;

        private bool TryVerdictBucket(Pawn pawn, int roleId, out SignalBucket bucket)
        {
            Dictionary<int, (RoleChipVerdict Badge, SignalBucket Bucket)>? verdicts = VerdictsFor(pawn);
            if (verdicts != null && verdicts.TryGetValue(roleId, out (RoleChipVerdict Badge, SignalBucket Bucket) verdict))
            {
                bucket = verdict.Bucket;
                return true;
            }
            bucket = SignalBucket.Neutral;
            return false;
        }

        // Owner: Colonists window. Key: RoleStore and Pawn reference identity
        // inside the pawn-scope stamp, floored strip width, chip-display mode,
        // tuning revision (caption ordering), definition revision, configured skill-column revision,
        // the skill-caption toggle, exact row text metrics, and pawn activity
        // revision. Value:
        // immutable ColonistRowSnapshot projections, including the resolved
        // chip display mode; producer-owned assignment/chip/skill buffers are
        // hidden behind indexed access, while game-owned portraits are stable
        // references never mutated here.
        // Dependencies: role/assignment UiVersion, pawn scope, external pawn
        // facts, activity and definition revisions, display mode, width, configured skill
        // columns, recommendation tuning, the skill-caption toggle, font
        // line boxes/tiny-font support, and
        // language. Refresh: immediate on
        // scope/key change and targeted per pawn on activity change. Equality:
        // exact keys preserve row identity.
        // Teardown: ReleaseSnapshots/language or external-snapshot invalidation
        // clears all rows and backing buffers.
        private readonly Dictionary<Pawn, ColonistRowSnapshot> chipLayouts =
            new Dictionary<Pawn, ColonistRowSnapshot>();
        private RoleStore? chipLayoutOwner;
        private ScopeCacheStamp chipLayoutStamp = ScopeCacheStamp.Invalid;
        private float chipLayoutWidth = -1f;
        private int chipLayoutDisplay = -1;
        private int chipLayoutTuningRevision = -1;
        private int chipLayoutDefinitionRevision = -1;
        private int chipLayoutSkillColumnsRevision = -1;
        private bool chipLayoutCaptions = true;
        private RowTextMetrics chipLayoutTextMetrics;

        private ColonistRowSnapshot RowSnapshotFor(Pawn pawn, RoleStore store,
            float stripWidth)
        {
            stripWidth = Mathf.Max(300f, stripWidth);
            ScopeCacheStamp stamp = PawnListStamp;
            int display = TableDisplayKey;
            ChipDisplay chipDisplay = (ChipDisplay)(display >> 1);
            int tuningRevision = store.RecommendationTuningRevision;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            int skillColumnsRevision = rosterState.SkillColumnsRevision;
            bool captions = SkillCaptions;
            RowTextMetrics textMetrics = TextMetrics();
            if (!ReferenceEquals(chipLayoutOwner, store)
                || chipLayoutStamp != stamp || chipLayoutWidth != stripWidth
                || chipLayoutDisplay != display
                || chipLayoutTuningRevision != tuningRevision
                || chipLayoutDefinitionRevision != definitionRevision
                || chipLayoutSkillColumnsRevision != skillColumnsRevision
                || chipLayoutCaptions != captions
                || !chipLayoutTextMetrics.ContentEquals(textMetrics))
            {
                chipLayouts.Clear();
                chipLayoutOwner = store;
                chipLayoutStamp = stamp;
                chipLayoutWidth = stripWidth;
                chipLayoutDisplay = display;
                chipLayoutTuningRevision = tuningRevision;
                chipLayoutDefinitionRevision = definitionRevision;
                chipLayoutSkillColumnsRevision = skillColumnsRevision;
                chipLayoutCaptions = captions;
                chipLayoutTextMetrics = textMetrics;
            }
            int activityRevision = ActivityTracker.RevisionOf(pawn);
            if (chipLayouts.TryGetValue(pawn, out ColonistRowSnapshot cached)
                && cached.ActivityRevision == activityRevision)
                return cached;
            ColonistChipSequenceSnapshot sequence =
                ChipSequenceFor(pawn, store);
            var layout = new List<RoleChipLayout>();
            float height = sequence.Count == 0
                ? RoleChipUI.Height
                : LayoutChips(chipLayoutWidth, sequence, pawn, layout);
            ActivitySnapshot activity = activityState.For(pawn);
            string label = pawn.LabelShortCap;
            ColonistStatsSnapshot stats = statsState.Snapshot(pawn);
            var entry = new ColonistRowSnapshot(pawn,
                PortraitsCache.Get(pawn,
                    new Vector2(PortraitSize, PortraitSize), Rot4.South),
                label,
                pawn.IsSlave ? PawnNameColorUtility.PawnNameColorOf(pawn) : Color.white,
                pawn.IsColonist,
                pawn.Downed,
                chipDisplay,
                "WR_CopiedRoles".Translate(label).ToString(),
                activityRevision, activity.RoleId, sequence, layout, height,
                BuildRowCaption(pawn, store, stats), textMetrics,
                BuildSkillCells(stats));
            chipLayouts[pawn] = entry;
            return entry;
        }

        private ColonistSkillCellSnapshot[] BuildSkillCells(
            ColonistStatsSnapshot stats)
        {
            ColonistSkillColumnsSnapshot columns = rosterState.SkillColumns;
            var result = new ColonistSkillCellSnapshot[columns.Count];
            for (int columnIndex = 0; columnIndex < columns.Count;
                    columnIndex++)
            {
                SkillDef skill = columns.At(columnIndex);
                ColonistSkillPresentation? presentation = null;
                for (int skillIndex = 0; skillIndex < stats.Skills.Count;
                        skillIndex++)
                {
                    ColonistSkillPresentation candidate =
                        stats.Skills[skillIndex];
                    if (ReferenceEquals(candidate.Line.Def, skill))
                    {
                        presentation = candidate;
                        break;
                    }
                }
                bool disabled = presentation == null
                    || presentation.Line.Disabled;
                result[columnIndex] = new ColonistSkillCellSnapshot(
                    SkillColumnWidth(skill), disabled,
                    disabled ? "-" : presentation!.Line.ValueText, // !disabled implies a presentation
                    disabled ? WrStyle.DisabledText
                        : ColonistStatsState.SkillTextColor(
                            presentation!.Line,
                            presentation.SignalView.PassionTier),
                    disabled ? null : presentation);
            }
            return result;
        }

        private const int CaptionMaxSkills = 4;
        private static readonly List<RowCaptionSegment> NoCaption =
            new List<RowCaptionSegment>();

        /// Whole-pixel line boxes for a colonist row's name and caption, read
        /// from the live font metrics. A box shorter than its font's line
        /// height clips glyph rows: vanilla Widgets.Label only snaps rects
        /// outward at fractional UI scales above 1, so an undersized box
        /// survives 1.25 and loses pixels at 1x.
        internal readonly struct RowTextMetrics
        {
            private RowTextMetrics(float nameBox, float captionBox,
                float blockHeight, float minRowHeight,
                TinyTextMetrics tinyMetrics)
            {
                NameBox = nameBox;
                CaptionBox = captionBox;
                BlockHeight = blockHeight;
                MinRowHeight = minRowHeight;
                TinyMetrics = tinyMetrics;
            }

            internal float NameBox { get; }
            internal float CaptionBox { get; }
            /// Name and caption stacked with the permitted line-box overlap.
            internal float BlockHeight { get; }
            internal float MinRowHeight { get; }
            private TinyTextMetrics TinyMetrics { get; }

            internal bool ContentEquals(RowTextMetrics other) =>
                NameBox == other.NameBox
                && CaptionBox == other.CaptionBox
                && BlockHeight == other.BlockHeight
                && MinRowHeight == other.MinRowHeight
                && TinyMetrics == other.TinyMetrics;

            /// Tiny falls back to Small whenever tiny text is unsupported (player
            /// preference, language, Steam Deck), which is the one case where the
            /// stacked pair outgrows the standard row.
            internal static RowTextMetrics Build(bool captions,
                float smallLineHeight, TinyTextMetrics tinyMetrics)
            {
                float nameBox = Mathf.Ceil(smallLineHeight);
                float captionBox = tinyMetrics.LineHeight;
                float block = captions
                    ? nameBox + captionBox - LineBoxOverlap : nameBox;
                return new RowTextMetrics(nameBox, captionBox, block,
                    Mathf.Max(RowHeight, block), tinyMetrics);
            }
        }

        // Owner: Colonists window. Key: caption toggle, tiny-font support, and
        // the exact Small/caption line heights. Value: immutable row line-box
        // metrics. Dependencies: only those consumed font metrics/preferences.
        // Refresh: immediate on the next read after a key change. Equality:
        // exact key hits reuse the value. Teardown: language invalidation clears
        // the remembered line heights.
        private RowTextMetrics rowTextMetrics;
        private bool rowTextMetricsCaptions;
        private TinyTextMetrics rowTextMetricsTiny;
        private float rowTextMetricsSmallLineHeight = -1f;
        private float rowTextMetricsCaptionLineHeight = -1f;

        private RowTextMetrics TextMetrics()
        {
            bool captions = SkillCaptions;
            TinyTextMetrics tinyMetrics = TinyText.Metrics;
            float smallLineHeight = Text.LineHeightOf(GameFont.Small);
            float captionLineHeight = tinyMetrics.LineHeight;
            if (rowTextMetricsCaptions == captions
                && rowTextMetricsTiny == tinyMetrics
                && rowTextMetricsSmallLineHeight == smallLineHeight
                && rowTextMetricsCaptionLineHeight == captionLineHeight)
                return rowTextMetrics;
            rowTextMetricsCaptions = captions;
            rowTextMetricsTiny = tinyMetrics;
            rowTextMetricsSmallLineHeight = smallLineHeight;
            rowTextMetricsCaptionLineHeight = captionLineHeight;
            rowTextMetrics = RowTextMetrics.Build(
                captions, smallLineHeight, tinyMetrics);
            return rowTextMetrics;
        }

        /// The pawn's best skills at Strong or better, ordered by the engine's
        /// champion score (level and verdict together): abbreviation in the
        /// verdict color, level in the shared skill-value color, truncated to
        /// what fits the name column. Shooting and Melee are combat noise
        /// here, not work skills, and stay out.
        private List<RowCaptionSegment> BuildRowCaption(Pawn pawn,
            RoleStore store, ColonistStatsSnapshot stats)
        {
            if (!SkillCaptions) return NoCaption;
            var result = new List<RowCaptionSegment>();
            var candidates = new List<SkillBucketCandidate>(stats.Skills.Count);
            for (int i = 0; i < stats.Skills.Count; i++)
            {
                SkillLine line = stats.Skills[i].Line;
                if (line.Disabled || line.Def == null) continue;
                if (line.Def == SkillDefOf.Shooting || line.Def == SkillDefOf.Melee)
                    continue;
                candidates.Add(new SkillBucketCandidate(line.Def.defName, line.Level));
            }
            List<SkillBucketChoice> top = SkillBucketRanking.Top(
                SignalSnapshotFor(pawn).SkillBuckets, candidates,
                SignalBucket.Strong, CaptionMaxSkills,
                store.recommendationTuning ?? RecommendationsTuningOptions.Default);
            if (top.Count == 0) return result;

            GameFont oldFont = Text.Font;
            try
            {
                float separatorWidth = WrText.FitTinyWidth(", ");
                float available = NameWidth - 2f;
                float used = 0f;
                foreach (SkillBucketChoice choice in top)
                {
                    ColonistSkillPresentation? presentation = null;
                    for (int i = 0; i < stats.Skills.Count; i++)
                        if (stats.Skills[i].Line.Def?.defName
                                == choice.SkillDefName)
                        {
                            presentation = stats.Skills[i];
                            break;
                        }
                    string? label = presentation?.Line.Label;
                    if (string.IsNullOrEmpty(label)) continue;
                    string abbrev = (label!.Length > 3 // IsNullOrEmpty guard above (no NotNullWhen on net472)
                        ? label.Substring(0, 3) : label).ToLowerInvariant()
                        + " ";
                    string level = choice.SkillLevel.ToString();
                    float abbrevWidth = WrText.FitTinyWidth(abbrev);
                    float levelWidth = WrText.FitTinyWidth(level);
                    // A new segment needs the previous segment's separator too.
                    float segmentWidth = (result.Count > 0
                            ? separatorWidth : 0f)
                        + abbrevWidth + levelWidth;
                    if (result.Count > 0 && used + segmentWidth > available)
                        break;
                    result.Add(new RowCaptionSegment(
                        abbrev, abbrevWidth,
                        SkillSignalPresentation.VerdictColor(choice.Bucket),
                        level, levelWidth,
                        ColonistStatsState.SkillTextColor(
                            presentation!.Line, // non-empty label implies a presentation
                            presentation.SignalView.PassionTier),
                        ", ", separatorWidth));
                    used += segmentWidth;
                }
                return result;
            }
            finally
            {
                Text.Font = oldFont;
            }
        }

        internal float StripHeightFor(Pawn pawn)
        {
            var store = RoleStore.Current;
            if (store == null) return RoleChipUI.Height;
            return RowSnapshotFor(pawn, store, EstimatedStripWidth).StripHeight;
        }

        /// One skill cell: the same fractional level, signal-derived colour,
        /// decorators and combined structured tooltip as the bottom stats panel.
        internal void DrawSkillCell(Rect cell, ColonistRowSnapshot row,
            ColonistSkillCellSnapshot skill)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                // Skill cells select like the name cell: the whole row should
                // act as one click target outside interactive chips/buttons.
                if (Widgets.ButtonInvisible(cell)) selectedPawn = row.Pawn;
                if (skill.Disabled)
                {
                    GUI.color = WrStyle.DisabledText;
                    Widgets.Label(new Rect(cell.x + 2f,
                        cell.y + ColonistRowTextOffsetY,
                        44f, cell.height), skill.ValueText);
                    return;
                }

                GUI.color = skill.TextColor;
                // Labels paint on Repaint only; layout and input passes only
                // need the cached presentation and hover region.
                if (Event.current.type == EventType.Repaint)
                    Widgets.Label(new Rect(cell.x + 2f,
                        cell.y + ColonistRowTextOffsetY,
                        44f, cell.height), skill.ValueText);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }

            float ix = cell.x + 48f;
            for (int iconIndex = 0; iconIndex < skill.IconCount; iconIndex++)
            {
                GUI.DrawTexture(new Rect(ix,
                    cell.y + (cell.height - 16f) / 2f,
                    16f, 16f), skill.IconAt(iconIndex));
                ix += 18f;
            }

            if (skill.Tooltip != null && Mouse.IsOver(cell))
                StructuredTipPresenter.TipRegion(cell, skill.Tooltip);
        }

        // Owner: Colonists window. Key: (role id, Pawn identity) within the
        // pawn-scope stamp. Value: scalar RoleRules.Pass result. Dependencies:
        // role rules, pawn lifecycle/location, and fixed tick/timezone boundary
        // invalidations represented by the stamp. Refresh: lazy per key after a
        // stamp change. Equality: exact key/stamp hits reuse the bool. Teardown:
        // ReleaseSnapshots/language or external refresh clears the dictionary.
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

        // Lazy per-pawn tips for the roster name column: the vanilla builder
        // runs per call, so WrTip defers it to the first hovered pass and
        // freezes the text per hover session. Selected-panel activity and trait
        // tips are detached in ColonistSelectedPanelState instead.
        // Owner: view (window scope). Key: pawn identity.
        // Value: WrTip (stable identity, gather closure built once).
        // Dependencies: none while cached; text gathers at hover time.
        // Refresh: per hover session. Equality: n/a. Teardown:
        // ReleaseSnapshots clears it.
        // Creation helpers keep the capturing lambdas out of the lookup
        // methods (display-class-at-entry allocation on hits otherwise).
        private readonly Dictionary<Pawn, WrTip> pawnTips = new Dictionary<Pawn, WrTip>();

        private WrTip PawnTip(Pawn pawn)
        {
            if (!pawnTips.TryGetValue(pawn, out WrTip tip))
                tip = CreatePawnTip(pawn);
            return tip;
        }

        // Vanilla's pawn-tip identity (id scheme and priority): the tip
        // dedups exactly like the eager signal it replaces.
        private WrTip CreatePawnTip(Pawn pawn)
            => pawnTips[pawn] = WrTip.PerSession("pawn:" + pawn.thingIDNumber,
                pawn.thingIDNumber * 152317, () => pawn.GetTooltip().text,
                TooltipPriority.Pawn);

        internal void DrawChipStrip(Rect stripRect, ColonistRowSnapshot row,
            float stripWidth)
        {
            // Ceil: odd slack keeps its extra pixel on top (the bottom is the
            // deliberately trimmed side).
            float yOffset = stripRect.y
                + Mathf.Ceil((stripRect.height - row.StripHeight) / 2f);

            for (int chipIndex = 0; chipIndex < row.ChipCount; chipIndex++)
            {
                RoleChipLayout chip = row.ChipAt(chipIndex);
                Rect localRect = chip.Rect;
                RoleCapabilityPresentation capability = chip.Capability;
                var chipRect = new Rect(stripRect.x + localRect.x, yOffset + localRect.y, localRect.width, localRect.height);

                bool chipEnabled = RoleActivation.IsActive(
                    chip.GlobalEnabled, chip.State);
                ChipStyle style = !chipEnabled ? ChipStyle.Disabled
                    : chip.Suppressed ? ChipStyle.ConditionalOff
                    : ChipStyle.Normal;
                // The cycle closure allocates: create it only on the one pass
                // that can consume it (left mouse-down inside this chip).
                System.Action? onClick = null;
                var pressEvent = Event.current;
                if (!chip.Suppressed && pressEvent.type == EventType.MouseDown && pressEvent.button == 0
                    && chipRect.Contains(pressEvent.mousePosition))
                {
                    Pawn capturedPawn = row.Pawn;
                    int capturedRoleId = chip.RenderData.RoleId;
                    onClick = () => RoleCommands.CycleRoleForPawn(
                        capturedPawn, capturedRoleId);
                }
                // The chip's one tooltip: marker meanings are folded into it.
                // Re-activated per hovered pass so the structured model stays
                // registered (untouched models retire every repaint generation).
                if (chip.Tooltip != null && Mouse.IsOver(chipRect))
                    StructuredTipPresenter.TipRegion(chipRect, chip.Tooltip);
                var click = RoleChipUI.Draw(chipRect, chip.RenderData, style,
                    showRemove: true, dragSource: row.Pawn,
                    onClick: onClick,
                    display: row.ChipDisplay, abbrev: chip.Abbreviation,
                    pinned: chip.Pinned,
                    warningSeverity: capability.WarningSeverity,
                    activeOutline: style == ChipStyle.Normal
                        && chip.RenderData.RoleId == row.ActiveRoleId,
                    strikes: RoleChipStrikes.Count(
                        chip.GlobalEnabled, chip.State),
                    forcedOn: chip.State == AssignmentState.ForceOn,
                    verdict: chip.Verdict, icon: chip.Icon);
                if (click == ChipClick.Remove)
                    RoleCommands.RemoveRoleFromPawn(
                        row.Pawn, chip.RenderData.RoleId);
                if (click == ChipClick.Context)
                {
                    Pawn menuPawn = row.Pawn;
                    int menuRoleId = chip.RenderData.RoleId;
                    string pinToggleLabel = chip.PinToggleLabel;
                    var menu = new RoleChipFloatMenu(new List<FloatMenuOption>
                    {
                        new FloatMenuOption(pinToggleLabel,
                            () => RoleCommands.ToggleAssignmentPin(menuPawn, menuRoleId))
                    });
                    Find.WindowStack.Add(menu);
                    StructuredTipPresenter.SetSuppressed(true);
                }
            }

            if (RoleDrag.Active && Mouse.IsOver(stripRect))
            {
                bool alreadyHasRole = row.HasRole(RoleDrag.RoleId);
                bool isSamePawn = RoleDrag.SourcePawn == row.Pawn;

                if (alreadyHasRole && !isSamePawn)
                {
                    RoleDrag.HoverBlocked = true;
                    Widgets.DrawBoxSolid(stripRect, new Color(0.8f, 0.2f, 0.2f, 0.12f));
                }
                else
                {
                    var mouse = Event.current.mousePosition;
                    int insertIndex = row.ChipInsertIndex(
                        new Vector2(mouse.x - stripRect.x, mouse.y - yOffset));

                    RoleDrag.HoverPawn = row.Pawn;
                    RoleDrag.HoverInsertIndex = insertIndex;

                    float markerX, markerY, markerH;
                    if (insertIndex == 0 || row.ChipCount == 0)
                    {
                        markerX = stripRect.x - ChipGap / 2f;
                        markerY = yOffset + 3f;
                        markerH = RoleChipUI.Height - 6f;
                    }
                    else
                    {
                        int prevIdx = insertIndex - 1;
                        Rect prevR = row.ChipAt(prevIdx).Rect;
                        // Centered in the gap after the previous chip, never inside it.
                        markerX = stripRect.x + prevR.xMax + ChipGap / 2f;
                        markerY = yOffset + prevR.y + 3f;
                        markerH = prevR.height - 6f;
                    }
                    Widgets.DrawBoxSolid(new Rect(markerX - 1f, markerY, 2f, markerH), new Color(1f, 1f, 1f, 0.9f));
                }
            }
        }

        private sealed class RoleChipFloatMenu : FloatMenu
        {
            internal RoleChipFloatMenu(List<FloatMenuOption> options)
                : base(options)
            {
            }

            public override void PostClose()
            {
                try
                {
                    base.PostClose();
                }
                finally
                {
                    StructuredTipPresenter.SetSuppressed(false);
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

        /// Current activity under the portrait name: the claiming role as a chip
        /// (bright outline, display-only), or a caption-colored label for
        /// non-role activity. All displayed fields and tooltip content come from
        /// the activity-revision-gated selected-panel snapshot.
        private static void DrawActivitySlot(Rect slotRect,
            ColonistSelectedActivitySnapshot activity)
        {
            if (activity.HasRole)
            {
                var chipRect = new Rect(
                    slotRect.x + (slotRect.width - activity.RoleWidth) / 2f,
                    slotRect.y, activity.RoleWidth, slotRect.height);
                RoleChipUI.Draw(chipRect, activity.Role, ChipStyle.Normal,
                    showRemove: false, dragSource: null, onClick: null,
                    interactive: false);
            }
            else
            {
                GameFont oldFont = Text.Font;
                TextAnchor oldAnchor = Text.Anchor;
                bool oldWordWrap = Text.WordWrap;
                Color oldColor = GUI.color;
                try
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Text.WordWrap = false;
                    GUI.color = WrStyle.CaptionText;
                    float visualH = Mathf.Max(
                        slotRect.height, TinyText.LineHeight);
                    TinyText.Label(new Rect(slotRect.x,
                        slotRect.center.y - visualH / 2f,
                        slotRect.width, visualH), activity.Label);
                }
                finally
                {
                    Text.Font = oldFont;
                    Text.Anchor = oldAnchor;
                    Text.WordWrap = oldWordWrap;
                    GUI.color = oldColor;
                }
            }
            if (activity.Tooltip != null && Mouse.IsOver(slotRect))
                StructuredTipPresenter.TipRegion(slotRect, activity.Tooltip);
        }

        private void DrawStatsPanel(Rect rect, RoleStore store)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWordWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            try
            {
            Widgets.DrawBoxSolidWithOutline(
                rect, WrStyle.PanelBackground, WrStyle.PanelOutline);
            rect = rect.ContractedBy(StatsPadding);
            if (selectedPawn == null) return;
            ColonistsRosterCatalogSnapshot rosterCatalog =
                rosterState.Catalog(store);
            ColonistSelectedPanelSnapshot? selected = selectedPanelState.Snapshot(
                store, selectedPawn, rosterCatalog,
                PortraitDisplaySize, PortraitDisplaySize);
            if (selected == null) return;
            ColonistSelectedChromeSnapshot chrome = selected.Chrome;

            // Left section: framed portrait with the name in a tag overlaying
            // the frame's top border, then the current-activity slot below.
            // The frame is smaller than the portrait texture, which draws
            // full-size centered so its transparent margins overflow evenly.
            // Frame offset matches the left inset (padding + centering) so the
            // top and left distances to the panel border are identical.
            float portraitBoxSize = PortraitDisplaySize;
            float frameInset = (portraitBoxSize - PortraitFrameSize) / 2f;
            // Whole cluster sits 4px lower so top and bottom spacing match.
            var portraitFrameRect = new Rect(rect.x + frameInset, rect.y + frameInset + 4f,
                PortraitFrameSize, PortraitFrameH);

            Widgets.DrawBoxSolidWithOutline(portraitFrameRect,
                new Color(0.05f, 0.05f, 0.05f, 1f),
                new Color(1f, 1f, 1f, 0.25f));
            // Clicking the portrait recenters the table on the selection.
            if (Widgets.ButtonInvisible(portraitFrameRect))
                CenterSelectedRow();
            // Portrait centered in the taller frame, nudged 8px below center.
            GUI.DrawTexture(
                new Rect(rect.x,
                    portraitFrameRect.y + (PortraitFrameH - portraitBoxSize) / 2f + 8f,
                    portraitBoxSize, portraitBoxSize),
                chrome.Portrait);

            // Name tag centered on the frame's top border, drawn after the
            // portrait so it overlays both the border and any portrait bleed.
            Text.Font = GameFont.Small;
            var nameTagRect = new Rect(
                portraitFrameRect.x
                    + (portraitFrameRect.width - chrome.NameTagWidth) / 2f,
                portraitFrameRect.y - PortraitNameH / 2f,
                chrome.NameTagWidth, PortraitNameH);
            Widgets.DrawBoxSolidWithOutline(nameTagRect,
                new Color(0.05f, 0.05f, 0.05f, 1f),
                new Color(1f, 1f, 1f, 0.25f));
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = chrome.NameColor;
            Widgets.Label(nameTagRect, chrome.Label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            float slotY = portraitFrameRect.yMax + 4f;
            DrawActivitySlot(
                new Rect(rect.x, slotY, portraitBoxSize, ActivitySlotH),
                selected.Activity);

            // Trait list under the activity slot (saves a trip to the Bio tab);
            // tooltips carry the vanilla trait descriptions.
            ColonistSelectedTraitsSnapshot pawnTraits = selected.Traits;
            if (pawnTraits.Count > 0)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                bool traitWrap = Text.WordWrap;
                Text.WordWrap = false;
                const float traitAdvance = 16f;
                float traitVisualH = Mathf.Max(
                    traitAdvance, TinyText.LineHeight);
                float traitY = slotY + ActivitySlotH + 2f;
                for (int i = 0; i < pawnTraits.Count; i++)
                {
                    if (traitY + traitAdvance > rect.yMax) break;
                    ColonistSelectedTraitRowSnapshot trait = pawnTraits.RowAt(i);
                    var traitSlotRect = new Rect(rect.x, traitY,
                        portraitBoxSize, traitAdvance);
                    var traitLabelRect = new Rect(rect.x,
                        traitSlotRect.center.y - traitVisualH / 2f,
                        portraitBoxSize, traitVisualH);
                    TinyText.Label(traitLabelRect, trait.Label);
                    if (trait.Tooltip != null && Mouse.IsOver(traitSlotRect))
                        StructuredTipPresenter.TipRegion(traitSlotRect,
                            trait.Tooltip);
                    traitY += traitAdvance;
                }
                Text.WordWrap = traitWrap;
                GUI.color = Color.white;
            }

            ColonistStatsSnapshot statsSnapshot = statsState.Snapshot(selected.Pawn);
            float skillColWidth = statsSnapshot.SkillColumnWidth;

            // Two equally sized skill columns after the portrait, separators
            // between them and before Recommended Roles.
            float col1X = rect.x + portraitBoxSize + 12f;
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
                int signalIconCount = signalIcons.Count;

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
                float iconWidth = signalIconCount == 0 ? 0f
                    : SkillLabelDecoratorGap
                        + signalIconCount * SkillDecoratorSize
                        + (signalIconCount - 1) * SkillDecoratorGap;
                float labelMaxW = Mathf.Max(0f,
                    skillColWidth - iconWidth - SkillValueGap - SkillValueWidth
                    - ColonistStatsState.VerdictStarsReserve);
                bool wrapWas = Text.WordWrap;
                Text.WordWrap = false;
                Widgets.Label(new Rect(xCursor, cellY, labelMaxW, CellH), labelText);
                Text.WordWrap = wrapWas;

                // Every signal with a resolved authored icon is rendered. Icons
                // deliberately have no individual tooltip; the cell owns one
                // combined structured tooltip for all skill and global signals.
                float iconX = xCursor + Mathf.Min(labelWidth, labelMaxW);
                if (signalIconCount > 0)
                {
                    iconX += SkillLabelDecoratorGap;
                    for (int signalIconIndex = 0;
                            signalIconIndex < signalIconCount;
                            signalIconIndex++)
                    {
                        Texture2D texture = signalIcons[signalIconIndex];
                        GUI.color = Color.white;
                        GUI.DrawTexture(new Rect(iconX,
                            cellY + (CellH - SkillDecoratorSize) / 2f,
                            SkillDecoratorSize, SkillDecoratorSize), texture);
                        iconX += SkillDecoratorSize + SkillDecoratorGap;
                    }
                }

                // Value sits left of the verdict star pair reserved at the
                // column's right edge.
                float valueX = cellX + skillColWidth - SkillValueWidth
                    - ColonistStatsState.VerdictStarsReserve;
                GUI.color = textColor;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(valueX, cellY, SkillValueWidth, CellH), line.ValueText);

                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                RoleChipVerdict verdictStars = presentation.VerdictStars;
                if (verdictStars.Shown)
                {
                    float starSize = ColonistStatsState.VerdictStarSize;
                    float starX = cellX + skillColWidth
                        - 2f * starSize - ColonistStatsState.VerdictStarGap;
                    float starY = cellY + (CellH - starSize) / 2f;
                    GUI.color = verdictStars.Bottom;
                    GUI.DrawTexture(new Rect(starX, starY, starSize, starSize),
                        WorkRolesTex.Star);
                    GUI.color = verdictStars.Top;
                    GUI.DrawTexture(new Rect(
                        starX + starSize + ColonistStatsState.VerdictStarGap,
                        starY, starSize, starSize), WorkRolesTex.Star);
                    GUI.color = Color.white;
                }

                var cellRect = new Rect(cellX, cellY, skillColWidth, CellH);
                StructuredTip? signalTip = presentation.Tooltip;
                if (signalTip != null && Mouse.IsOver(cellRect))
                    StructuredTipPresenter.TipRegion(cellRect, signalTip);
            }

            // Recommended Roles section: mirrors the Make It So outcome — kept roles
            // subtle, additions normal, removals struck — so the panel IS the preview
            // and the button applies directly.
            if (profile.ShowRecommendations && recX < rect.xMax)
            {
                float recW = rect.xMax - recX;
                ColonistRecommendationRenderSnapshot preview =
                    recommendationState.RenderSnapshot(store, selected.Pawn,
                        rosterCatalog, PawnListStamp, recW, rect.height,
                        externalSnapshotProvider);
                Rect localHeader = preview.HeaderRect;
                WrText.HeaderLabel(new Rect(recX + localHeader.x,
                    rect.y + localHeader.y, localHeader.width,
                    localHeader.height), preview.HeaderLabel);

                for (int previewIndex = 0;
                        previewIndex < preview.ChipCount; previewIndex++)
                {
                    ColonistRecommendationRenderChip chip =
                        preview.ChipAt(previewIndex);
                    Rect local = chip.Rect;
                    var chipRect = new Rect(recX + local.x, rect.y + local.y,
                        local.width, local.height);
                    if (chip.Assigned)
                    {
                        // Assigned: Subtle style, remove icon, body click inert.
                        // Disabled roles dim WITHOUT the strike (this panel
                        // shows recommendations, not verdicts); the red outline
                        // marks only enabled roles the plan would remove.
                        ChipClick click = RoleChipUI.Draw(chipRect, chip.Chip,
                            chip.Style,
                            showRemove: true, dragSource: null, onClick: null,
                            verdict: chip.Verdict);
                        if (click == ChipClick.Remove)
                            RoleCommands.RemoveRoleFromPawn(preview.Pawn,
                                chip.Chip.RoleId);
                        if (chip.RemovedOutline)
                            RoleChipUI.DrawRemovedOutline(chipRect);
                    }
                    else
                    {
                        // Closure only on the pass that can consume it (see the
                        // chip strip); captures declared inside the gate, or the
                        // display class would still allocate per iteration.
                        System.Action? onClick = null;
                        var pressEvent = Event.current;
                        if (pressEvent.type == EventType.MouseDown && pressEvent.button == 0
                            && chipRect.Contains(pressEvent.mousePosition))
                        {
                            Pawn clickPawn = preview.Pawn;
                            int clickId = chip.Chip.RoleId;
                            int insertIndex = chip.InsertIndex;
                            onClick = () => RoleCommands.AssignRole(
                                clickPawn, clickId, insertIndex);
                        }
                        RoleChipUI.Draw(chipRect, chip.Chip, ChipStyle.Normal,
                            showRemove: false, dragSource: null,
                            onClick: onClick, verdict: chip.Verdict);
                    }
                    if (Mouse.IsOver(chipRect))
                    {
                        if (chip.Tooltip != null)
                            StructuredTipPresenter.TipRegion(chipRect,
                                chip.Tooltip);
                        else if (chip.FallbackTip != null)
                            TooltipHandler.TipRegion(chipRect,
                                chip.FallbackTip);
                    }
                }

                // Make It So hides while auto-optimize owns bulk fixes; the
                // panel's chips stay clickable for per-role cherry-picking.
                if (preview.HasChanges
                    && RoleStore.Current?.autoOptimize != true)
                {
                    Rect localApply = preview.ApplyRect;
                    var makeItSoRect = new Rect(recX + localApply.x,
                        rect.y + localApply.y, localApply.width,
                        localApply.height);
                    if (Widgets.ButtonText(makeItSoRect,
                            preview.ApplyLabel))
                        preview.Apply();
                }
            }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWordWrap;
                GUI.color = oldColor;
            }
        }

        /// Preview entries from the colony plan (all changed pawns, or just one).
        private List<Dialog_ChangesPreview.PawnPreview> BuildFixEntries(Pawn? only)
        {
            RoleStore? store = RoleStore.Current;
            return store == null
                ? new List<Dialog_ChangesPreview.PawnPreview>()
                : recommendationState.FixEntries(store, only, selectedPawn,
                    PawnListStamp, ExternalSnapshotFor);
        }

        /// Rebuild for the preview's stale check: recompute the plan from scratch.
        private List<Dialog_ChangesPreview.PawnPreview> RebuildFixEntries(Pawn? only)
        {
            InvalidateRecommendationCache();
            return BuildFixEntries(only);
        }

        /// Applies the colony fix plan (all changed pawns, one pawn, or the preview's
        /// selected subset).
        private void ApplyFix(Pawn? only, HashSet<Pawn>? included = null)
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

        /// Opens the auto-optimize enable confirmation: the Fix My Colony
        /// preview plus an explanation banner, without per-pawn selection
        /// (the hourly schedule fixes everyone regardless). Confirming
        /// applies the shown changes now and turns the schedule on; nothing
        /// changes on cancel.
        public void ShowAutoOptimizeEnablePreview()
            => Find.WindowStack.Add(new Dialog_ChangesPreview(
                () => "WR_OptAutoOptimize".Translate(), BuildFixEntries(null),
                _ =>
                {
                    ApplyFix(null);
                    RoleCommands.SetAutoOptimize(true);
                },
                () => RebuildFixEntries(null),
                infoFactory: AutoOptimizeInfoText,
                warningFactory: AutoOptimizeGlobalWarningText,
                selectable: false));

        /// The enable banner restates the option tooltip's information, plus
        /// what confirming does.
        private static string AutoOptimizeInfoText() =>
            "WR_OptAutoOptimizeTipWhat".Translate().ToString() + " "
            + "WR_OptAutoOptimizeTipWhy".Translate().ToString() + "\n\n"
            + "WR_AutoOptimizeConfirmApply".Translate().ToString();

        /// With more than one colony the schedule's global reach deserves a
        /// warning: the toggle applies everywhere while the preview shows only
        /// the current colony. Counts the same maps AutoOptimizer will touch.
        private static string? AutoOptimizeGlobalWarningText()
        {
            int colonies = 0;
            List<Map> maps = Find.Maps;
            for (int index = 0; index < maps.Count; index++)
                if (ColonyScope.IsColonyLocationForSimulation(maps[index]))
                    colonies++;
            return colonies > 1
                ? "WR_AutoOptimizeGlobalWarning".Translate().ToString()
                : null;
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
