using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// One-time welcome shared by every mod (each mod's game component
    /// decides when to show it and records it as seen): the mod's name,
    /// cropped preview art, a short description, a framed pointer to the
    /// control that opens the mod and, optionally, a warning panel. Close
    /// and "Take me there now" both dismiss it; the latter (or the button
    /// copy in the find panel) also runs the mod's open action. ESC closes
    /// it via the standard cancel path while it has focus.
    ///
    /// Callers pass translated strings at construction, immediately before
    /// adding the window, so no text is resolved during drawing.
    public sealed class WelcomeDialog : Window
    {
        /// Resolve the caller's catalog once at the welcome lifecycle boundary.
        /// Installed but inactive mods do not produce conflicts. The returned
        /// immutable notice is shared by admission and the window's presentation.
        public static WelcomeConflictNotice FindConflicts(IReadOnlyList<WelcomeModConflict> configured)
        {
            var packages = new List<string>();
            var mods = LoadedModManager.RunningModsListForReading;
            for (int i = 0; i < mods.Count; i++) packages.Add(mods[i].PackageIdPlayerFacing);
            return WelcomeConflictNotice.ForActivePackages(configured, packages);
        }

        /// Reopen once for each newly encountered conflict, even for an existing
        /// save. Caller persists both player-owned lists after successful admission.
        public static bool ClaimSave(List<string> shownSaves, Action retry,
            WelcomeConflictNotice conflicts, List<string> conflictsSeen)
        {
            if (Find.World == null) return false;
            string id = Find.World.info.persistentRandomValue.ToString();
            var seen = conflicts.HasUnseen(id, conflictsSeen) ? new List<string>() : shownSaves;
            if (!ClaimSave(seen, retry)) return false;
            if (!shownSaves.Contains(id)) shownSaves.Add(id);
            conflicts.MarkSeen(id, conflictsSeen);
            return true;
        }
        /// Callers skip the welcome (without marking the save seen) while
        /// dev mode is on: mod authors start many throwaway games and must
        /// not be greeted on every one.
        public static bool Suppressed => Prefs.DevMode;

        /// The once-per-player-per-save gate every mod's welcome goes
        /// through. True when the current save has not shown this mod's
        /// welcome yet: its id (the world's persistent random value, the
        /// save's stable identity) is added to shownSaves, and the caller
        /// persists its settings and opens the dialog. Marked seen the
        /// moment it is claimed, so however the dialog is dismissed it never
        /// returns for this save. False with no world, while Suppressed, or
        /// once seen. Presentation only: per-player settings, never synced
        /// state.
        ///
        /// Also false while the scenario's opening is on screen (vanilla's
        /// start dialog, or a replacement such as Immersive Opening; both
        /// bracket it with WindowStack.Notify_GameStartDialogOpened/Closed):
        /// a welcome added then draws over the opening. The save stays
        /// unclaimed and retry (the caller's own queue action) runs once the
        /// opening closes.
        public static bool ClaimSave(List<string> shownSaves, Action retry)
        {
            if (suspendedWindows.IsStopped) return false;
            RimWorld.Planet.World? world = Find.World;
            if (world == null || Suppressed) { ResetPending(); return false; }
            string id = world.info.persistentRandomValue.ToString();
            if (shownSaves.Contains(id)) return false;
            pendingSaveId = id;
            if (!openingClosing
                && Find.WindowStack?.SecondsSinceClosedGameStartDialog == 0f)
            {
                retryAfterOpening = retry;
                return false;
            }
            retryAfterOpening = null;
            int visible = VisibleWelcomeCount();
            if (WelcomeTileLayout.Create(UI.screenWidth, UI.screenHeight, visible + 1).Count <= visible)
            {
                retryAfterSpace = retry;
                return false;
            }
            retryAfterSpace = null;
            pendingSaveId = null;
            shownSaves.Add(id);
            return true;
        }

        // The welcome waiting for the opening to close: this assembly's (one
        // mod's) cached static queue action, holding no game state. Set by
        // ClaimSave while the opening is on screen; cleared when the opening
        // closes (Patch_GameStartDialogClosed runs it) or by the next claim.
        // One left over from a game quit mid-opening is harmless: it re-runs
        // the claim against whatever save is current.
        private static Action? retryAfterOpening;
        // Owner: this assembly's pending welcome. Key: persistent save id.
        // Value: the caller's cached static retry, never a world/model capture.
        // Dependencies: the same save, opening state and visible window count.
        // Refresh: opening-close/window-remove events only. Equality: one retry
        // per mod/save; replacing it does not enqueue a second operation.
        // Teardown: successful claim, suppression, game disposal or save change.
        private static Action? retryAfterSpace;
        private static string? pendingSaveId;
        private static readonly WelcomePendingWindows<WelcomeDialog> suspendedWindows =
            new WelcomePendingWindows<WelcomeDialog>();

        // Owner: process/assembly. Key/value: last observed logical display
        // dimensions. Dependencies: window pixels and UI scale. Refresh:
        // dimension change only; steady OnGUI compares two integers. Equality:
        // unchanged dimensions do no work. Teardown: primitive-only process
        // stamp; no window/world reference is retained across disposal.
        private static int observedScreenWidth = -1, observedScreenHeight = -1;
        private static readonly Comparison<Window> CompareWindowIds =
            (left, right) => left.ID.CompareTo(right.ID);

        internal static void ResetPending()
        {
            retryAfterOpening = null;
            retryAfterSpace = null;
            pendingSaveId = null;
            suspendedWindows.Clear();
        }

        internal static void BeginGame()
        {
            ResetPending();
            suspendedWindows.Start();
        }

        internal static void BeginDisposal()
        {
            ResetPending();
            suspendedWindows.Stop();
        }

        internal static void CloseDisposedWindows()
        {
            WindowStack? stack = Find.WindowStack;
            if (stack == null) return;
            var owned = new List<WelcomeDialog>();
            for (int i = 0; i < stack.Count; i++)
                if (stack[i] is WelcomeDialog welcome) owned.Add(welcome);
            for (int i = 0; i < owned.Count; i++) owned[i].Close(false);
        }

        internal static bool IsWelcome(Window window) =>
            window.GetType().FullName == "RimShared.UiLib.WelcomeDialog";

        private static int VisibleWelcomeCount()
        {
            var windows = Find.WindowStack.Windows;
            int count = 0;
            for (int i = 0; i < windows.Count; i++)
                if (IsWelcome(windows[i])) count++;
            return count;
        }

        internal static void SpaceAvailable()
        {
            if (suspendedWindows.IsStopped) return;
            RimWorld.Planet.World? world = Find.World;
            if (world == null || Suppressed) { ResetPending(); return; }
            string currentSave = world.info.persistentRandomValue.ToString();
            int visible = VisibleWelcomeCount();
            bool capacity = WelcomeTileLayout.Create(UI.screenWidth, UI.screenHeight, visible + 1).Count > visible;
            if (suspendedWindows.TryTake(currentSave, capacity, out WelcomeDialog? suspended))
            {
                Find.WindowStack.Add(suspended!);
                return;
            }
            Action? retry = retryAfterSpace;
            if (retry == null) return;
            if (currentSave != pendingSaveId)
            { ResetPending(); return; }
            retryAfterSpace = null;
            retry();
        }

        internal static void ObserveScreen()
        {
            if (suspendedWindows.IsStopped) return;
            int width = UI.screenWidth, height = UI.screenHeight;
            if (observedScreenWidth == width && observedScreenHeight == height) return;
            observedScreenWidth = width;
            observedScreenHeight = height;
            ReflowWindows();
            SpaceAvailable();
        }

        // True while OpeningClosed runs the retry: for the rest of the
        // closing frame the stack still reports 0 seconds since the close.
        private static bool openingClosing;

        internal static void OpeningClosed()
        {
            if (suspendedWindows.IsStopped) return;
            Action? retry = retryAfterOpening;
            if (retry == null) return;
            if (Find.World == null || Suppressed
                || Find.World.info.persistentRandomValue.ToString() != pendingSaveId)
            { ResetPending(); return; }
            retryAfterOpening = null;
            openingClosing = true;
            try { retry(); }
            finally { openingClosing = false; }
        }

        private const float PreviewWidth = 520f;
        private const float IconSize = 48f;
        private const float ButtonHeight = 35f;
        private const float Gap = 12f;
        private const float PanelPad = 10f;
        private const float DialogWidth = 600f;

        private static readonly Color LinkColor = new Color(0.45f, 0.7f, 1f);
        private static readonly Color LinkHoverColor =
            new Color(0.65f, 0.85f, 1f);
        private static readonly Color WarningText = new Color(1f, 0.8f, 0.35f);
        private static readonly Color WarningOutline =
            new Color(1f, 0.8f, 0.35f, 0.6f);
        private static readonly Color WarningBackground =
            new Color(0.08f, 0.08f, 0.08f, 0.9f);

        private readonly string title;
        private readonly string body;
        private readonly string find;
        private readonly string takeMeThere;
        private readonly string previewPath;
        private readonly Rect previewCrop;
        private readonly Action open;

        /// A 48px icon left of the find text (the button the player looks
        /// for), or null.
        public Texture2D? FindIcon { get; set; }
        /// Label of a bottom-bar main button to draw, exactly as the bar
        /// draws it, left of the find text; clicking it runs the open
        /// action. Ignored while FindIcon is set.
        public string? FindButtonLabel { get; set; }
        public Color FindTextColor { get; set; } = Color.white;
        /// Amber panel below the find panel; null or empty shows none.
        public string? Warning { get; set; }
        /// Optional active-conflict notice, resolved by FindConflicts and shared
        /// with ClaimSave. Empty/null notices allocate no warning geometry.
        /// Set before opening, together with the translated heading/advice.
        /// Conflicts appear above the artwork; Warning remains below the find panel.
        public WelcomeConflictNotice? ConflictingMods { get; set; }
        public string? ConflictHeading { get; set; }
        public string? ConflictAdvice { get; set; }
        public string? AdditionalConflictAdvice { get; set; }

        // Owner: window. Key: none. Value: the preview PNG loaded from disk
        // (never a ContentFinder asset) and its crop texcoords; window-owned
        // and immutable while open. Dependencies: none (static art).
        // Refresh: loaded once in PreOpen. Teardown: destroyed in PostClose.
        private Texture2D? preview;
        private Rect previewTexCoords;
        private float previewAspect = 1f;

        // Owner: window. Key: compact tile width/height, maximum row-band height
        // and UiRevision.Current. The band depends on screen size and membership.
        // Value: immutable published text/control rectangles and dimensions.
        // Dependencies: those dimensions, active font/UI metrics, constructor
        // text and pre-open adornment/warning properties, loaded preview aspect.
        // Refresh: PreOpen, window membership/resolution events, metric revision
        // in WindowUpdate. Equal keys reuse all geometry. Teardown: window close.
        // Language selection reloads game data/windows; texts belong to this open.
        private string closeLabel = "";
        private float titleHeight;
        private float bodyHeight;
        private float findHeight;
        private float findPanelHeight;
        private float adornmentWidth;
        private float adornmentHeight;
        private float warningHeight;
        private string conflictText = string.Empty;
        private float conflictHeight;
        private float linkWidth;
        private float linkHeight;
        private float windowHeight = 520f;
        private WelcomeTile layoutTile;
        private int metricRevision = -1;
        private float measuredWidth = -1f, measuredHeight = -1f;
        private float measuredMaximumHeight = -1f;
        private Rect titleRect, viewportRect, contentRect, previewRect, bodyRect;
        private Rect findPanelRect, adornmentRect, findLabelRect, warningRect;
        private Rect conflictRect;
        private Rect closeRect, linkRect;
        private Vector2 scroll;
        private bool suspendOnClose;

        /// previewCrop is the art's ink band in the preview file's pixels
        /// (top-left origin); only that band renders.
        public WelcomeDialog(string title, string body, string find,
            string takeMeThere, string previewPath, Rect previewCrop,
            Action open)
        {
            this.title = title;
            this.body = body;
            this.find = find;
            this.takeMeThere = takeMeThere;
            this.previewPath = previewPath;
            this.previewCrop = previewCrop;
            this.open = open;
            forcePause = true;
            absorbInputAroundWindow = false;
            draggable = true;
            closeOnCancel = true;     // ESC while focused
            doCloseButton = false;
        }

        public override Vector2 InitialSize =>
            new Vector2(DialogWidth, windowHeight);

        public override void PreOpen()
        {
            if (ConflictingMods != null && ConflictingMods.Count != 0)
            {
                string advice = ConflictAdvice ?? "RS_CompatibilityRemove".Translate().ToString();
                if (!string.IsNullOrEmpty(AdditionalConflictAdvice)) advice += " " + AdditionalConflictAdvice;
                conflictText = ConflictingMods.Format(
                    ConflictHeading ?? "RS_CompatibilityHeading".Translate().ToString(), advice);
            }
            else conflictText = string.Empty;
            suspendOnClose = false;
            // A suspended instance reloads its owned image. Its availability
            // and aspect may change, even when the restored tile has the same
            // dimensions, so every open starts a new geometry lifetime.
            measuredWidth = measuredHeight = -1f;
            measuredMaximumHeight = -1f;
            metricRevision = -1;
            previewAspect = 1f;
            try
            {
                closeLabel = "CloseButton".Translate();
                preview = LoadPreview(previewPath);
                if (preview != null)
                {
                    float w = preview.width, h = preview.height;
                    previewTexCoords = new Rect(previewCrop.x / w,
                        1f - previewCrop.yMax / h,
                        previewCrop.width / w, previewCrop.height / h);
                    previewAspect = previewCrop.width / previewCrop.height;
                }
                base.PreOpen();
            }
            catch { ReleasePreview(); throw; }
        }

        protected override void SetInitialSizeAndPosition()
        {
            // Shared source is compiled into each mod: compare the stable type
            // name only at this lifecycle boundary, and use Window's virtual API
            // to reflow copies without reflection or shared static registries.
            var windows = Find.WindowStack.Windows;
            int count = 0, index = 0;
            bool present = false;
            for (int i = 0; i < windows.Count; i++)
            {
                Window window = windows[i];
                if (!IsWelcome(window)) continue;
                count++;
                if (window.ID < ID) index++;
                if (ReferenceEquals(window, this)) present = true;
            }
            if (!present) count++;
            WelcomeTilePlan tiles = WelcomeTileLayout.Create(UI.screenWidth, UI.screenHeight, count);
            suspendOnClose = !tiles.TryGetTile(index, out WelcomeTile tile);
            if (suspendOnClose) return;
            layoutTile = tile;
            WelcomeTile fitted = tile.FitContentHeight(windowHeight);
            windowRect = new Rect(fitted.X, fitted.Y, fitted.Width, fitted.Height);
            BuildLayout();
        }

        public override void PostOpen()
        {
            base.PostOpen();
            ReflowWindows();
        }

        private static void ReflowWindows()
        {
            if (suspendedWindows.IsStopped) return;
            WindowStack stack = Find.WindowStack;
            var windows = new List<Window>();
            for (int i = 0; i < stack.Count; i++)
                if (IsWelcome(stack[i])) windows.Add(stack[i]);
            windows.Sort(CompareWindowIds);
            WelcomeTilePlan tiles = WelcomeTileLayout.Create(UI.screenWidth, UI.screenHeight, windows.Count);
            // Snapshot before any removals: closing one copy invokes all four
            // assemblies' removal hooks. Full-capacity checks prevent those
            // hooks from reopening suspended instances during this batch.
            for (int i = 0; i < windows.Count; i++) windows[i].Notify_ResolutionChanged();
            for (int i = windows.Count - 1; i >= tiles.Count; i--) windows[i].Close(false);
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            UiRevision.ObserveCurrentMetrics();
            if (metricRevision != UiRevision.Current) BuildLayout();
        }

        private void BuildLayout()
        {
            UiRevision.ObserveCurrentMetrics();
            float width = layoutTile.Width - Margin * 2f;
            float compactHeight = layoutTile.Height - Margin * 2f;
            int revision = UiRevision.Current;
            if (measuredWidth == width && measuredHeight == compactHeight
                && measuredMaximumHeight == layoutTile.MaximumHeight && metricRevision == revision) return;
            measuredWidth = width;
            measuredHeight = compactHeight;
            measuredMaximumHeight = layoutTile.MaximumHeight;
            metricRevision = revision;
            float contentWidth = Mathf.Max(1f, width - 20f);
            float buttonHeight, closeWidth;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Medium;
                Text.WordWrap = true;
                titleHeight = Mathf.Max(Mathf.Ceil(Text.LineHeight), Mathf.Ceil(Text.CalcHeight(title, width)));
                Text.Font = GameFont.Small;
                float lineHeight = Mathf.Ceil(Text.LineHeight);
                buttonHeight = Mathf.Max(ButtonHeight, lineHeight + 12f);
                Text.WordWrap = false;
                closeWidth = Mathf.Min(width, Mathf.Max(100f, WrText.MeasureFitWidth(closeLabel) + 24f));
                adornmentWidth = adornmentHeight = 0f;
                if (FindIcon != null)
                {
                    adornmentWidth = IconSize;
                    adornmentHeight = IconSize;
                }
                else if (!string.IsNullOrEmpty(FindButtonLabel))
                {
                    adornmentWidth = Mathf.Min(contentWidth / 2f, Mathf.Max(100f,
                        Mathf.Ceil(Text.CalcSize(FindButtonLabel).x) + 20f));
                    adornmentHeight = buttonHeight;
                }
                // Fit width: an exact CalcSize rect wraps at fractional UI
                // scales (seen at 1.5).
                linkWidth = WrText.MeasureFitWidth(takeMeThere);
                Text.WordWrap = true;
                linkHeight = Mathf.Max(lineHeight, Mathf.Ceil(Text.CalcHeight(takeMeThere, Mathf.Min(linkWidth, width))));
                bodyHeight = Mathf.Ceil(Text.CalcHeight(body, contentWidth));
                findHeight = Mathf.Ceil(Text.CalcHeight(find, FindTextWidth(contentWidth)));
                warningHeight = string.IsNullOrEmpty(Warning) ? 0f
                    : Mathf.Ceil(Text.CalcHeight(Warning, contentWidth - PanelPad * 2f));
                conflictHeight = conflictText.Length == 0 ? 0f
                    : Mathf.Ceil(Text.CalcHeight(conflictText, contentWidth - PanelPad * 2f));
            }
            bool stacked = closeWidth + linkWidth + Gap > width;
            float footerHeight = stacked ? buttonHeight + Gap + linkHeight : Mathf.Max(buttonHeight, linkHeight);
            // Keep the existing artwork footprint while adding space for text.
            // Growing the art with the viewport would consume the extra room.
            float compactViewportHeight = Mathf.Max(24f,
                compactHeight - titleHeight - footerHeight - Gap * 2f);
            findPanelHeight = Mathf.Max(adornmentHeight, findHeight)
                + PanelPad * 2f;
            float y = 0f;
            if (conflictHeight > 0f)
            {
                conflictRect = new Rect(0f, y, contentWidth, conflictHeight + PanelPad * 2f);
                y += conflictRect.height + Gap;
            }
            if (preview != null)
            {
                float h = Mathf.Min(Mathf.Min(PreviewWidth, contentWidth) / previewAspect,
                    Mathf.Max(64f, compactViewportHeight * 0.4f));
                float w = h * previewAspect;
                previewRect = new Rect((contentWidth - w) / 2f, y, w, h);
                y += h + Gap;
            }
            bodyRect = new Rect(0f, y, contentWidth, bodyHeight);
            y += bodyHeight + Gap;
            findPanelRect = new Rect(0f, y, contentWidth, findPanelHeight);
            adornmentRect = new Rect(PanelPad, y + (findPanelHeight - adornmentHeight) / 2f,
                adornmentWidth, adornmentHeight);
            float findWidth = FindTextWidth(contentWidth);
            findLabelRect = new Rect(contentWidth - PanelPad - findWidth,
                y + (findPanelHeight - findHeight) / 2f, findWidth, findHeight);
            y += findPanelHeight + Gap;
            warningRect = new Rect(0f, y, contentWidth, warningHeight + PanelPad * 2f);
            if (warningHeight > 0f) y += warningRect.height + Gap;
            contentRect = new Rect(0f, 0f, contentWidth, y);

            WelcomeTile fitted = layoutTile.FitContentHeight(
                y + titleHeight + footerHeight + Gap * 2f + Margin * 2f);
            windowRect.y -= (fitted.Height - windowRect.height) / 2f;
            windowRect.height = windowHeight = fitted.Height;
            windowRect = windowRect.Rounded();
            float height = fitted.Height - Margin * 2f;
            titleRect = new Rect(0f, 0f, width, titleHeight);
            viewportRect = new Rect(0f, titleHeight + Gap, width,
                Mathf.Max(24f, height - titleHeight - footerHeight - Gap * 2f));
            closeRect = new Rect(0f, height - footerHeight, closeWidth, buttonHeight);
            linkRect = stacked
                ? new Rect(0f, height - linkHeight, width, linkHeight)
                : new Rect(width - linkWidth, height - footerHeight + (footerHeight - linkHeight) / 2f, linkWidth, linkHeight);
            scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, y - viewportRect.height));
        }

        public override void PostClose()
        {
            base.PostClose();
            ReleasePreview();
            if (suspendedWindows.IsStopped) return;
            if (suspendOnClose)
            {
                suspendOnClose = false;
                RimWorld.Planet.World? world = Find.World;
                if (world != null && !Suppressed)
                    suspendedWindows.Add(world.info.persistentRandomValue.ToString(), this);
                return;
            }
            ReflowWindows();
        }

        private void ReleasePreview()
        {
            if (preview == null) return;
            UnityEngine.Object.Destroy(preview);
            preview = null;
        }

        public override void DoWindowContents(Rect inRect)
        {
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Medium;
                Text.WordWrap = true;
                Widgets.Label(titleRect, title);
            }
            bool openClicked = false;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                Widgets.BeginScrollView(viewportRect, ref scroll, contentRect);
                try
                {
                    if (conflictHeight > 0f)
                    {
                        Widgets.DrawBoxSolidWithOutline(conflictRect, WarningBackground, WarningOutline);
                        Color previous = GUI.color;
                        GUI.color = WarningText;
                        Widgets.Label(conflictRect.ContractedBy(PanelPad), conflictText);
                        GUI.color = previous;
                    }
                    if (preview != null)
                        GUI.DrawTextureWithTexCoords(previewRect, preview, previewTexCoords);
                    Widgets.Label(bodyRect, body);
                    Widgets.DrawMenuSection(findPanelRect);
                    if (FindIcon != null) GUI.DrawTexture(adornmentRect, FindIcon);
                    else if (adornmentWidth > 0f)
                        openClicked = Widgets.ButtonTextSubtle(adornmentRect, FindButtonLabel!);
                    GUI.color = FindTextColor;
                    Widgets.Label(findLabelRect, find);
                    if (warningHeight > 0f)
                    {
                        Widgets.DrawBoxSolidWithOutline(warningRect, WarningBackground, WarningOutline);
                        GUI.color = WarningText;
                        Widgets.Label(warningRect.ContractedBy(PanelPad), Warning!);
                    }
                }
                finally { Widgets.EndScrollView(); }
            }

            // Bottom row: Close on the left, the link on the right.
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = false;
                if (Widgets.ButtonText(closeRect, closeLabel)) Close();
            }

            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                Text.WordWrap = true;
                GUI.color = Mouse.IsOver(linkRect) ? LinkHoverColor : LinkColor;
                Widgets.Label(linkRect, takeMeThere);
            }
            if (Widgets.ButtonInvisible(linkRect) || openClicked)
            {
                Close();
                open();
            }
        }

        private float FindTextWidth(float width) =>
            width - PanelPad * 2f
                - (adornmentWidth > 0f ? adornmentWidth + Gap : 0f);

        private static Texture2D? LoadPreview(string path)
        {
            Texture2D? texture = null;
            try
            {
                if (!File.Exists(path)) return null;
                byte[] bytes = File.ReadAllBytes(path);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32,
                    mipChain: false);
                if (texture.LoadImage(bytes))
                {
                    texture.name = "WelcomePreview";
                    Texture2D result = texture;
                    texture = null;
                    return result;
                }
            }
            catch (IOException)
            {
                // Unreadable art degrades to a text-only welcome.
            }
            finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
            return null;
        }
    }

    // This runs before vanilla snapshots/iterates the live WindowStack. Each
    // compiled copy has an allocation-free dimension gate, and resumes only
    // its own save-scoped queue. No hidden window remains in the live stack.
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
    internal static class Patch_WelcomeDisplayChanged
    {
        private static void Prefix() => WelcomeDialog.ObserveScreen();
    }

    /// Opens the welcome a scenario opening held back, as the opening
    /// closes (vanilla's start dialog and Immersive Opening both call this).
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Notify_GameStartDialogClosed))]
    internal static class Patch_GameStartDialogClosed
    {
        private static void Postfix() => WelcomeDialog.OpeningClosed();
    }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.TryRemove), new[] { typeof(Window), typeof(bool) })]
    internal static class Patch_WelcomeRemoved
    {
        private static void Postfix(Window window, bool __result)
        {
            if (__result && WelcomeDialog.IsWelcome(window)) WelcomeDialog.SpaceAvailable();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Dispose))]
    internal static class Patch_WelcomeGameDisposed
    {
        // Every assembly stops admission in its prefix before any finalizer
        // closes a window. Removal hooks in other compiled copies therefore
        // cannot reopen a queued welcome or claim an unseen one during exit.
        // Finalizers also release previews if vanilla disposal throws.
        private static void Prefix(Game __instance, out bool __state)
        {
            __state = ReferenceEquals(Current.Game, __instance);
            if (__state) WelcomeDialog.BeginDisposal();
        }
        private static void Finalizer(bool __state)
        {
            if (__state) WelcomeDialog.CloseDisposedWindows();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    internal static class Patch_WelcomeGameStarted
    {
        private static void Prefix(Game __instance)
        {
            // FinalizeInit precedes both StartedNewGame and LoadedGame's
            // deferred welcome actions. This resets primitive/managed state
            // only and is safe on the game's loading thread.
            if (ReferenceEquals(Current.Game, __instance)) WelcomeDialog.BeginGame();
        }
    }
}
