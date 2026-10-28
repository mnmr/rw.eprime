using System;
using HarmonyLib;
using EPrimeReadouts.UI;
using Verse;

namespace EPrimeReadouts
{
    public class EPrimeReadoutsMod : Mod
    {
        public static ReadoutSettings Settings = null!; // assigned in the mod constructor at startup

        /// The mod's content pack — used to locate shipped data files (Seed/).
        public static ModContentPack? ContentPack;

        /// The mod's install directory; the Help tab loads its topic files
        /// and images from here and the welcome dialog its preview art.
        public static string ContentRootDir = "";

        public EPrimeReadoutsMod(ModContentPack content) : base(content)
        {
            RimShared.GameLib.Localization.BundledTranslationFallback.Install();
            Settings = GetSettings<ReadoutSettings>();
            if (string.IsNullOrEmpty(Settings.setupProfileId)) Settings.setupProfileId = Guid.NewGuid().ToString("N");
            ContentPack = content;
            ContentRootDir = content.RootDir;
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
            Persist();
        }

        public static void PersistSetup(Action<ReadoutSettings> change)
        {
            change(Settings);
            PlayerSetupBridge.Changed();
            SetupDefaults.QueuePreferencesWrite();
        }

        // Static setters keep unchanged option draws free of captured-local allocations.
        public static void PersistSetup(Action<ReadoutSettings, bool> change, bool value)
        {
            change(Settings, value);
            PlayerSetupBridge.Changed();
            SetupDefaults.QueuePreferencesWrite();
        }

        /// Writes the settings, deferred while a Scribe pass is active.
        public static void Persist()
        {
            if (Scribe.mode == LoadSaveMode.Inactive) Settings.Write();
            else LongEventHandler.ExecuteWhenFinished(Settings.Write);
        }

        public override string SettingsCategory() => "EPrime's Readouts";

    }
}
