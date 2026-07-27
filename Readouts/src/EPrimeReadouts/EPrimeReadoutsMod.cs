using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts
{
    public class EPrimeReadoutsMod : Mod
    {
        public static ReadoutSettings Settings;

        public EPrimeReadoutsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ReadoutSettings>();
            new Harmony("mnmr.eprimereadouts").PatchAll();
        }

        public static void Persist(Action<ReadoutSettings> change)
        {
            change(Settings);
            Settings.Write();
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
