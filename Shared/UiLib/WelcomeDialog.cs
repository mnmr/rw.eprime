using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
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
            RimWorld.Planet.World? world = Find.World;
            if (world == null || Suppressed) return false;
            if (!openingClosing
                && Find.WindowStack?.SecondsSinceClosedGameStartDialog == 0f)
            {
                retryAfterOpening = retry;
                return false;
            }
            retryAfterOpening = null;
            string id = world.info.persistentRandomValue.ToString();
            if (shownSaves.Contains(id)) return false;
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

        // True while OpeningClosed runs the retry: for the rest of the
        // closing frame the stack still reports 0 seconds since the close.
        private static bool openingClosing;

        internal static void OpeningClosed()
        {
            Action? retry = retryAfterOpening;
            if (retry == null) return;
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

        // Owner: window. Key: none. Value: the preview PNG loaded from disk
        // (never a ContentFinder asset) and its crop texcoords; window-owned
        // and immutable while open. Dependencies: none (static art).
        // Refresh: loaded once in PreOpen. Teardown: destroyed in PostClose.
        private Texture2D? preview;
        private Rect previewTexCoords;
        private float previewAspect = 1f;

        // Wrapped-text measurements and the resulting window height,
        // resolved once per open in PreOpen BEFORE the base call positions
        // the window (a builder boundary, never the render pass); the
        // language and UI scale cannot change while the dialog is open.
        private string closeLabel = "";
        private float titleHeight;
        private float bodyHeight;
        private float findHeight;
        private float findPanelHeight;
        private float adornmentWidth;
        private float adornmentHeight;
        private float warningHeight;
        private float linkWidth;
        private float linkHeight;
        private float windowHeight = 520f;

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
            absorbInputAroundWindow = true;
            closeOnCancel = true;     // ESC while focused
            doCloseButton = false;
        }

        public override Vector2 InitialSize =>
            new Vector2(DialogWidth, windowHeight);

        public override void PreOpen()
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

            float width = DialogWidth - Margin * 2f;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = false;
                if (FindIcon != null)
                {
                    adornmentWidth = IconSize;
                    adornmentHeight = IconSize;
                }
                else if (!string.IsNullOrEmpty(FindButtonLabel))
                {
                    adornmentWidth = Mathf.Max(100f,
                        Mathf.Ceil(Text.CalcSize(FindButtonLabel).x) + 20f);
                    adornmentHeight = ButtonHeight;
                }
                // Fit width: an exact CalcSize rect wraps at fractional UI
                // scales (seen at 1.5).
                linkWidth = WrText.MeasureFitWidth(takeMeThere);
                linkHeight = Mathf.Max(22f,
                    Mathf.Ceil(Text.LineHeightOf(GameFont.Small)));
                titleHeight = Mathf.Max(34f,
                    Mathf.Ceil(Text.LineHeightOf(GameFont.Medium)));
                Text.WordWrap = true;
                bodyHeight = Mathf.Ceil(Text.CalcHeight(body, width));
                findHeight = Mathf.Ceil(Text.CalcHeight(find, FindTextWidth(width)));
                warningHeight = string.IsNullOrEmpty(Warning) ? 0f
                    : Mathf.Ceil(Text.CalcHeight(Warning, width - PanelPad * 2f));
            }
            findPanelHeight = Mathf.Max(adornmentHeight, findHeight)
                + PanelPad * 2f;
            float previewHeight = preview != null
                ? Mathf.Ceil(Mathf.Min(PreviewWidth, width) / previewAspect) + Gap
                : 0f;
            float warningBlock = warningHeight > 0f
                ? warningHeight + PanelPad * 2f + Gap
                : 0f;
            windowHeight = Margin * 2f + 38f + previewHeight
                + bodyHeight + Gap + findPanelHeight + Gap + warningBlock + 35f;

            base.PreOpen();
        }

        public override void PostClose()
        {
            base.PostClose();
            if (preview != null)
            {
                UnityEngine.Object.Destroy(preview);
                preview = null;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inRect.x, y, inRect.width, titleHeight),
                    title);
            }
            y += 38f;

            if (preview != null)
            {
                float drawWidth = Mathf.Min(PreviewWidth, inRect.width);
                float drawHeight = Mathf.Ceil(drawWidth / previewAspect);
                GUI.DrawTextureWithTexCoords(new Rect(
                        inRect.x + (inRect.width - drawWidth) / 2f, y,
                        drawWidth, drawHeight),
                    preview, previewTexCoords);
                y += drawHeight + Gap;
            }

            bool openClicked = false;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                Widgets.Label(new Rect(inRect.x, y, inRect.width, bodyHeight),
                    body);
                y += bodyHeight + Gap;

                var findRect = new Rect(inRect.x, y, inRect.width,
                    findPanelHeight);
                Widgets.DrawMenuSection(findRect);
                var adornment = new Rect(findRect.x + PanelPad,
                    findRect.y + (findRect.height - adornmentHeight) / 2f,
                    adornmentWidth, adornmentHeight);
                if (FindIcon != null)
                    GUI.DrawTexture(adornment, FindIcon);
                else if (adornmentWidth > 0f)
                    openClicked = Widgets.ButtonTextSubtle(adornment,
                        FindButtonLabel!); // width > 0 ⇒ label set
                float findWidth = FindTextWidth(inRect.width);
                GUI.color = FindTextColor;
                Widgets.Label(new Rect(findRect.xMax - PanelPad - findWidth,
                        findRect.y + (findRect.height - findHeight) / 2f,
                        findWidth, findHeight),
                    find);
                y += findPanelHeight + Gap;

                if (warningHeight > 0f)
                {
                    var warningRect = new Rect(inRect.x, y, inRect.width,
                        warningHeight + PanelPad * 2f);
                    Widgets.DrawBoxSolidWithOutline(warningRect,
                        WarningBackground, WarningOutline);
                    GUI.color = WarningText;
                    Widgets.Label(warningRect.ContractedBy(PanelPad), Warning!); // height > 0 ⇒ set
                }
            }

            // Bottom row: Close on the left, the link on the right.
            var closeRect = new Rect(inRect.x, inRect.yMax - 35f, 140f, 35f);
            if (Widgets.ButtonText(closeRect, closeLabel))
                Close();

            var linkRect = new Rect(inRect.xMax - linkWidth,
                inRect.yMax - 35f + (35f - linkHeight) / 2f,
                linkWidth, linkHeight);
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                Text.WordWrap = false;
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
            try
            {
                if (!File.Exists(path)) return null;
                byte[] bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32,
                    mipChain: false);
                if (texture.LoadImage(bytes))
                {
                    texture.name = "WelcomePreview";
                    return texture;
                }
                UnityEngine.Object.Destroy(texture);
            }
            catch (IOException)
            {
                // Unreadable art degrades to a text-only welcome.
            }
            return null;
        }
    }

    /// Opens the welcome a scenario opening held back, as the opening
    /// closes (vanilla's start dialog and Immersive Opening both call this).
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Notify_GameStartDialogClosed))]
    internal static class Patch_GameStartDialogClosed
    {
        private static void Postfix() => WelcomeDialog.OpeningClosed();
    }
}
