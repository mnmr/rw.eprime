using System.IO;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// One-time welcome, shown once per player per save (the game component
    /// marks it seen the moment it opens): the mod's name, preview art, a
    /// short description, and a framed pointer to the gear button that
    /// opens the editor. Close and "Take me there now" both dismiss it; the
    /// latter also opens the editor. ESC closes it via the standard cancel
    /// path while it has focus.
    public class Dialog_ReadoutsWelcome : Window
    {
        private const float PreviewWidth = 520f;
        private const float IconSize = 48f;
        private const float Gap = 12f;
        private const float PanelPad = 10f;

        // The preview art's ink band in the 1280x720 About/Preview.png: the
        // readout bands run from y 60 to 660, so trim the empty margins to a
        // 30px pad on each side. Drawn via texcoords so the trimmed rows
        // never render.
        private const float CropTop = 30f / 720f;
        private const float CropBottom = 690f / 720f;
        private static readonly Rect PreviewTexCoords = new Rect(
            0f, 1f - CropBottom, 1f, CropBottom - CropTop);
        private const float PreviewAspect = 1280f / (720f * (CropBottom - CropTop));

        private static readonly Color LinkColor = new Color(0.45f, 0.7f, 1f);
        private static readonly Color LinkHoverColor =
            new Color(0.65f, 0.85f, 1f);

        // Owner: window. Key: none. Value: the About/Preview.png texture
        // loaded from disk (never a ContentFinder asset); window-owned and
        // immutable while open. Dependencies: none (static art). Refresh:
        // loaded once in PreOpen. Teardown: destroyed in PostClose.
        private Texture2D? preview;

        // Translated strings, wrapped-text measurements, and the resulting
        // window height, resolved once per open in PreOpen BEFORE the base
        // call positions the window (a builder boundary, never the render
        // pass); the language cannot change while the dialog is open.
        private string title = "";
        private string body = "";
        private string find = "";
        private string takeMeThere = "";
        private string closeLabel = "";
        private float bodyHeight;
        private float findHeight;
        private float findPanelHeight;
        private float linkWidth;
        private float windowHeight = 520f;

        public Dialog_ReadoutsWelcome()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnCancel = true;     // ESC while focused
            doCloseButton = false;
        }

        public override Vector2 InitialSize => new Vector2(600f, windowHeight);

        public override void PreOpen()
        {
            title = "EPR.WelcomeTitle".Translate();
            body = "EPR.WelcomeBody".Translate();
            find = "EPR.WelcomeFind".Translate();
            takeMeThere = "EPR.WelcomeTakeMeThere".Translate();
            closeLabel = "CloseButton".Translate();
            preview = LoadPreview();

            float width = 600f - Margin * 2f;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                bodyHeight = Mathf.Ceil(Text.CalcHeight(body, width));
                findHeight = Mathf.Ceil(Text.CalcHeight(
                    find, width - PanelPad * 2f - IconSize - Gap));
                Text.WordWrap = false;
                linkWidth = Mathf.Ceil(Text.CalcSize(takeMeThere).x);
            }
            findPanelHeight = Mathf.Max(IconSize, findHeight) + PanelPad * 2f;
            float previewHeight = preview != null
                ? Mathf.Ceil(Mathf.Min(PreviewWidth, width) / PreviewAspect) + Gap
                : 0f;
            windowHeight = Margin * 2f + 38f + previewHeight
                + bodyHeight + Gap + findPanelHeight + Gap + 35f;

            base.PreOpen();
        }

        public override void PostClose()
        {
            base.PostClose();
            if (preview != null)
            {
                Object.Destroy(preview);
                preview = null;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), title);
            }
            y += 38f;

            if (preview != null)
            {
                float drawWidth = Mathf.Min(PreviewWidth, inRect.width);
                float drawHeight = Mathf.Ceil(drawWidth / PreviewAspect);
                GUI.DrawTextureWithTexCoords(new Rect(
                        inRect.x + (inRect.width - drawWidth) / 2f, y,
                        drawWidth, drawHeight),
                    preview, PreviewTexCoords);
                y += drawHeight + Gap;
            }

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
                GUI.DrawTexture(new Rect(findRect.x + PanelPad,
                        findRect.y + (findRect.height - IconSize) / 2f,
                        IconSize, IconSize),
                    ReadoutTextures.Gear);
                GUI.color = EprStyle.SelectionTint;
                Widgets.Label(new Rect(findRect.x + PanelPad + IconSize + Gap,
                        findRect.y + (findRect.height - findHeight) / 2f,
                        findRect.width - PanelPad * 2f - IconSize - Gap,
                        findHeight),
                    find);
            }

            // Bottom row: Close on the left, the link on the right.
            var closeRect = new Rect(inRect.x, inRect.yMax - 35f, 140f, 35f);
            if (Widgets.ButtonText(closeRect, closeLabel))
                Close();

            var linkRect = new Rect(inRect.xMax - linkWidth,
                inRect.yMax - 35f + (35f - 22f) / 2f, linkWidth, 22f);
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = Mouse.IsOver(linkRect) ? LinkHoverColor : LinkColor;
                Widgets.Label(linkRect, takeMeThere);
            }
            if (Widgets.ButtonInvisible(linkRect))
            {
                Close();
                Find.WindowStack.Add(new Dialog_ReadoutConfig());
            }
        }

        private static Texture2D? LoadPreview()
        {
            string path = Path.Combine(
                Path.Combine(EPrimeReadoutsMod.ContentRootDir, "About"),
                "Preview.png");
            try
            {
                if (!File.Exists(path)) return null;
                byte[] bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32,
                    mipChain: false);
                if (texture.LoadImage(bytes))
                {
                    texture.name = "EPrimeReadoutsWelcomePreview";
                    return texture;
                }
                Object.Destroy(texture);
            }
            catch (IOException)
            {
                // Unreadable art degrades to a text-only welcome.
            }
            return null;
        }
    }
}
