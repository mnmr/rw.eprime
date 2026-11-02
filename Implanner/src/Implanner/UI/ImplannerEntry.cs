using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    /// The two ways into Implanner and the player's choice between them
    /// (ImplannerSettings.entryStyle): the toolbar button opens the floating
    /// Dialog_Implanner, the bottom-bar main button opens
    /// MainTabWindow_Implanner. At most one host is open at a time; both
    /// host the same ImplannerView, whose selected tab is static, so a style
    /// change swaps the host under the same tab.
    // Cache contract:
    // Owner: process.
    // Key: none.
    // Value: the mod's MainButtonDef and its hotkey def, resolved once by
    //   defName after defs load (static per session); never a game object.
    // Dependencies: the loaded definition set.
    // Refresh policy: resolved in the static constructor.
    // Equality policy: never changes within a session.
    // Teardown: none (process-lifetime defs).
    [StaticConstructorOnStartup]
    internal static class ImplannerEntry
    {
        internal const string MainButtonDefName = "Implanner";
        internal const string HotKeyDefName = "Implanner_MainTab";

        private static readonly MainButtonDef? button;

        static ImplannerEntry()
        {
            button = DefDatabase<MainButtonDef>.GetNamedSilentFail(MainButtonDefName);
            // A rebindable key with no default: the game generates main-tab
            // bindings only for defs declaring a default key, and every free
            // key collides with some other mod. Listed under Main tabs in
            // the keyboard options.
            KeyBindingDef? key = DefDatabase<KeyBindingDef>.GetNamedSilentFail(HotKeyDefName);
            if (button != null && key != null) button.hotKey = key;
            ApplyStyle();
        }

        internal static EntryStyle Style => ImplannerMod.Settings.entryStyle;

        /// The bar button follows the style; vanilla reads buttonVisible
        /// every draw, so no other invalidation is needed.
        private static void ApplyStyle()
        {
            if (button != null) button.buttonVisible = Style != EntryStyle.Toolbar;
        }

        private static bool TabOpen =>
            button != null && Current.ProgramState == ProgramState.Playing
            && Find.MainTabsRoot.OpenTab == button;

        private static Dialog_Implanner? OpenDialogWindow =>
            Find.WindowStack?.WindowOfType<Dialog_Implanner>();

        /// Opens the host the style prefers (welcome dialog, hotkey in
        /// toolbar style).
        internal static void Open()
        {
            if (Style == EntryStyle.Tab) OpenTab();
            else OpenDialog();
        }

        /// The toolbar button: closes the open host or opens the preferred one.
        internal static void Toggle()
        {
            if (Style == EntryStyle.Tab)
            {
                if (TabOpen) CloseTab();
                else OpenTab();
                return;
            }
            Dialog_Implanner? dialog = OpenDialogWindow;
            if (dialog != null) dialog.Close();
            else OpenDialog();
        }

        internal static void OpenTab()
        {
            if (button != null && Current.ProgramState == ProgramState.Playing)
                Find.MainTabsRoot.SetCurrentTab(button);
        }

        internal static void CloseTab()
        {
            if (TabOpen) Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
        }

        internal static void OpenDialog()
        {
            if (OpenDialogWindow == null) Find.WindowStack.Add(new Dialog_Implanner());
        }

        internal static void CloseDialog() => OpenDialogWindow?.Close(doCloseSound: false);

        /// Pending host swap after a style change, applied from the frame
        /// path (ImplannerGameComponent.GameComponentUpdate), never inside
        /// the OnGUI pass that clicked the control: the clicking host is
        /// still drawing from the snapshots its close would release.
        private static EntryStyle? pendingSwap;

        /// Sets the style and schedules the host swap it implies: the open
        /// dialog becomes the tab (or the tab the dialog) under the same
        /// selected view tab. No-op for the current style.
        internal static void SetStyle(EntryStyle style)
        {
            ImplannerSettings settings = ImplannerMod.Settings;
            if (settings.entryStyle == style) return;
            settings.entryStyle = style;
            ApplyStyle();
            ImplannerGameComponent.RequestSettingsWrite();
            pendingSwap = style;
        }

        /// Frame path.
        internal static void DrainPending()
        {
            if (pendingSwap == null) return;
            EntryStyle style = pendingSwap.Value;
            pendingSwap = null;
            if (Current.ProgramState != ProgramState.Playing) return;
            if (style == EntryStyle.Tab && OpenDialogWindow != null)
            {
                CloseDialog();
                OpenTab();
            }
            else if (style == EntryStyle.Toolbar && TabOpen)
            {
                CloseTab();
                OpenDialog();
            }
        }

        /// Segment order of the style control (PlannerLabels.EntryStyles):
        /// toolbar button, main tab, both.
        private static readonly EntryStyle[] StyleBySegment =
            { EntryStyle.Toolbar, EntryStyle.Tab, EntryStyle.Both };

        private static int SegmentOf(EntryStyle style)
        {
            for (int i = 0; i < StyleBySegment.Length; i++)
                if (StyleBySegment[i] == style) return i;
            return -1;
        }

        /// The style control: a caption line over a three-segment row,
        /// shared by the Options tab and the mod settings page. Heights
        /// come from the caller's measured text geometry.
        internal static void DrawStyleRow(float x, ref float y, float width,
            float captionHeight, float segmentHeight, WrTip? tip)
        {
            var caption = new Rect(x, y, width, captionHeight);
            tip?.Region(caption);
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = true;
                Widgets.Label(caption, PlannerLabels.OptEntryStyle);
            }
            y += captionHeight + 2f;
            int active = SegmentOf(Style);
            int clicked = SegmentedControl.Row(new Rect(x, y, width, segmentHeight),
                PlannerLabels.EntryStyles, active);
            if (clicked >= 0 && clicked != active)
                SetStyle(StyleBySegment[clicked]);
            y += segmentHeight + 2f;
        }
    }

    /// The bar button's worker: in toolbar style (bar button hidden) the
    /// hotkey still works and toggles the floating dialog instead.
    public class MainButtonWorker_Implanner : MainButtonWorker_ToggleTab
    {
        public override void Activate()
        {
            if (ImplannerEntry.Style == EntryStyle.Toolbar) ImplannerEntry.Toggle();
            else base.Activate();
        }
    }
}
