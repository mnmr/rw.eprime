using System.Collections.Generic;
using EPrimeReadouts.Core;
using EPrimeReadouts.Patches;
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

        // The readout runs before the window stack in the OnGUI order, so any
        // event it consumes never reaches a window drawn over it. While a
        // window owns the cursor the panel renders inert.
        private static bool inputBlocked;

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

        private static readonly object structuredTipOwner = new object();
        private static DrawModel draw;
        private static Vector2 scroll;
        private static float cachedTitleWidth = -1f;
        private static int cachedTitleUiVersion = -1;
        private static int viewStamp;
        private static int builtGroupsVersion = -1;
        private static int builtThresholdsVersion = -1;
        private static int builtStamp = -1;
        private static Map builtMap;
        private static float builtWidth;
        private static PoolSnapshot builtPools;
        private static RenderCountSnapshot builtCounts;

        /// Call after any per-player view-state change (depth, search, settings).
        public static void BumpView() => viewStamp++;

        public static void OnGUI()
        {
            UiVersion.ObserveCurrentMetrics();
            hotRects.Clear();
            if (Event.current.type == EventType.Layout) return;
            if (Current.ProgramState != ProgramState.Playing) return;
            var map = Find.CurrentMap;
            if (map == null || Find.MainTabsRoot.OpenTab == MainButtonDefOf.Menu) return;
            var store = ReadoutStore.Current;
            if (store == null) return;
            var settings = EPrimeReadoutsMod.Settings;

            float width = settings.panelWidth;
            var renderData = GameRenderData.Get(map, store);
            if (NeedsRebuild(store, map, width, renderData))
                Rebuild(store, map, width, renderData);

            inputBlocked = Find.WindowStack.GetWindowAt(Event.current.mousePosition) != null;
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) Patch_ActiveTip_TipRect.BeginGeneration(structuredTipOwner);
            try { Draw(map, store, settings); }
            finally { if (repaint) Patch_ActiveTip_TipRect.EndGeneration(structuredTipOwner); }
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

            // Header hot rect (gear + search)
            hotRects.Add(new Rect(x, settings.offsetY, width, SearchRowH));

            float maxContentH = Verse.UI.screenHeight - y - settings.bottomMargin;
            float totalH = draw.Model.TotalHeight;
            float contentW = draw.Model.TotalWidth;
            bool scrolling = totalH > maxContentH;
            float contentH = scrolling ? maxContentH : totalH;
            var outRect = new Rect(x, y, contentW, contentH);
            var viewRect = new Rect(0f, 0f, contentW, totalH);
            if (scrolling)
            {
                Widgets.BeginScrollView(outRect, ref scroll, viewRect, showScrollbars: false);
            }
            else
            {
                scroll = Vector2.zero;
                Widgets.BeginGroup(outRect);
            }
            CellRenderer.Draw(draw);
            HandleContentInput(store);
            if (scrolling) Widgets.EndScrollView();
            else Widgets.EndGroup();

            // Populate hot rects for each group container (GroupBack cells),
            // translated to screen space and clipped against the scroll outRect.
            float contentTop = y;
            var cells = draw.Model.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.Kind != CellKind.GroupBack) continue;
                var screenRect = new Rect(
                    x + cell.Rect.X,
                    contentTop + cell.Rect.Y - scroll.y,
                    cell.Rect.W,
                    cell.Rect.H);
                // Clip against the scroll outRect so off-screen parts don't count.
                float clipLeft   = screenRect.x < outRect.x ? outRect.x : screenRect.x;
                float clipTop    = screenRect.y < outRect.y ? outRect.y : screenRect.y;
                float clipRight  = screenRect.xMax > outRect.xMax ? outRect.xMax : screenRect.xMax;
                float clipBottom = screenRect.yMax > outRect.yMax ? outRect.yMax : screenRect.yMax;
                if (clipRight <= clipLeft || clipBottom <= clipTop) continue;
                hotRects.Add(new Rect(clipLeft, clipTop, clipRight - clipLeft, clipBottom - clipTop));
            }

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
                if (settings.showModNameWhenNoSearch)
                {
                    // Width measured once (Tiny font) — never per frame; the
                    // label rect fits the text exactly so it cannot wrap.
                    if (cachedTitleWidth < 0f || cachedTitleUiVersion != UiVersion.Current)
                    {
                        Text.Font = GameFont.Tiny;
                        cachedTitleWidth = Text.CalcSize("EPR.Title".Translate()).x + 4f;
                        cachedTitleUiVersion = UiVersion.Current;
                        Text.Font = GameFont.Small;
                    }
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = EprStyle.HeaderText;
                    Widgets.Label(new Rect(rect.x + 26f, rect.y, cachedTitleWidth, rect.height),
                        "EPR.Title".Translate());
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                }
                return;
            }

            var fieldRect = new Rect(rect.x + 26f, rect.y + 1f, rect.width - 26f, 22f);
            if (inputBlocked)
            {
                GUI.Label(fieldRect, SearchText ?? "", Text.CurTextFieldStyle);
            }
            else
            {
                string newText = Widgets.TextField(fieldRect, SearchText ?? "");
                if (newText != SearchText)
                {
                    SearchText = newText;
                    BumpView();
                }
            }
            if (SearchText.NullOrEmpty()) return;
            var clearRect = new Rect(rect.xMax - 20f, rect.y + 5f, 16f, 16f);
            if (inputBlocked)
                GUI.DrawTexture(clearRect, TexButton.CloseXSmall);
            else if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
            {
                SearchText = "";
                BumpView();
            }
        }

        private static void HandleContentInput(ReadoutStore store)
        {
            if (inputBlocked) return;
            var hits = draw.Model.MarkerHits;
            var settings = EPrimeReadoutsMod.Settings;
            for (int i = 0; i < hits.Count; i++)
            {
                var rect = new Rect(hits[i].Rect.X, hits[i].Rect.Y, hits[i].Rect.W, hits[i].Rect.H);
                if (Mouse.IsOver(rect))
                {
                    Widgets.DrawHighlight(rect);
                    TooltipHandler.TipRegion(rect, (TaggedString)"EPR.CycleTip".Translate());
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
                Width = width,
                Catalog = GameResourceCatalog.Instance,
                Pools = renderData.Structure,
            };
            draw = DrawModel.Resolve(ReadoutLayoutEngine.Build(input), renderData);
            builtGroupsVersion = store.GroupsVersion;
            builtThresholdsVersion = store.ThresholdsVersion;
            builtStamp = viewStamp;
            builtMap = map;
            builtWidth = width;
            builtPools = renderData.Structure;
            builtCounts = renderData.Counts;
        }
    }
}
