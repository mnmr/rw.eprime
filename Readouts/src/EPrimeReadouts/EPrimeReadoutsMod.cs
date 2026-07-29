using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts
{
    public class EPrimeReadoutsMod : Mod
    {
        public static ReadoutSettings Settings;

        /// The mod's content pack — used to locate shipped data files (Seed/).
        public static ModContentPack ContentPack;

        public EPrimeReadoutsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ReadoutSettings>();
            ContentPack = content;
            new Harmony("mnmr.eprimereadouts").PatchAll();
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
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("EPR.UseVanilla".Translate(), ref Settings.useVanillaReadout);
            listing.Label("EPR.OffsetX".Translate() + ": " + (int)Settings.offsetX);
            Settings.offsetX = listing.Slider(Settings.offsetX, 0f, 200f);
            listing.Label("EPR.OffsetY".Translate() + ": " + (int)Settings.offsetY);
            Settings.offsetY = listing.Slider(Settings.offsetY, 0f, 200f);
            listing.Label("EPR.PanelWidth".Translate() + ": " + (int)Settings.panelWidth);
            Settings.panelWidth = listing.Slider(Settings.panelWidth, 80f, 400f);
            listing.Label("EPR.BottomMargin".Translate() + ": " + (int)Settings.bottomMargin);
            Settings.bottomMargin = listing.Slider(Settings.bottomMargin, 0f, 500f);
            listing.End();
        }
    }
}
