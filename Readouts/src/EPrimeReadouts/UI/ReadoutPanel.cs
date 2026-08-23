using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// The readout itself, drawn from vanilla's ResourceReadoutOnGUI hook.
    /// Steady state blits the cached DrawModel; rebuilds happen only when the
    /// relevant model domain, view state, map, width or shared data changes.
    public static class ReadoutPanel
    {
        private const float SearchRowH = 26f;
        // Right-edge column reserved for the clear-X so the text field and the
        // button never share screen space (rows above/below align to it too).
        private const float ClearColW = 22f;
        private const string SearchControlName = "EPR.SearchField";

        // The readout runs before the window stack in the OnGUI order, so any
        // event it consumes never reaches a window drawn over it. While a
        // window owns the cursor the panel renders inert.
        private static bool inputBlocked;
        private static readonly PanelClickTracker contentClick =
            new PanelClickTracker();
        private static DrawModel? pressedDraw;
        private static bool pressedAdditive;

        // Search-field keyboard capture. Vanilla gates every KeyBindingDef
        // check (event-based and Input-polled alike) on
        // WindowStack.AnySearchWidgetFocused; Patch_WindowStack reports this
        // flag through that gate so typing never fires game shortcuts. The
        // frame stamp releases the guard within one frame if the panel stops
        // drawing (e.g. the map interface is no longer rendered).
        private static bool searchFieldFocused;
        private static int searchFocusFrame = -1;
        internal static bool SearchFieldCapturesInput =>
            searchFieldFocused && Time.frameCount - searchFocusFrame <= 1;

        /// Screen-space rects that count as "over the panel": the title/search
        /// header plus each group container (scroll-adjusted). Rebuilt every
        /// drawn frame; empty while the panel is hidden.
        private static readonly List<Rect> hotRects = new List<Rect>();

        public static bool IsOverPoint(Vector2 point)
        {
            for (int i = 0; i < hotRects.Count; i++)
                if (hotRects[i].Contains(point)) return true;
            return false;
        }

        public static string SearchText = "";

        // Cache contract:
        // Owner: process/current world and selected map.
        // Key: map identity, exact domain revisions, view stamp, width,
        // UiVersion, and shared pool/count snapshot identities.
        // Value: immutable resolved DrawModel plus presentation measurements.
        // Dependencies: those keys and per-player depth/search/settings state.
        // Refresh policy: immediate on dependency changes; counts arrive via
        // GameRenderData's 204-tick publisher.
        // Equality policy: unchanged dependencies preserve DrawModel identity.
        // Teardown: ReleaseMap/Reset drops map and store-derived state.
        private static DrawModel? draw;
        private static Vector2 scroll;
        private static float cachedTitleWidth = -1f;
        private static int cachedTitleUiVersion = -1;
        private static string? cachedTitleText;
        private static int viewStamp;
        private static int builtGroupsVersion = -1;
        private static int builtThresholdsVersion = -1;
        private static int builtCountRulesVersion = -1;
        private static int builtStamp = -1;
        private static Map? builtMap;
        private static float builtWidth;
        private static PoolSnapshot? builtPools;
        private static RenderCountSnapshot? builtCounts;
        private static LayoutInput? builtInput;
        private static int builtUiVersion = -1;

        // Buffered presentation owns no Unity resources until the main-thread
        // update gate builds its first inactive frame.
        private static PanelBufferPipeline bufferPipeline =
            new PanelBufferPipeline();
        private static PanelFrameBuffers frameBuffers = new PanelFrameBuffers(
            bufferPipeline, PanelBufferBackend.Shared);
        private static bool bufferedRendererDisabled;
        private static int graphicsEligibleFrame;
        private static int lastGraphicsFrame = -1;
        private static DrawModel? publishedGeometryDraw;
        private static int publishedGeometryWidth;
        private static int publishedGeometryHeight;
        private static float publishedGeometryScroll = float.NaN;
        private static int publishedIconRevision = -1;
        private static string? publishedHeaderKey;

        // Cache contract:
        // Owner: process/current main readout panel.
        // Key: draw-model identity, panel position/viewport, and scroll offset.
        // Value: screen-space interaction Rect list.
        // Dependencies: draw cells, x/header/content geometry and scroll.y.
        // Refresh policy: immediate when an exact dependency changes.
        // Equality policy: unchanged dependencies reuse the existing list contents.
        // Teardown: Hide/Reset clears rectangles and all retained identities.
        private static DrawModel? hotDraw;
        private static float hotX;
        private static float hotHeaderY;
        private static float hotContentTop;
        private static float hotOutWidth;
        private static float hotOutHeight;
        private static float hotScrollY;
        private static bool hotVisible;

        /// Call after any per-player view-state change (depth, search, settings).
        public static void BumpView() => viewStamp++;

        // Hover-driven tier depth (expandOnHover), PER BAND: the id of the
        // group whose band is under the pointer, or -1. A transition bumps
        // the view stamp so the cached DrawModel rebuilds once per band
        // enter/leave, never per frame. Stability: band heights never vary
        // with depth (collapse is horizontal), so expanding the hovered band
        // cannot shift any band vertically, and an expanded band's footprint
        // contains its collapsed footprint, so enter/leave cannot oscillate.
        private static int hoveredGroupId = -1;

        private static void UpdateHoverState(ReadoutSettings settings)
        {
            int hovered = -1;
            if (settings.expandOnHover && !inputBlocked && draw != null)
            {
                // Same geometry as Draw: content group at (x, contentTop),
                // clamped to the scroll viewport, offset by scroll.y.
                float x = settings.offsetX;
                float contentTop = settings.offsetY + SearchRowH;
                float maxContentH = Verse.UI.screenHeight - contentTop
                    - settings.bottomMargin;
                float totalH = draw.Model.TotalHeight;
                float contentH = totalH > maxContentH ? maxContentH : totalH;
                Vector2 mouse = Event.current.mousePosition;
                if (mouse.x >= x && mouse.y >= contentTop
                    && mouse.y < contentTop + contentH)
                {
                    float cx = mouse.x - x;
                    float cy = mouse.y - contentTop + scroll.y;
                    int bandIndex = PanelViewport.BandAt(
                        draw.Model.Bands, cx, cy,
                        scroll.y, scroll.y + contentH);
                    if (bandIndex >= 0)
                    {
                        int groupId = draw.Model.Bands[bandIndex].GroupId;
                        if (groupId >= 0) hovered = groupId;
                    }
                }
            }
            if (hovered != hoveredGroupId)
            {
                hoveredGroupId = hovered;
                BumpView();
            }
        }

        internal static void ReleaseMap(Map map)
        {
            if (map == null || !ReferenceEquals(builtMap, map)) return;
            Reset();
        }

        internal static void Reset()
        {
            hotRects.Clear();
            draw = null;
            scroll = Vector2.zero;
            cachedTitleWidth = -1f;
            cachedTitleUiVersion = -1;
            cachedTitleText = null;
            builtGroupsVersion = -1;
            builtThresholdsVersion = -1;
            builtCountRulesVersion = -1;
            builtStamp = -1;
            builtMap = null;
            builtWidth = 0f;
            builtPools = null;
            builtCounts = null;
            builtInput = null;
            builtUiVersion = -1;
            inputBlocked = false;
            CancelContentPress();
            searchFieldFocused = false;
            SearchText = "";
            viewStamp = 0;
            hoveredGroupId = -1;
            hotDraw = null;
            hotVisible = false;
            frameBuffers.Release();
            bufferPipeline = new PanelBufferPipeline();
            frameBuffers = new PanelFrameBuffers(
                bufferPipeline, PanelBufferBackend.Shared);
            bufferedRendererDisabled = false;
            graphicsEligibleFrame = 0;
            lastGraphicsFrame = -1;
            publishedGeometryDraw = null;
            publishedGeometryWidth = 0;
            publishedGeometryHeight = 0;
            publishedGeometryScroll = float.NaN;
            publishedIconRevision = -1;
            publishedHeaderKey = null;
        }

        internal static void ProcessPendingGraphics(Map map)
        {
            if (bufferedRendererDisabled || draw == null
                || !ReferenceEquals(map, builtMap)
                || !ReferenceEquals(map, Find.CurrentMap)
                || Time.frameCount < graphicsEligibleFrame
                || lastGraphicsFrame == Time.frameCount)
                return;
            lastGraphicsFrame = Time.frameCount;

            PanelBufferBackend backend = PanelBufferBackend.Shared;
            if (!backend.TryInitialize())
            {
                bufferedRendererDisabled = true;
                return;
            }
            if (!bufferPipeline.TryBeginBuild(
                out BufferBuildTicket ticket)) return;

            try
            {
                ReadoutSettings settings = EPrimeReadoutsMod.Settings;
                VisiblePanelGeometry geometry = CurrentGeometry(settings);
                PanelHeaderBufferData header = CurrentHeader(settings);
                if (!frameBuffers.BuildBack(
                    ticket, draw, geometry, header,
                    PanelVisualOptions.Default, UiVersion.Current,
                    IconScaleCache.Revision))
                {
                    DisableBufferedRenderer(
                        "a required icon, font, or buffer path is unsupported");
                }
            }
            catch (System.Exception exception)
            {
                DisableBufferedRenderer(
                    "buffer build threw " + exception.GetType().Name
                    + ": " + exception.Message);
            }
        }

        public static void OnGUI()
        {
            if (Event.current.type == EventType.Layout) return;
            UiVersion.ObserveCurrentMetrics();
            if (Current.ProgramState != ProgramState.Playing)
            { Hide(); return; }
            Map? currentMap = Find.CurrentMap;
            if (currentMap == null
                || Find.MainTabsRoot.OpenTab == MainButtonDefOf.Menu)
            { Hide(); return; }
            Map map = currentMap;
            ReadoutStore? currentStore = ReadoutStore.Current;
            if (currentStore == null) { Hide(); return; }
            ReadoutStore store = currentStore;
            ReadoutSettings settings = EPrimeReadoutsMod.Settings;
            EnsurePresentationText();
            float width = settings.panelWidth;
            inputBlocked = Find.WindowStack.GetWindowAt(
                Event.current.mousePosition) != null;

            RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData =
                GameRenderData.Get(map, store);

            UpdateHoverState(settings);
            if (NeedsStructuralRebuild(store, map, width, renderData))
            {
                Rebuild(store, map, width, renderData);
                QueueBaseRebuild();
            }
            else if (!ReferenceEquals(builtCounts, renderData.Counts))
            {
                bool refreshed = TryRefreshCounts(renderData);
                if (!refreshed)
                {
                    Rebuild(store, map, width, renderData);
                    QueueBaseRebuild();
                }
                else QueueCountFrame();
            }
            ObserveBufferInputs(settings);

            using (new GuiStateScope()) Draw(map, store, settings);
        }

        private static void Draw(Map map, ReadoutStore store, ReadoutSettings settings)
        {
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint && !bufferedRendererDisabled)
                frameBuffers.Swap();
            bool buffered = !bufferedRendererDisabled
                && frameBuffers.HasFront;

            GenUI.DrawTextWinterShadow(
                new Rect(256f, 512f, -256f, -512f));

            float width = settings.panelWidth;
            float x = settings.offsetX;
            float y = settings.offsetY;
            float maxContentH = Verse.UI.screenHeight
                - (y + SearchRowH) - settings.bottomMargin;
            float totalH = draw!.Model.TotalHeight;
            float contentW = draw.Model.TotalWidth;
            bool scrolling = totalH > maxContentH;
            float contentH = scrolling ? maxContentH : totalH;

            if (buffered && repaint)
            {
                frameBuffers.Present(x, y);
            }

            Text.Font = GameFont.Small;
            DrawSearchRow(
                new Rect(x, y, width, SearchRowH),
                drawStable: !buffered);
            y += SearchRowH;

            var outRect = new Rect(x, y, contentW, contentH);
            var viewRect = new Rect(0f, 0f, contentW, totalH);
            if (scrolling) Widgets.BeginScrollView(
                outRect, ref scroll, viewRect, showScrollbars: false);
            else
            {
                scroll = Vector2.zero;
                Widgets.BeginGroup(outRect);
            }
            try
            {
                float viewportTop = scrolling ? scroll.y : 0f;
                float viewportBottom = viewportTop + contentH;
                if (!buffered)
                {
                    CellRenderer.DrawDirect(
                        draw, viewportTop, viewportBottom,
                        inputBlocked, PanelVisualOptions.Default);
                }
                HandleContentInput(
                    store, viewportTop, viewportBottom);
            }
            finally
            {
                if (scrolling) Widgets.EndScrollView();
                else Widgets.EndGroup();
            }

            EnsureHotRects(x, settings.offsetY, y, outRect, scroll.y);

            ConsumeStrayEvents();

            ObserveBufferInputs(settings);
        }

        /// Single header row: gear (opens the config dialog), then — per the
        /// display options — the search field with its clear-X, or the mod
        /// name, or nothing.
        private static void DrawSearchRow(Rect rect, bool drawStable)
        {
            var settings = EPrimeReadoutsMod.Settings;
            var gearRect = new Rect(rect.x, rect.y + 2f, 22f, 22f);
            var evt = Event.current;
            bool repaint = evt.type == EventType.Repaint;
            bool drawGear = drawStable || !repaint
                || gearRect.Contains(evt.mousePosition);
            if (inputBlocked && drawStable)
                GUI.DrawTexture(gearRect, ReadoutTextures.Gear);
            else if (!inputBlocked && drawGear
                && Widgets.ButtonImage(gearRect, ReadoutTextures.Gear))
                Find.WindowStack.Add(new Dialog_ReadoutConfig());

            if (!settings.showSearchFilter)
            {
                searchFieldFocused = false;
                if (settings.showModNameWhenNoSearch && drawStable)
                {
                    // Width measured once (Small font) — never per frame; the
                    // label rect fits the text exactly so it cannot wrap.
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = EprStyle.HeaderText;
                    Widgets.Label(new Rect(rect.x + 26f, rect.y, cachedTitleWidth, rect.height),
                        cachedTitleText);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                return;
            }

            // The field stops short of the clear-X column; overlapping rects
            // would let the field consume the X's mouse-down.
            var fieldRect = new Rect(rect.x + 26f, rect.y + 1f,
                rect.width - 26f - ClearColW, 22f);

            // IMGUI never releases keyboard focus on its own: a click outside
            // the field (map, other rows, the clear-X) must unfocus it here.
            if (evt.type == EventType.MouseDown
                && !fieldRect.Contains(evt.mousePosition)
                && GUI.GetNameOfFocusedControl() == SearchControlName)
                Verse.UI.UnfocusCurrentControl();

            // Escape clears the filter and leaves the field. Consuming the
            // event here keeps vanilla's raw Escape handling — which runs
            // later in the frame and opens the menu — from also seeing it.
            if (searchFieldFocused && evt.type == EventType.KeyDown
                && evt.keyCode == KeyCode.Escape)
            {
                if (!SearchText.NullOrEmpty())
                {
                    SearchText = "";
                    BumpView();
                }
                Verse.UI.UnfocusCurrentControl();
                evt.Use();
            }

            if (inputBlocked)
            {
                searchFieldFocused = false;
                if (drawStable)
                    GUI.Label(fieldRect, SearchText ?? "",
                        Text.CurTextFieldStyle);
            }
            else if (drawStable || !repaint || searchFieldFocused)
            {
                GUI.SetNextControlName(SearchControlName);
                string newText = MouseFocusTextField.Draw(fieldRect,
                    SearchText ?? "", Text.CurTextFieldStyle);
                if (newText != SearchText)
                {
                    SearchText = newText;
                    BumpView();
                }
                searchFieldFocused = GUI.GetNameOfFocusedControl() == SearchControlName;
                searchFocusFrame = Time.frameCount;
            }
            if (SearchText.NullOrEmpty()) return;
            var clearRect = new Rect(rect.xMax - 20f, rect.y + 5f, 16f, 16f);
            bool drawClear = drawStable || !repaint
                || clearRect.Contains(evt.mousePosition);
            if (inputBlocked && drawStable)
                GUI.DrawTexture(clearRect, TexButton.CloseXSmall);
            else if (!inputBlocked && drawClear
                && Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
            {
                SearchText = "";
                BumpView();
                // A focused field would redraw its editor's stale text over
                // the cleared state; drop focus so the empty string sticks.
                Verse.UI.UnfocusCurrentControl();
            }
        }

        private static void HandleContentInput(
            ReadoutStore store, float viewportTop, float viewportBottom)
        {
            var evt = Event.current;
            bool isDrag = evt.type == EventType.MouseDrag;
            bool isRelease = evt.type == EventType.MouseUp && evt.button == 0;
            PanelPointerPolicy pointerPolicy = PanelPointerPolicy.For(
                contentClick.OwnsPointer, inputBlocked, isDrag, isRelease);
            if (pointerPolicy.ConsumeEvent)
            {
                if (isDrag)
                {
                    evt.Use();
                    return;
                }

                PanelHitTarget releasedOver =
                    pointerPolicy.ResolveReleaseTarget
                    && ReferenceEquals(pressedDraw, draw)
                        ? HitAt(evt.mousePosition, viewportTop, viewportBottom)
                        : PanelHitTarget.None;
                PanelHitTarget clicked = contentClick.Release(releasedOver);
                bool additive = pressedAdditive;
                pressedDraw = null;
                pressedAdditive = false;
                evt.Use();
                ActivateHit(store, clicked, additive);
                return;
            }

            if (inputBlocked) return;
            if (evt.type == EventType.Repaint)
            {
                DrawHitFeedback(HitAt(
                    evt.mousePosition, viewportTop, viewportBottom));
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                PanelHitTarget target = HitAt(
                    evt.mousePosition, viewportTop, viewportBottom);
                if (target.Kind == PanelHitKind.None) return;
                contentClick.Press(target);
                pressedDraw = draw;
                pressedAdditive = evt.shift;
                evt.Use();
                return;
            }
        }

        private static PanelHitTarget HitAt(
            Vector2 mouse, float viewportTop, float viewportBottom)
        {
            if (mouse.x < 0f || mouse.x >= draw!.Model.TotalWidth
                || mouse.y < viewportTop || mouse.y >= viewportBottom)
                return PanelHitTarget.None;

            int bandIndex = PanelViewport.BandAt(
                draw.Model.Bands, mouse.x, mouse.y,
                viewportTop, viewportBottom);
            if (bandIndex < 0) return PanelHitTarget.None;
            RenderBand band = draw.Model.Bands[bandIndex];

            int slotIndex = PanelViewport.SlotAt(
                draw.Model.SlotHits, band.SlotStart, band.SlotCount,
                mouse.x, mouse.y,
                viewportTop, viewportBottom);
            if (slotIndex >= 0) return PanelHitTarget.Slot(slotIndex);

            int markerIndex = PanelViewport.MarkerAt(
                draw.Model.MarkerHits, band.MarkerStart, band.MarkerCount,
                mouse.x, mouse.y,
                viewportTop, viewportBottom);
            return markerIndex >= 0
                ? PanelHitTarget.Marker(markerIndex)
                : PanelHitTarget.None;
        }

        private static void DrawHitFeedback(PanelHitTarget target)
        {
            if (target.Kind == PanelHitKind.Slot)
            {
                SlotHit hit = draw!.Model.SlotHits[target.Index];
                Widgets.DrawHighlight(new Rect(
                    hit.Rect.X, hit.Rect.Y, hit.Rect.W, hit.Rect.H));
                RenderCell icon = draw.Model.Cells[hit.CellIndex];
                Vector2 mouse = Event.current.mousePosition;
                if (mouse.x >= icon.Rect.X
                    && mouse.x < icon.Rect.X + icon.Rect.W
                    && mouse.y >= icon.Rect.Y
                    && mouse.y < icon.Rect.Y + icon.Rect.H)
                {
                    draw.RegisterHoveredTip(hit.CellIndex);
                }
            }
            else if (target.Kind == PanelHitKind.Marker)
            {
                MarkerHit hit = draw!.Model.MarkerHits[target.Index];
                var rect = new Rect(
                    hit.Rect.X, hit.Rect.Y, hit.Rect.W, hit.Rect.H);
                Widgets.DrawHighlight(rect);
                WrTips.Key("EPR.CycleTip").Region(rect);
            }
        }

        private static void ActivateHit(
            ReadoutStore store, PanelHitTarget target, bool additive)
        {
            var settings = EPrimeReadoutsMod.Settings;
            if (target.Kind == PanelHitKind.Slot)
            {
                SlotHit hit = draw!.Model.SlotHits[target.Index];
                MapSelection.SelectMembers(builtMap, hit.Members,
                    settings.searchStorageOnly, settings.searchHideForbidden,
                    additive, settings.selectJumpCamera);
                return;
            }
            if (target.Kind != PanelHitKind.Marker) return;

            MarkerHit marker = draw!.Model.MarkerHits[target.Index];
            var group = store.Model.GroupById(marker.GroupId);
            if (group == null) return;
            string key = store.DepthKey(group.Id);
            int depth = settings.tierDepths.TryGetValue(key, out int stored)
                ? stored : 1;
            int next = Markers.NextDepth(group.TierCount, depth);
            EPrimeReadoutsMod.Persist(s => s.tierDepths[key] = next);
            BumpView();
        }

        private static void CancelContentPress()
        {
            contentClick.Cancel();
            pressedDraw = null;
            pressedAdditive = false;
        }

        private static void QueueBaseRebuild()
        {
            if (bufferedRendererDisabled) return;
            bufferPipeline.InvalidateBase();
            graphicsEligibleFrame = System.Math.Max(
                graphicsEligibleFrame, Time.frameCount + 1);
        }

        private static void QueueCountFrame()
        {
            if (bufferedRendererDisabled) return;
            bufferPipeline.PublishCounts();
            graphicsEligibleFrame = System.Math.Max(
                graphicsEligibleFrame, Time.frameCount + 1);
        }

        private static void QueueVisibleFrame()
        {
            if (bufferedRendererDisabled) return;
            bufferPipeline.PublishCounts();
            graphicsEligibleFrame = System.Math.Max(
                graphicsEligibleFrame, Time.frameCount + 1);
        }

        private static void ObserveBufferInputs(ReadoutSettings settings)
        {
            if (bufferedRendererDisabled || draw == null) return;
            VisiblePanelGeometry geometry = CurrentGeometry(settings);
            PanelHeaderBufferData header = CurrentHeader(settings);
            int iconRevision = IconScaleCache.Revision;
            bool baseChanged = !ReferenceEquals(publishedGeometryDraw, draw)
                || publishedIconRevision != iconRevision;
            bool visibleChanged = publishedGeometryWidth != geometry.Width
                || publishedGeometryHeight != geometry.Height
                || publishedGeometryScroll != geometry.ScrollY
                || publishedHeaderKey != header.RevisionKey;

            if (baseChanged) QueueBaseRebuild();
            else if (visibleChanged) QueueVisibleFrame();

            publishedGeometryDraw = draw;
            publishedGeometryWidth = geometry.Width;
            publishedGeometryHeight = geometry.Height;
            publishedGeometryScroll = geometry.ScrollY;
            publishedIconRevision = iconRevision;
            publishedHeaderKey = header.RevisionKey;
        }

        private static VisiblePanelGeometry CurrentGeometry(
            ReadoutSettings settings)
        {
            float contentTop = settings.offsetY + SearchRowH;
            float maxContentHeight = Mathf.Max(
                0f, Verse.UI.screenHeight - contentTop
                    - settings.bottomMargin);
            float totalHeight = draw != null ? draw.Model.TotalHeight : 0f;
            float contentHeight = Mathf.Min(totalHeight, maxContentHeight);
            int headerHeight = Mathf.CeilToInt(SearchRowH);
            int visibleContentHeight = Mathf.Max(
                0, Mathf.CeilToInt(contentHeight));
            float contentWidth = draw != null
                ? draw.Model.TotalWidth : settings.panelWidth;
            PanelSurfaceSizing surface = PanelSurfaceSizing.Create(
                settings.panelWidth,
                contentWidth,
                headerHeight + visibleContentHeight,
                Prefs.UIScale);
            return new VisiblePanelGeometry(
                surface,
                headerHeight,
                visibleContentHeight,
                totalHeight > maxContentHeight ? scroll.y : 0f);
        }

        private static PanelHeaderBufferData CurrentHeader(
            ReadoutSettings settings) =>
            new PanelHeaderBufferData(
                settings.showSearchFilter,
                !settings.showSearchFilter
                    && settings.showModNameWhenNoSearch,
                SearchText ?? "",
                cachedTitleText ?? "",
                cachedTitleWidth);

        private static void DisableBufferedRenderer(string reason)
        {
            if (bufferedRendererDisabled) return;
            bufferedRendererDisabled = true;
            frameBuffers.Release();
            Log.Warning(
                "[Readouts] Buffered renderer disabled: " + reason);
        }

        /// Anything not consumed by a control inside the panel must not leak
        /// to the map (clicks would select things, wheel would zoom).
        private static void ConsumeStrayEvents()
        {
            if (inputBlocked) return;
            var evt = Event.current;
            if (!IsOverPoint(evt.mousePosition)) return;
            if (evt.type == EventType.ScrollWheel)
            {
                scroll.y += evt.delta.y * 20f;
                if (scroll.y < 0f) scroll.y = 0f;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDown)
            {
                evt.Use();
            }
        }

        private static bool NeedsStructuralRebuild(
            ReadoutStore store,
            Map map,
            float width,
            RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData)
        {
            if (draw == null
                || builtGroupsVersion != store.GroupsVersion
                || builtThresholdsVersion != store.ThresholdsVersion
                || builtCountRulesVersion != store.CountRulesVersion
                || builtStamp != viewStamp
                || builtUiVersion != UiVersion.Current
                || builtMap != map || builtWidth != width
                || !ReferenceEquals(builtPools, renderData.Structure))
                return true;
            return false;
        }

        private static bool TryRefreshCounts(
            RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData)
        {
            if (draw == null || builtInput == null) return false;
            builtInput.Counts = renderData.Counts.Counts;
            builtInput.SearchCounts = renderData.Counts.SearchCounts;
            builtInput.Debts = renderData.Counts.Debts;
            if (!ReadoutLayoutEngine.TryRefreshCounts(
                    builtInput, draw.Model)) return false;
            draw.RefreshCounts(renderData);
            builtCounts = renderData.Counts;
            return true;
        }

        private static void Rebuild(
            ReadoutStore store,
            Map map,
            float width,
            RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData)
        {
            var settings = EPrimeReadoutsMod.Settings;
            var groups = store.Model.InDisplayOrder();
            groups.RemoveAll(g => !(settings.enabledGroups.TryGetValue(store.DepthKey(g.Id), out bool on)
                ? on : g.DefaultEnabled));
            var input = new LayoutInput
            {
                Groups = groups,
                // Unset depth defaults to tier 1 only; users expand per group.
                // expandOnHover (per band): hovering a band expands that
                // group's configured tiers to all tiers; with
                // collapseWhenIdle, unhovered bands show 0 tiers (horizontal
                // collapse) and the hovered band shows its configured tiers,
                // never more.
                DepthOf = g =>
                {
                    int configured = settings.tierDepths.TryGetValue(
                        store.DepthKey(g.Id), out int depth) ? depth : 1;
                    if (!settings.expandOnHover) return configured;
                    if (g.Id == hoveredGroupId)
                        return settings.collapseWhenIdle ? configured : g.TierCount;
                    return settings.collapseWhenIdle ? 0 : configured;
                },
                // Hover-added tiers (beyond the configured depth) render
                // HoverLit triangles so the configured depth stays visible.
                ConfiguredDepthOf = g => settings.tierDepths.TryGetValue(
                    store.DepthKey(g.Id), out int depth) ? depth : 1,
                Counts = renderData.Counts.Counts,
                Thresholds = store.Model.Thresholds,
                CountRules = store.Model.CountRules,
                SearchText = SearchText,
                SearchCounts = renderData.Counts.SearchCounts,
                SearchHideZero = settings.searchHideZero,
                SearchStorageOnly = settings.searchStorageOnly,
                SearchHideForbidden = settings.searchHideForbidden,
                // Debt is already option-resolved in the snapshot; showing the
                // overrun as a negative is pure presentation, so it rides the
                // view stamp instead of invalidating counts.
                Debts = renderData.Counts.Debts,
                AllowNegativeCounts = settings.showNegativeCounts,
                Width = width,
                Catalog = GameResourceCatalog.Instance,
                Pools = renderData.Structure,
                Metrics = PanelCellMetrics.Current,
            };
            DrawModel next = DrawModel.Resolve(
                ReadoutLayoutEngine.Build(input), renderData);
            draw = next;
            builtInput = input;
            builtGroupsVersion = store.GroupsVersion;
            builtThresholdsVersion = store.ThresholdsVersion;
            builtCountRulesVersion = store.CountRulesVersion;
            builtStamp = viewStamp;
            builtMap = map;
            builtWidth = width;
            builtPools = renderData.Structure;
            builtCounts = renderData.Counts;
            builtUiVersion = UiVersion.Current;
        }

        private static void EnsurePresentationText()
        {
            if (cachedTitleUiVersion == UiVersion.Current
                && cachedTitleText != null) return;
            cachedTitleText = UiText.Get("EPR.Title");
            using (new GuiStateScope())
            {
                Text.Font = GameFont.Small;
                cachedTitleWidth = WrText.FitWidth(cachedTitleText) + 4f;
            }
            cachedTitleUiVersion = UiVersion.Current;
        }

        private static void EnsureHotRects(float x, float headerY,
            float contentTop, Rect outRect, float scrollY)
        {
            if (hotVisible
                && ReferenceEquals(hotDraw, draw)
                && hotX == x
                && hotHeaderY == headerY
                && hotContentTop == contentTop
                && hotOutWidth == outRect.width
                && hotOutHeight == outRect.height
                && hotScrollY == scrollY)
                return;

            hotRects.Clear();
            hotRects.Add(new Rect(x, headerY,
                EPrimeReadoutsMod.Settings.panelWidth, SearchRowH));
            var bands = draw!.Model.Bands; // rebuilt before Draw in OnGUI
            PanelBandRange visible = PanelViewport.VisibleBands(
                bands, scrollY, scrollY + outRect.height);
            for (int i = visible.Start; i < visible.End; i++)
            {
                RenderBand band = bands[i];
                var screenRect = new Rect(x + band.Rect.X,
                    contentTop + band.Rect.Y - scrollY,
                    band.Rect.W, band.Rect.H);
                float clipLeft = screenRect.x < outRect.x ? outRect.x : screenRect.x;
                float clipTop = screenRect.y < outRect.y ? outRect.y : screenRect.y;
                float clipRight = screenRect.xMax > outRect.xMax
                    ? outRect.xMax : screenRect.xMax;
                float clipBottom = screenRect.yMax > outRect.yMax
                    ? outRect.yMax : screenRect.yMax;
                if (clipRight <= clipLeft || clipBottom <= clipTop) continue;
                hotRects.Add(new Rect(clipLeft, clipTop,
                    clipRight - clipLeft, clipBottom - clipTop));
            }
            hotDraw = draw;
            hotX = x;
            hotHeaderY = headerY;
            hotContentTop = contentTop;
            hotOutWidth = outRect.width;
            hotOutHeight = outRect.height;
            hotScrollY = scrollY;
            hotVisible = true;
        }

        private static void Hide()
        {
            searchFieldFocused = false;
            CancelContentPress();
            if (!hotVisible) return;
            hotRects.Clear();
            hotDraw = null;
            hotVisible = false;
        }
    }
}
