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
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("IMP_SettingsShowToolbarButton".Translate(),
                ref Settings.showToolbarButton);
            listing.End();
        }
    }
}
