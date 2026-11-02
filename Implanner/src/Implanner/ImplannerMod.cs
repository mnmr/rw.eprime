using HarmonyLib;
using Implanner.UI;
using RimShared.UiLib;
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
        // Key: none.
        // Value: the Small-font caption height of the entry-style control
        //   (immutable scalar).
        // Dependencies: UiRevision.Current (UI scale, language) and the
        //   settings window width the caption wraps to.
        // Refresh policy: immediate on the first draw after either moves.
        // Equality policy: unchanged inputs reuse the scalar.
        // Teardown: none needed (one scalar for the process).
        private static float entryCaptionHeight;
        private static int entryCaptionStamp = -1;
        private static float entryCaptionWidth = -1f;

        public ImplannerMod(ModContentPack content) : base(content)
        {
            RimShared.GameLib.Localization.BundledTranslationFallback.Install();
            Instance = this;
            Settings = GetSettings<ImplannerSettings>();
            ContentRootDir = content.RootDir;
            new Harmony("EPrime.Implanner").PatchAll();
        }

        public override string SettingsCategory() => "Implanner";

        /// The mod settings page carries the same entry-style control as the
        /// Options tab; the game writes the settings when the page closes.
        public override void DoSettingsWindowContents(Rect inRect)
        {
            UiRevision.ObserveCurrentMetrics();
            PlannerLabels.Ensure();
            float width = Mathf.Min(inRect.width, 520f);
            if (entryCaptionStamp != UiRevision.Current || entryCaptionWidth != width)
            {
                entryCaptionStamp = UiRevision.Current;
                entryCaptionWidth = width;
                using (GuiStateScope.Capture())
                {
                    Text.Font = GameFont.Small;
                    Text.WordWrap = true;
                    entryCaptionHeight = Mathf.Max(Mathf.Ceil(Text.LineHeight),
                        Mathf.Ceil(Text.CalcHeight(PlannerLabels.OptEntryStyle, width)));
                }
            }
            float y = inRect.y;
            ImplannerEntry.DrawStyleRow(inRect.x, ref y, width, entryCaptionHeight, 30f, null);
        }
    }
}
