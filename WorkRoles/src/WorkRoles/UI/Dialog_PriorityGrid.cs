using System.Collections.Generic;
using RimShared.Common;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Read-only vanilla-style priority grid for a set of pawns: vanilla work
    /// boxes (skill-shaded backgrounds, passion overlays, check/number) under
    /// 45-degree column labels. Managed pawns can show raw ranks or the vanilla
    /// 0-4 projection — a view-only toggle seeded from the Options setting;
    /// unmanaged pawns always show their real vanilla priorities.
    public class Dialog_PriorityGrid : Window
    {
        private readonly List<Pawn> pawns;
        private readonly PriorityGridSortState sortState;
        private readonly int[] sortPriorities;
        private readonly List<WorkTypeDef> workTypes = new List<WorkTypeDef>();
        private readonly RevisionPairGate columnCacheRevisions = new RevisionPairGate();
        private float headerH;
        // Pawn names are fixed for the dialog lifetime. Definition and translated
        // column geometry is replaceable at its authoritative revision boundary.
        private readonly string[] pawnNames;
        // Assigned by EnsureColumnCache, called from the constructor.
        private string[] columnLabels = null!;
        private Vector2[] columnLabelSizes = null!;
        private InclinedLabelGeometry[] columnLabelGeometries = null!;
        private Vector2 phantomLabelSize;
        private InclinedLabelGeometry phantomLabelGeometry;
        private float headerRunOut;
        private string[] columnTips = null!;
        private string titleLabel = null!;
        private string rawModeLabel = null!;
        private string vanillaModeLabel = null!;
        private float modeToggleW;
        private Vector2 scroll;
        /// Local view state only — never written back to the synced setting.
        private bool showVanilla;

        // Owner: priority-grid dialog. Key: RoleStore identity, UiVersion,
        // definition revision, manual-priority display mode, and the exact
        // PriorityGridFacts revision of every fixed pawn. Value: one flattened,
        // immutable-by-publication snapshot of every repaint-ready cell plus the
        // display mode. Dependencies: role/assignment priority projections,
        // work-type definitions, pawn disabled-work state, whole skill levels,
        // passions, unmanaged vanilla priorities, and manual-priority mode.
        // Refresh: immediate on an event-driven key change. Equality: equal
        // rebuilt contents retain identity within the same RoleStore owner.
        // Teardown: PostClose drops snapshot/owner references and revision stamps.
        private PriorityGridSnapshot? gridSnapshot;
        private RoleStore? gridStore;
        private int gridUiRevision = int.MinValue;
        private int gridDefinitionRevision = int.MinValue;
        private int gridFactsCurrent = int.MinValue;
        private readonly int[] gridPawnFactRevisions;
        private bool gridFactsReleased;

        private const float TitleH = 38f;
        private const float NameW = 170f;
        private const float ColW = 26f;   // vanilla work box (25) + gap
        private const float RowH = 27f;
        private const float LabelAngle = 45f;
        private const float HeaderRunOutPadding = 20f;
        private const float ScrollChromeH = 24f;
        private const float MaxScreenHeightFraction = 0.9f;
        private static readonly Color SortedHeaderColor = new Color(1f, 0.82f, 0.25f);

        private readonly struct PriorityGridCellSnapshot
        {
            internal PriorityGridCellSnapshot(Texture2D baseTexture,
                Texture2D blendTexture, float blendAlpha,
                Texture2D? passionTexture, int rawPriority,
                int vanillaPriority, string? rawLabel, string? vanillaLabel,
                Color priorityColor)
            {
                Available = true;
                BaseTexture = baseTexture;
                BlendTexture = blendTexture;
                BlendAlpha = blendAlpha;
                PassionTexture = passionTexture;
                RawPriority = rawPriority;
                VanillaPriority = vanillaPriority;
                RawLabel = rawLabel;
                VanillaLabel = vanillaLabel;
                PriorityColor = priorityColor;
            }

            internal bool Available { get; }
            internal Texture2D BaseTexture { get; }
            internal Texture2D BlendTexture { get; }
            internal float BlendAlpha { get; }
            internal Texture2D? PassionTexture { get; }
            internal int RawPriority { get; }
            internal int VanillaPriority { get; }
            internal string? RawLabel { get; }
            internal string? VanillaLabel { get; }
            internal Color PriorityColor { get; }

            internal int Priority(bool vanilla) => vanilla
                ? VanillaPriority : RawPriority;

            internal string? Label(bool vanilla) => vanilla
                ? VanillaLabel : RawLabel;

            internal bool ContentEquals(PriorityGridCellSnapshot other) =>
                Available == other.Available
                && ReferenceEquals(BaseTexture, other.BaseTexture)
                && ReferenceEquals(BlendTexture, other.BlendTexture)
                && BlendAlpha == other.BlendAlpha
                && ReferenceEquals(PassionTexture, other.PassionTexture)
                && RawPriority == other.RawPriority
                && VanillaPriority == other.VanillaPriority
                && string.Equals(RawLabel, other.RawLabel,
                    System.StringComparison.Ordinal)
                && string.Equals(VanillaLabel, other.VanillaLabel,
                    System.StringComparison.Ordinal)
                && PriorityColor == other.PriorityColor;
        }

        private sealed class PriorityGridSnapshot
        {
            private readonly PriorityGridCellSnapshot[] cells;
            private readonly int rowCount;
            private readonly int columnCount;

            internal PriorityGridSnapshot(PriorityGridCellSnapshot[] cells,
                int rowCount, int columnCount, bool numeric)
            {
                this.cells = cells;
                this.rowCount = rowCount;
                this.columnCount = columnCount;
                Numeric = numeric;
            }

            internal bool Numeric { get; }

            internal PriorityGridCellSnapshot CellAt(int row, int column) =>
                cells[row * columnCount + column];

            internal void CopyPriorities(int column, bool vanilla,
                int[] destination)
            {
                for (int row = 0; row < rowCount; row++)
                    destination[row] = CellAt(row, column).Priority(vanilla);
            }

            internal bool ContentEquals(PriorityGridSnapshot other)
            {
                if (other == null || Numeric != other.Numeric
                    || rowCount != other.rowCount
                    || columnCount != other.columnCount
                    || cells.Length != other.cells.Length)
                    return false;
                for (int i = 0; i < cells.Length; i++)
                    if (!cells[i].ContentEquals(other.cells[i])) return false;
                return true;
            }
        }

        public Dialog_PriorityGrid(List<Pawn> pawns)
        {
            this.pawns = pawns;
            sortState = new PriorityGridSortState(pawns.Count);
            sortPriorities = new int[pawns.Count];
            gridPawnFactRevisions = new int[pawns.Count];
            showVanilla = RoleStore.Current?.reportVanillaPriorities == true;
            using (new TextBlock(GameFont.Small))
            {
                pawnNames = new string[pawns.Count];
                for (int r = 0; r < pawns.Count; r++)
                    pawnNames[r] = pawns[r].LabelShortCap.Truncate(NameW - 6f);
            }
            EnsureColumnCache();
            for (int r = 0; r < pawns.Count; r++)
                PriorityGridFacts.Acquire(pawns[r]);
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            draggable = true;
        }

        public override Vector2 InitialSize
        {
            get
            {
                EnsureColumnCache();
                // The last label rises past its column; reserve its run-out.
                float w = Mathf.Min(NameW + workTypes.Count * ColW + headerRunOut
                        + Margin * 2f + 20f,
                    Verse.UI.screenWidth * 0.95f);
                var heightLayout = PriorityGridHeightLayout.Calculate(
                    rowCount: pawns.Count,
                    rowHeight: RowH,
                    fixedContentHeight: TitleH + headerH,
                    windowMarginsHeight: Margin * 2f,
                    scrollChromeHeight: ScrollChromeH,
                    maxWindowHeight: Verse.UI.screenHeight * MaxScreenHeightFraction);
                return new Vector2(w, heightLayout.WindowHeight);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            using var guiState = new GuiStateScope(capture: true);
            // Vanilla GUI.DragWindow runs after this returns, so skipping
            // MouseDrag passes here leaves window-move dragging intact.
            if (WrEvent.SkipContentPass()) return;
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            Color previousColor = GUI.color;
            try
            {
                DrawContents(inRect);
            }
            finally
            {
                Text.Font = previousFont;
                Text.Anchor = previousAnchor;
                GUI.color = previousColor;
            }
        }

        private void DrawContents(Rect inRect)
        {
            EnsureColumnCache();
            EnsureGridSnapshot();
            var titleRect = new Rect(inRect.x, inRect.y, inRect.width, TitleH);
            var headerRect = new Rect(inRect.x, titleRect.yMax, inRect.width, headerH);
            var rowsRect = new Rect(inRect.x, headerRect.yMax, inRect.width,
                Mathf.Max(0f, inRect.yMax - headerRect.yMax));

            if (Event.current.type == EventType.Repaint)
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(titleRect, titleLabel);
            }
            Text.Font = GameFont.Small;

            bool numeric = gridSnapshot!.Numeric; // published by EnsureGridSnapshot

            if (numeric)
            {
                var toggleRect = new Rect(titleRect.xMax - modeToggleW - 26f, titleRect.y + 2f,
                    modeToggleW, 28f);
                if (Widgets.ButtonText(toggleRect, showVanilla ? vanillaModeLabel : rawModeLabel))
                {
                    showVanilla = !showVanilla;
                    RefreshSort();
                }
            }

            var viewRect = new Rect(0f, 0f,
                NameW + workTypes.Count * ColW + headerRunOut,
                pawns.Count * RowH);

            Widgets.BeginScrollView(rowsRect, ref scroll, viewRect);
            try
            {
                var scrollViewport = new Rect(scroll.x, scroll.y,
                    rowsRect.width, rowsRect.height);
                var visibleBodyColumns = UniformViewportRange.Calculate(
                    itemCount: workTypes.Count,
                    itemExtent: ColW,
                    contentStart: NameW,
                    viewportStart: scrollViewport.x,
                    viewportExtent: scrollViewport.width);
                var visibleRows = UniformViewportRange.Calculate(
                    itemCount: pawns.Count,
                    itemExtent: RowH,
                    contentStart: 0f,
                    viewportStart: scrollViewport.y,
                    viewportExtent: scrollViewport.height);

                if (Event.current.type == EventType.Repaint)
                {
                    DrawVisibleColumnChrome(visibleBodyColumns, pawns.Count * RowH);
                    DrawVisibleRows(visibleRows, visibleBodyColumns,
                        viewRect.width, scrollViewport.x < NameW);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            HandleHeaderInteractions(headerRect);

            // Draw the pinned header after the row panel so it remains the top
            // visual layer even if a future label style reaches the shared edge.
            if (Event.current.type == EventType.Repaint)
                DrawHeader(headerRect);
        }

        public override void PostClose()
        {
            if (!gridFactsReleased)
            {
                for (int r = 0; r < pawns.Count; r++)
                    PriorityGridFacts.ReleaseWatch(pawns[r]);
                gridFactsReleased = true;
            }
            gridSnapshot = null;
            gridStore = null;
            gridUiRevision = int.MinValue;
            gridDefinitionRevision = int.MinValue;
            gridFactsCurrent = int.MinValue;
            base.PostClose();
        }

        private void EnsureColumnCache()
        {
            int languageRevision = LanguageChangeCoordinator.Revision;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            if (!columnCacheRevisions.ShouldRefresh(languageRevision, definitionRevision)) return;

            // Column indices are definition-order dependent. A cache rebuild is
            // rare, so discard the transient sort instead of risking its index
            // now referring to a different work type.
            sortState.Reset();
            workTypes.Clear();
            foreach (WorkTypeDef workType in WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder)
                if (workType.visible)
                    workTypes.Add(workType);

            columnLabels = new string[workTypes.Count];
            columnLabelSizes = new Vector2[workTypes.Count];
            columnLabelGeometries = new InclinedLabelGeometry[workTypes.Count];
            columnTips = new string[workTypes.Count];
            titleLabel = "WR_PriorityGridTitle".Translate();
            rawModeLabel = "WR_GridModeRaw".Translate();
            vanillaModeLabel = "WR_GridModeVanilla".Translate();

            float maxVerticalExtent = 0f;
            float maxRightRunOut = 0f;
            using (new TextBlock(GameFont.Small))
            {
                phantomLabelSize = Text.CalcSize("");
                phantomLabelGeometry = InclinedLabelGeometry.Calculate(
                    phantomLabelSize.x, phantomLabelSize.y, LabelAngle);
                modeToggleW = Mathf.Max(WrText.FitWidth(rawModeLabel),
                    WrText.FitWidth(vanillaModeLabel)) + 24f;
                for (int c = 0; c < workTypes.Count; c++)
                {
                    columnLabels[c] = workTypes[c].labelShort.CapitalizeFirst();
                    // Vanilla's Work tab shows the type description; match it.
                    columnTips[c] = workTypes[c].gerundLabel.CapitalizeFirst()
                        + (workTypes[c].description.NullOrEmpty()
                            ? "" : "\n" + workTypes[c].description);
                    Vector2 size = Text.CalcSize(columnLabels[c]);
                    size.x = WrText.FitWidth(columnLabels[c]);
                    columnLabelSizes[c] = size;
                    var geometry = InclinedLabelGeometry.Calculate(
                        size.x, size.y, LabelAngle);
                    columnLabelGeometries[c] = geometry;
                    maxVerticalExtent = Mathf.Max(maxVerticalExtent, geometry.VerticalExtent);
                    maxRightRunOut = Mathf.Max(maxRightRunOut, geometry.RightRunOut);
                }
            }
            headerH = Mathf.Max(maxVerticalExtent + 8f, 40f);
            float separatorRunOut = headerH * Mathf.Cos(Mathf.Deg2Rad * LabelAngle);
            headerRunOut = Mathf.Max(maxRightRunOut, separatorRunOut)
                + HeaderRunOutPadding;
        }

        private void EnsureGridSnapshot()
        {
            RoleStore? store = RoleStore.Current;
            int uiRevision = UiVersion.Current;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            int factsCurrent = PriorityGridFacts.Revisions.Current;
            bool numeric = Current.Game?.playSettings?.useWorkPriorities ?? false;
            bool ownerChanged = !ReferenceEquals(gridStore, store);
            bool keyChanged = gridSnapshot == null || ownerChanged
                || gridUiRevision != uiRevision
                || gridDefinitionRevision != definitionRevision
                || gridSnapshot.Numeric != numeric;

            bool pawnFactsChanged = gridSnapshot == null;
            if (!pawnFactsChanged && gridFactsCurrent != factsCurrent)
            {
                for (int r = 0; r < pawns.Count; r++)
                    if (gridPawnFactRevisions[r]
                        != PriorityGridFacts.Revisions.RevisionOf(pawns[r]))
                    {
                        pawnFactsChanged = true;
                        break;
                    }
            }

            if (!keyChanged && !pawnFactsChanged)
            {
                // An unrelated pawn may have moved the global observation
                // revision. Record it after proving this fixed cohort unchanged.
                gridFactsCurrent = factsCurrent;
                return;
            }

            int columnCount = workTypes.Count;
            var cells = new PriorityGridCellSnapshot[pawns.Count * columnCount];
            for (int r = 0; r < pawns.Count; r++)
            {
                Pawn pawn = pawns[r];
                for (int c = 0; c < columnCount; c++)
                    cells[r * columnCount + c] =
                        BuildCell(pawn, workTypes[c], store);
            }

            var rebuilt = new PriorityGridSnapshot(cells, pawns.Count,
                columnCount, numeric);
            bool identityChanged = ownerChanged || gridSnapshot == null
                || !gridSnapshot.ContentEquals(rebuilt);
            if (identityChanged) gridSnapshot = rebuilt;

            gridStore = store;
            gridUiRevision = uiRevision;
            gridDefinitionRevision = definitionRevision;
            gridFactsCurrent = factsCurrent;
            for (int r = 0; r < pawns.Count; r++)
                gridPawnFactRevisions[r] =
                    PriorityGridFacts.Revisions.RevisionOf(pawns[r]);

            if (identityChanged) RefreshSort();
        }

        private static PriorityGridCellSnapshot BuildCell(Pawn pawn,
            WorkTypeDef workType, RoleStore? store)
        {
            if (pawn.WorkTypeIsDisabled(workType)) return default;

            float skill = pawn.skills != null
                ? pawn.skills.AverageOfRelevantSkillsFor(workType) : 10f;
            Texture2D baseTexture;
            Texture2D blendTexture;
            float blendAlpha;
            if (skill < 4f)
            {
                baseTexture = WidgetsWork.WorkBoxBGTex_Awful;
                blendTexture = WidgetsWork.WorkBoxBGTex_Bad;
                blendAlpha = skill / 4f;
            }
            else if (skill <= 14f)
            {
                baseTexture = WidgetsWork.WorkBoxBGTex_Bad;
                blendTexture = WidgetsWork.WorkBoxBGTex_Mid;
                blendAlpha = (skill - 4f) / 10f;
            }
            else
            {
                baseTexture = WidgetsWork.WorkBoxBGTex_Mid;
                blendTexture = WidgetsWork.WorkBoxBGTex_Excellent;
                blendAlpha = (skill - 14f) / 6f;
            }

            Passion passion = pawn.skills?.MaxPassionOfRelevantSkillsFor(workType)
                ?? Passion.None;
            Texture2D? passionTexture = passion == Passion.Major
                ? WidgetsWork.PassionWorkboxMajorIcon
                : passion == Passion.Minor
                    ? WidgetsWork.PassionWorkboxMinorIcon
                    : null;

            bool managed = store != null && store.IsManaged(pawn);
            int rawPriority;
            int vanillaPriority;
            if (managed)
            {
                rawPriority = CompiledJobOrders.PriorityFor(pawn, workType);
                vanillaPriority = CompiledJobOrders.VanillaPriorityFor(pawn, workType);
            }
            else
            {
                rawPriority = pawn.workSettings?.GetPriority(workType) ?? 0;
                vanillaPriority = rawPriority;
            }

            int colorKey = managed
                ? vanillaPriority : Mathf.Clamp(rawPriority, 0, 4);
            return new PriorityGridCellSnapshot(baseTexture, blendTexture,
                blendAlpha, passionTexture, rawPriority, vanillaPriority,
                rawPriority > 0 ? rawPriority.ToStringCached() : null,
                vanillaPriority > 0 ? vanillaPriority.ToStringCached() : null,
                WidgetsWork.ColorOfPriority(colorKey));
        }

        private void HandleHeaderInteractions(Rect headerRect)
        {
            for (int c = 0; c < workTypes.Count; c++)
            {
                float x = headerRect.x + NameW + c * ColW - scroll.x;
                var headRect = new Rect(x, headerRect.y, ColW, headerRect.height);
                TooltipHandler.TipRegion(headRect, columnTips[c]);
                if (WrText.InclinedLabelButton(headRect, columnLabelSizes[c],
                        columnLabelGeometries[c], LabelAngle))
                    ToggleSort(c);
            }
        }

        private void DrawHeader(Rect headerRect)
        {
            // Inclined labels project to the right of their base columns. Expand
            // the viewport left by that exact run-out so the last label remains
            // visible while scrolling through the reserved trailing header area.
            var visibleHeaderColumns = UniformViewportRange.Calculate(
                itemCount: workTypes.Count,
                itemExtent: ColW,
                contentStart: NameW,
                viewportStart: scroll.x - headerRunOut,
                viewportExtent: headerRect.width + headerRunOut);
            // Each label draws only its own trailing 45° line; the first label
            // needs the line BEFORE it drawn separately (an empty phantom label
            // one column to the left).
            WrText.InclinedLabel(new Rect(
                    headerRect.x + NameW - ColW - scroll.x,
                    headerRect.y,
                    ColW,
                    headerRect.height),
                "", phantomLabelSize, phantomLabelGeometry, LabelAngle);
            for (int c = visibleHeaderColumns.Start; c < visibleHeaderColumns.EndExclusive; c++)
            {
                float x = headerRect.x + NameW + c * ColW - scroll.x;
                var headRect = new Rect(x, headerRect.y, ColW, headerRect.height);
                Color? labelColor = sortState.SortedColumnIndex == c
                    ? SortedHeaderColor
                    : (Color?)null;
                WrText.InclinedLabel(headRect, columnLabels[c], columnLabelSizes[c],
                    columnLabelGeometries[c], LabelAngle, labelColor);
                // Header stub of the column separator; the body draws the rest.
                GUI.color = new Color(1f, 1f, 1f, 0.12f);
                WrText.LineVertical(x, headerRect.yMax - 2f, 2f);
                GUI.color = Color.white;
            }
        }

        private void DrawVisibleColumnChrome(
            UniformViewportRange visibleBodyColumns,
            float bodyH)
        {
            for (int c = visibleBodyColumns.Start; c < visibleBodyColumns.EndExclusive; c++)
            {
                float x = NameW + c * ColW;
                TooltipHandler.TipRegion(new Rect(x, 0f, ColW, bodyH), columnTips[c]);
                // Column separator, vanilla Work-tab style (pixel-snapped).
                GUI.color = new Color(1f, 1f, 1f, 0.12f);
                WrText.LineVertical(x, 0f, bodyH);
                GUI.color = Color.white;
            }
        }

        private void DrawVisibleRows(
            UniformViewportRange visibleRows,
            UniformViewportRange visibleBodyColumns,
            float viewWidth,
            bool pawnNamesVisible)
        {
            for (int r = visibleRows.Start; r < visibleRows.EndExclusive; r++)
            {
                int sourceRow = sortState.RowOrder[r];
                float y = r * RowH;
                if (r % 2 == 0)
                    Widgets.DrawBoxSolid(new Rect(0f, y, viewWidth, RowH),
                        new Color(1f, 1f, 1f, 0.04f));

                if (pawnNamesVisible)
                {
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(2f, y, NameW - 6f, RowH), pawnNames[sourceRow]);
                    Text.Anchor = TextAnchor.UpperLeft;
                }

                for (int c = visibleBodyColumns.Start; c < visibleBodyColumns.EndExclusive; c++)
                {
                    PriorityGridCellSnapshot cell =
                        gridSnapshot!.CellAt(sourceRow, c);
                    if (!cell.Available) continue; // vanilla leaves these blank
                    // Floored centering: (ColW - 25) / 2 is 0.5, and a half-pixel
                    // x smears the box textures at every UI scale.
                    var box = new Rect(NameW + c * ColW + Mathf.Floor((ColW - 25f) / 2f), y + (RowH - 25f) / 2f, 25f, 25f);
                    DrawWorkBoxBackground(box, cell);
                    int priority = cell.Priority(showVanilla);
                    if (priority <= 0) continue;
                    if (!gridSnapshot!.Numeric)
                    {
                        GUI.DrawTexture(box, WidgetsWork.WorkBoxCheckTex);
                    }
                    else
                    {
                        Text.Anchor = TextAnchor.MiddleCenter;
                        GUI.color = cell.PriorityColor;
                        Widgets.Label(box.ContractedBy(-3f), cell.Label(showVanilla));
                        GUI.color = Color.white;
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                }
            }
        }

        private void ToggleSort(int columnIndex)
        {
            gridSnapshot!.CopyPriorities(columnIndex, showVanilla, sortPriorities);
            sortState.Toggle(columnIndex, sortPriorities);
        }

        private void RefreshSort()
        {
            if (sortState.SortedColumnIndex is int columnIndex)
            {
                gridSnapshot!.CopyPriorities(columnIndex, showVanilla, sortPriorities);
                sortState.Refresh(sortPriorities);
            }
        }

        /// Vanilla's WidgetsWork.DrawWorkBoxBackground is private; this draws
        /// the equivalent already-resolved textures and blend values.
        private static void DrawWorkBoxBackground(Rect rect,
            PriorityGridCellSnapshot cell)
        {
            GUI.DrawTexture(rect, cell.BaseTexture);
            GUI.color = new Color(1f, 1f, 1f, cell.BlendAlpha);
            GUI.DrawTexture(rect, cell.BlendTexture);
            GUI.color = Color.white;

            if (cell.PassionTexture != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                var half = rect;
                half.xMin = rect.center.x;
                half.yMin = rect.center.y;
                GUI.DrawTexture(half, cell.PassionTexture);
                GUI.color = Color.white;
            }
        }
    }
}
