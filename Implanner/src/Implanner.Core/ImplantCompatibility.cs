using System;

namespace Implanner.Core
{
    /// A curated group of implant kinds the player may treat as one slot.
    public enum CompatGroup
    {
        None = 0,
        Bladder = 1,
        HygieneEnhancer = 2,
    }

    /// Curated cross-mod exclusivity that definition data cannot express.
    /// Bladder implants and hygiene enhancers from different mods (Dubs
    /// Bad Hygiene, FSF Advanced Bionics Expansion) are plain torso
    /// implants with no tags, so the game lets one pawn carry all of them
    /// with stacking effects. The "Allow multiple ..." options
    /// (PlannerModel.AllowMultipleBladders / AllowMultipleHygieneEnhancers)
    /// let the player treat each group as one slot instead. Definition
    /// names only: missing content simply never matches.
    public static class ImplantCompatibility
    {
        /// Listed in display order for the option tooltips.
        public static readonly string[] BladderImplants =
        {
            "BionicBladder",       // Dubs Bad Hygiene
            "FSFAdvBionicBladder", // FSF Advanced Bionics Expansion
        };

        public static readonly string[] HygieneEnhancerImplants =
        {
            "HygieneEnhancer",       // Dubs Bad Hygiene
            "FSFAdvHygieneEnhancer", // FSF Advanced Bionics Expansion
        };

        public static CompatGroup GroupOf(string defName)
        {
            if (Contains(BladderImplants, defName)) return CompatGroup.Bladder;
            if (Contains(HygieneEnhancerImplants, defName)) return CompatGroup.HygieneEnhancer;
            return CompatGroup.None;
        }

        static bool Contains(string[] defNames, string defName)
        {
            for (int i = 0; i < defNames.Length; i++)
                if (string.Equals(defNames[i], defName, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
