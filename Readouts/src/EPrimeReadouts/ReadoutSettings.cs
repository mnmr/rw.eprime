using System.Collections.Generic;
using Verse;

namespace EPrimeReadouts
{
    /// Per-player view state: panel geometry and per-group tier depths. Group
    /// definitions live in ReadoutStore (per-save, synced); nothing here is
    /// ever synced or written to saves.
    public class ReadoutSettings : ModSettings
    {
        public bool useVanillaReadout;
        public float offsetX = 7f;
        public float offsetY = 7f;
        public float panelWidth = 140f;
        public float bottomMargin = 200f;
        /// Key: "{world.persistentRandomValue}:{groupId}" — unique across saves.
        public Dictionary<string, int> tierDepths = new Dictionary<string, int>();
        /// Key like tierDepths; absent = group disabled. Seeds start disabled.
        public Dictionary<string, bool> enabledGroups = new Dictionary<string, bool>();
        public float dialogW;
        public float dialogH;
        public bool helpGroupsFolded;
        public bool helpResourcesFolded;
        public bool helpEditorFolded;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref useVanillaReadout, "useVanillaReadout", false);
            Scribe_Values.Look(ref offsetX, "offsetX", 7f);
            Scribe_Values.Look(ref offsetY, "offsetY", 7f);
            Scribe_Values.Look(ref panelWidth, "panelWidth", 140f);
            Scribe_Values.Look(ref bottomMargin, "bottomMargin", 200f);
            Scribe_Values.Look(ref dialogW, "dialogW", 0f);
            Scribe_Values.Look(ref dialogH, "dialogH", 0f);
            Scribe_Values.Look(ref helpGroupsFolded, "helpGroupsFolded", false);
            Scribe_Values.Look(ref helpResourcesFolded, "helpResourcesFolded", false);
            Scribe_Values.Look(ref helpEditorFolded, "helpEditorFolded", false);
            Scribe_Collections.Look(ref tierDepths, "tierDepths", LookMode.Value, LookMode.Value);
            if (tierDepths == null) tierDepths = new Dictionary<string, int>();
            Scribe_Collections.Look(ref enabledGroups, "enabledGroups", LookMode.Value, LookMode.Value);
            if (enabledGroups == null) enabledGroups = new Dictionary<string, bool>();
        }
    }
}
