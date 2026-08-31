using System;
using HarmonyLib;
using EPrimeReadouts.UI;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts
{
    public class EPrimeReadoutsMod : Mod
    {
        public static ReadoutSettings Settings = null!; // assigned in the mod constructor at startup
        private readonly Listing_Standard settingsListing = new Listing_Standard();

        /// The mod's content pack — used to locate shipped data files (Seed/).
        public static ModContentPack? ContentPack;

        // Cache contract:
        // Owner: this Mod/settings-window instance.
        // Key: language revision plus the four displayed integer setting values.
        // Value: immutable formatted label strings.
        // Dependencies: active language and displayed slider values only.
        // Refresh policy: immediate when an exact dependency changes.
        // Equality policy: unchanged values preserve string references.
        // Teardown: bounded fields die with the Mod instance; no resources owned.
        private int settingsTextLanguageVersion = -1;
        private int textOffsetX = int.MinValue;
        private int textOffsetY = int.MinValue;
        private int textPanelWidth = int.MinValue;
        private int textBottomMargin = int.MinValue;
        private string? offsetXLabel;
        private string? offsetYLabel;
        private string? panelWidthLabel;
        private string? bottomMarginLabel;

        public EPrimeReadoutsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ReadoutSettings>();
            ContentPack = content;
            var harmony = new Harmony("mnmr.eprimereadouts");
            MouseFocusTextField.Initialize(harmony);
            harmony.PatchAll();
        }

        /// Applies a settings change and writes to disk — deferred while any
        /// Scribe operation is active, because ModSettings.Write() spins up
        /// its own ScribeSaver and vanilla force-stops whatever load/save is
        /// in flight when that happens.
        public static void Persist(Action<ReadoutSettings> change)
        {
            change(Settings);
            if (Scribe.mode == LoadSaveMode.Inactive) Settings.Write();
            else LongEventHandler.ExecuteWhenFinished(Settings.Write);
        }

        public override string SettingsCategory() => "EPrime's Readouts";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            using (GuiStateScope.Capture())
            {
            EnsureSettingsText();
            Listing_Standard listing = settingsListing;
            listing.Begin(inRect);
            try
            {
            listing.CheckboxLabeled(UiText.Get("EPR.UseVanilla"), ref Settings.useVanillaReadout);
            listing.Label(offsetXLabel);
            Settings.offsetX = listing.Slider(Settings.offsetX, 0f, 200f);
            listing.Label(offsetYLabel);
            Settings.offsetY = listing.Slider(Settings.offsetY, 0f, 200f);
            listing.Label(panelWidthLabel);
            Settings.panelWidth = listing.Slider(Settings.panelWidth, 80f, 400f);
            listing.Label(bottomMarginLabel);
            Settings.bottomMargin = listing.Slider(Settings.bottomMargin, 0f, 500f);
            }
            finally
            {
            listing.End();
            }
            }
        }

        private void EnsureSettingsText()
        {
            UiVersion.ObserveCurrentMetrics();
            int offsetX = (int)Settings.offsetX;
            int offsetY = (int)Settings.offsetY;
            int panelWidth = (int)Settings.panelWidth;
            int bottomMargin = (int)Settings.bottomMargin;
            if (settingsTextLanguageVersion == UiVersion.LanguageCurrent
                && textOffsetX == offsetX
                && textOffsetY == offsetY
                && textPanelWidth == panelWidth
                && textBottomMargin == bottomMargin)
                return;
            offsetXLabel = UiText.Get("EPR.OffsetX") + ": " + offsetX;
            offsetYLabel = UiText.Get("EPR.OffsetY") + ": " + offsetY;
            panelWidthLabel = UiText.Get("EPR.PanelWidth") + ": " + panelWidth;
            bottomMarginLabel = UiText.Get("EPR.BottomMargin") + ": " + bottomMargin;
            settingsTextLanguageVersion = UiVersion.LanguageCurrent;
            textOffsetX = offsetX;
            textOffsetY = offsetY;
            textPanelWidth = panelWidth;
            textBottomMargin = bottomMargin;
        }
    }
}
