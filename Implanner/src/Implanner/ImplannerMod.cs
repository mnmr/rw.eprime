using HarmonyLib;
using UnityEngine;
using Verse;

namespace Implanner
{
    public class ImplannerMod : Mod
    {
        // Initialized by the Mod constructor before any game code can run.
        // RimWorld constructs Mod subclasses during the earliest loading phase,
        // so Settings and Instance are always non-null by the time patches or
        // game components execute.
        public static ImplannerMod Instance = null!;
        public static ImplannerSettings Settings = null!;

        /// The mod's install directory; the Help tab loads its topic files
        /// and images from here.
        public static string ContentRootDir = "";

        // Cache contract:
        // Owner: process (the settings window has no instance state).
        // Key: none (one label).
        // Value: the translated toolbar-button setting label (immutable).
        // Dependencies: UiVersion.LanguageCurrent.
        // Refresh policy: immediate on the first draw after the language
        //   revision moves.
        // Equality policy: an unchanged revision reuses the string.
        // Teardown: none needed (one bounded string for the process).
        private static string showToolbarLabel = "";
        private static int showToolbarLabelStamp = -1;

        public ImplannerMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ImplannerSettings>();
            ContentRootDir = content.RootDir;
            new Harmony("EPrime.Implanner").PatchAll();
        }

        public override string SettingsCategory() => "Implanner";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            UiVersion.ObserveCurrentMetrics();
            if (showToolbarLabelStamp != UiVersion.LanguageCurrent)
            {
                showToolbarLabelStamp = UiVersion.LanguageCurrent;
                showToolbarLabel = "IMP_SettingsShowToolbarButton".Translate();
            }
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(showToolbarLabel, ref Settings.showToolbarButton);
            listing.End();
        }
    }
}
