using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    /// Implanner as a bottom-bar main tab (ImplannerSettings.entryStyle Tab
    /// or Both): the shared resizable tab hosting one ImplannerView,
    /// anchored bottom-right under its icon button in the bar's right-hand
    /// group, default-sized like the floating dialog and growable to the
    /// screen from its top-left grip. Opening it closes the floating
    /// dialog; the view's selected tab carries over.
    public class MainTabWindow_Implanner : ResizableMainTabWindow
    {
        private static readonly Vector2 DefaultSize = new Vector2(1220f, 760f);

        private readonly ImplannerView view;

        public MainTabWindow_Implanner()
        {
            view = new ImplannerView(this);
        }

        public override MainTabWindowAnchor Anchor => MainTabWindowAnchor.Right;

        protected override Vector2 TargetSize() => DefaultSize;

        protected override Vector2 MinSize => new Vector2(
            Mathf.Min(DefaultSize.x, Verse.UI.screenWidth),
            Mathf.Min(DefaultSize.y, Verse.UI.screenHeight - BottomBarHeight));

        /// The player's remembered tab size lives in the mod settings
        /// (0 = default per axis); the write is deferred to the frame path.
        private sealed class SizePreferenceAdapter : IWindowSizePreference
        {
            public float Width
            {
                get => ImplannerMod.Settings.tabWindowWidth;
                set => ImplannerMod.Settings.tabWindowWidth = value;
            }

            public float Height
            {
                get => ImplannerMod.Settings.tabWindowHeight;
                set => ImplannerMod.Settings.tabWindowHeight = value;
            }

            public void RequestWrite() => ImplannerGameComponent.RequestSettingsWrite();
        }

        private static readonly SizePreferenceAdapter sizePreference = new SizePreferenceAdapter();
        protected override IWindowSizePreference? SizePreference => sizePreference;
        protected override string GripTipKey => "IMP_ResizeGripTip";
        protected override int GripWindowId => 173350020;

        public override void PreOpen()
        {
            base.PreOpen();
            ImplannerEntry.CloseDialog();
            view.Open();
        }

        public override void PostClose()
        {
            base.PostClose();
            view.Close();
        }

        protected override void DrawTabContents(Rect inRect) => view.Draw(inRect);

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            view.Update(windowRect.width - 2f * Margin);
        }
    }
}
