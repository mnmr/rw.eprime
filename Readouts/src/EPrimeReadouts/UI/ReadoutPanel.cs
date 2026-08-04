using System.Collections.Generic;
using EPrimeReadouts.Core;
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
        private static DrawModel draw;
        private static Vector2 scroll;
        private static float cachedTitleWidth = -1f;
        private static int cachedTitleUiVersion = -1;
        private static string cachedTitleText;
        private static string cachedCycleTip;
        private static int viewStamp;
        private static int builtGroupsVersion = -1;
        private static int builtThresholdsVersion = -1;
        private static int builtStamp = -1;
        private static Map builtMap;
        private static float builtWidth;
        private static PoolSnapshot builtPools;
        private static RenderCountSnapshot builtCounts;
        private static int builtUiVersion = -1;

        // Cache contract:
        // Owner: process/current main readout panel.
        // Key: draw-model identity, panel position/viewport, and scroll offset.
        // Value: screen-space interaction Rect list.
        // Dependencies: draw cells, x/header/content geometry and scroll.y.
        // Refresh policy: immediate when an exact dependency changes.
        // Equality policy: unchanged dependencies reuse the existing list contents.
        // Teardown: Hide/Reset clears rectangles and all retained identities.
        private static DrawModel hotDraw;
        private static float hotX;
        private static float hotHeaderY;
        private static float hotContentTop;
        private static float hotOutWidth;
        private static float hotOutHeight;
        private static float hotScrollY;
        private static bool hotVisible;

        /// Call after any per-player view-state change (depth, search, settings).
        public static void BumpView() => viewStamp++;

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
            cachedCycleTip = null;
            builtGroupsVersion = -1;
            builtThresholdsVersion = -1;
            builtStamp = -1;
            builtMap = null;
            builtWidth = 0f;
            builtPools = null;
            builtCounts = null;
            builtUiVersion = -1;
            inputBlocked = false;
            searchFieldFocused = false;
            SearchText = "";
            viewStamp = 0;
            hotDraw = null;
            hotVisible = false;
        }

        public static void OnGUI()
        {
            UiVersion.ObserveCurrentMetrics();
            if (Event.current.type == EventType.Layout) return;
            if (Current.ProgramState != ProgramState.Playing) { Hide(); return; }
            var map = Find.CurrentMap;
            if (map == null || Find.MainTabsRoot.OpenTab == MainButtonDefOf.Menu)
            { Hide(); return; }
            var store = ReadoutStore.Current;
            if (store == null) { Hide(); return; }
            var settings = EPrimeReadoutsMod.Settings;
            EnsurePresentationText();

            float width = settings.panelWidth;
            var renderData = GameRenderData.Get(map, store);
            if (NeedsRebuild(store, map, width, renderData))
                Rebuild(store, map, width, renderData);

            inputBlocked = Find.WindowStack.GetWindowAt(Event.current.mousePosition) != null;
            using (new GuiStateScope()) Draw(map, store, settings);
        }

        private static void Draw(Map map, ReadoutStore store, ReadoutSettings settings)
        {
            GenUI.DrawTextWinterShadow(new Rect(256f, 512f, -256f, -512f));
            Text.Font = GameFont.Small;

            float width = settings.panelWidth;
            float x = settings.offsetX;
            float y = settings.offsetY;
            DrawSearchRow(new Rect(x, y, width, SearchRowH));
            y += SearchRowH;

            float maxContentH = Verse.UI.screenHeight - y - settings.bottomMargin;
            float totalH = draw.Model.TotalHeight;
            float contentW = draw.Model.TotalWidth;
            bool scrolling = totalH > maxContentH;
            float contentH = scrolling ? maxContentH : totalH;
            var outRect = new Rect(x, y, contentW, contentH);
            var viewRect = new Rect(0f, 0f, contentW, totalH);
            if (scrolling) Widgets.BeginScrollView(outRect, ref scroll, viewRect,
                showScrollbars: false);
            else
            {
                scroll = Vector2.zero;
                Widgets.BeginGroup(outRect);
            }
            try
            {
                CellRenderer.Draw(draw);
                HandleContentInput(store);
            }
            finally
            {
                if (scrolling) Widgets.EndScrollView();
                else Widgets.EndGroup();
            }

            EnsureHotRects(x, settings.offsetY, y, outRect, scroll.y);

            ConsumeStrayEvents();
        }

        /// Single header row: gear (opens the config dialog), then — per the
        /// display options — the search field with its clear-X, or the mod
        /// name, or nothing.
        private static void DrawSearchRow(Rect rect)
        {
            var settings = EPrimeReadoutsMod.Settings;
            var gearRect = new Rect(rect.x, rect.y + 2f, 22f, 22f);
            if (inputBlocked)
                GUI.DrawTexture(gearRect, ReadoutTextures.Gear);
            else if (Widgets.ButtonImage(gearRect, ReadoutTextures.Gear))
                Find.WindowStack.Add(new Dialog_ReadoutConfig());

            if (!settings.showSearchFilter)
            {
                searchFieldFocused = false;
                if (settings.showModNameWhenNoSearch)
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
            var evt = Event.current;
            if (evt.type == EventType.MouseDown
                && !fieldRect.Contains(evt.mousePosition)
                && GUI.GetNameOfFocusedControl() == SearchControlName)
                Verse.UI.UnfocusCurrentControl();

            // Escape leaves the field (QuickSearchWidget behavior). Consuming
            // the event here keeps vanilla's raw Escape handling — which runs
            // later in the frame and opens the menu — from also seeing it.
            if (searchFieldFocused && evt.type == EventType.KeyDown
                && evt.keyCode == KeyCode.Escape)
            {
                Verse.UI.UnfocusCurrentControl();
                evt.Use();
            }

            if (inputBlocked)
            {
                searchFieldFocused = false;
                GUI.Label(fieldRect, SearchText ?? "", Text.CurTextFieldStyle);
            }
            else
            {
                GUI.SetNextControlName(SearchControlName);
                string newText = Widgets.TextField(fieldRect, SearchText ?? "");
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
            if (inputBlocked)
                GUI.DrawTexture(clearRect, TexButton.CloseXSmall);
            else if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
            {
                SearchText = "";
                BumpView();
                // A focused field would redraw its editor's stale text over
                // the cleared state; drop focus so the empty string sticks.
                Verse.UI.UnfocusCurrentControl();
            }
        }

        private static void HandleContentInput(ReadoutStore store)
        {
            if (inputBlocked) return;
            var settings = EPrimeReadoutsMod.Settings;

            // Clicking a slot selects its things on the map (shift adds to the
            // current selection). Hover/click detection is bounded iteration
            // over prebuilt hit rects; the selection pass itself runs only
            // inside the consumed click event.
            var slots = draw.Model.SlotHits;
            for (int i = 0; i < slots.Count; i++)
            {
                var rect = new Rect(slots[i].Rect.X, slots[i].Rect.Y,
                    slots[i].Rect.W, slots[i].Rect.H);
                if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
                if (Widgets.ButtonInvisible(rect) && Event.current.button == 0)
                    MapSelection.SelectMembers(builtMap, slots[i].Members,
                        settings.searchStorageOnly, settings.searchHideForbidden,
                        additive: Event.current.shift,
                        jumpCamera: settings.selectJumpCamera);
            }

            var hits = draw.Model.MarkerHits;
            for (int i = 0; i < hits.Count; i++)
            {
                var rect = new Rect(hits[i].Rect.X, hits[i].Rect.Y, hits[i].Rect.W, hits[i].Rect.H);
                if (Mouse.IsOver(rect))
                {
                    Widgets.DrawHighlight(rect);
                    TooltipHandler.TipRegion(rect, (TaggedString)cachedCycleTip);
                }
                if (Widgets.ButtonInvisible(rect))
                {
                    var group = store.Model.GroupById(hits[i].GroupId);
                    if (group == null) continue;
                    string key = store.DepthKey(group.Id);
                    int depth = settings.tierDepths.TryGetValue(key, out int stored)
                        ? stored : 1;
                    int next = Markers.NextDepth(group.TierCount, depth);
                    EPrimeReadoutsMod.Persist(s => s.tierDepths[key] = next);
                    BumpView();
                }
            }
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

        private static bool NeedsRebuild(
            ReadoutStore store,
            Map map,
            float width,
            RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData)
        {
            if (draw == null
                || builtGroupsVersion != store.GroupsVersion
                || builtThresholdsVersion != store.ThresholdsVersion
                || builtStamp != viewStamp
                || builtUiVersion != UiVersion.Current
                || builtMap != map || builtWidth != width
                || !ReferenceEquals(builtPools, renderData.Structure)
                || !ReferenceEquals(builtCounts, renderData.Counts))
                return true;
            return false;
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
                DepthOf = g => settings.tierDepths.TryGetValue(store.DepthKey(g.Id), out int depth)
                    ? depth : 1,
                Counts = renderData.Counts.Counts,
                Thresholds = store.Model.Thresholds,
                SearchText = SearchText,
                SearchCounts = renderData.Counts.SearchCounts,
                SearchHideZero = settings.searchHideZero,
                SearchStorageOnly = settings.searchStorageOnly,
                SearchHideForbidden = settings.searchHideForbidden,
                Width = width,
                Catalog = GameResourceCatalog.Instance,
                Pools = renderData.Structure,
                Metrics = PanelCellMetrics.Current,
            };
            draw = DrawModel.Resolve(ReadoutLayoutEngine.Build(input), renderData);
            builtGroupsVersion = store.GroupsVersion;
            builtThresholdsVersion = store.ThresholdsVersion;
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
            cachedCycleTip = UiText.Get("EPR.CycleTip");
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
            var cells = draw.Model.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.Kind != CellKind.GroupBack) continue;
                var screenRect = new Rect(x + cell.Rect.X,
                    contentTop + cell.Rect.Y - scrollY, cell.Rect.W, cell.Rect.H);
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
            if (!hotVisible) return;
            hotRects.Clear();
            hotDraw = null;
            hotVisible = false;
        }
    }
}
