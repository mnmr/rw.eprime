using HarmonyLib;
using QualityJobs.Core;
using Verse;

namespace QualityJobs
{
    /// Endless Growth (SlimeSenpai.EndlessGrowth) raises the skill cap to
    /// 100 and changes how quality rolls from skill. Core's SkillRules
    /// follows it so thresholds, sliders, odds and finisher ranking agree
    /// with what the game actually rolls.
    [StaticConstructorOnStartup]
    internal static class EndlessGrowthSupport
    {
        // Owner: process. Key: the mod's CommonPatches type, the same
        // provider Implanner binds. Value: one immutable flag in
        // SkillRules.EndlessGrowth. Dependencies: the loaded assembly set
        // (fixed for process lifetime, identical on every multiplayer
        // client). Refresh: startup only. Teardown: none; no game object
        // is retained.
        static EndlessGrowthSupport()
        {
            SkillRules.EndlessGrowth =
                AccessTools.TypeByName("SlimeSenpai.EndlessGrowth.CommonPatches") != null;
            if (SkillRules.EndlessGrowth)
                Log.Message("[QualityJobs] Endless Growth is active; skill thresholds "
                    + "reach 100 and quality odds follow its curve.");
        }
    }
}
