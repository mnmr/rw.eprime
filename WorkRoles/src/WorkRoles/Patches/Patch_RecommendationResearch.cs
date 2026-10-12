using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.Patches
{
    /// The catalog's availability flag reads research completion. Capture at
    /// progress mutations too: vanilla advances progress before FinishProject,
    /// so a FinishProject-only transition check misses ordinary research.
    /// No retained game objects or polling; nested completion notifications
    /// advance the domain once, and unfinished/no-op progress never advances it.
    public readonly struct RecommendationResearchTransition
    {
        private readonly ResearchProjectDef? project;
        private readonly bool finished;
        private readonly int revision;

        internal RecommendationResearchTransition(ResearchProjectDef? project)
        {
            this.project = project;
            finished = project?.IsFinished ?? true;
            revision = UiVersion.RoleRecommendation;
        }

        internal void Publish()
        {
            if (!finished && project?.IsFinished == true
                && revision == UiVersion.RoleRecommendation)
                UiVersion.Changed(ModelDomain.RoleRecommendation);
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public static class Patch_ResearchManager_FinishProject_Recommendations
    {
        public static void Prefix(ResearchProjectDef proj,
            out RecommendationResearchTransition __state)
            => __state = new RecommendationResearchTransition(proj);
        public static void Postfix(RecommendationResearchTransition __state)
            => __state.Publish();
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.AddProgress))]
    public static class Patch_ResearchManager_AddProgress_Recommendations
    {
        public static void Prefix(ResearchProjectDef proj,
            out RecommendationResearchTransition __state)
            => __state = new RecommendationResearchTransition(proj);
        public static void Postfix(RecommendationResearchTransition __state)
            => __state.Publish();
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ResearchPerformed))]
    public static class Patch_ResearchManager_ResearchPerformed_Recommendations
    {
        public static void Prefix(ResearchManager __instance,
            out RecommendationResearchTransition __state)
            => __state = new RecommendationResearchTransition(__instance.GetProject());
        public static void Postfix(RecommendationResearchTransition __state)
            => __state.Publish();
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ApplyTechprint))]
    public static class Patch_ResearchManager_ApplyTechprint_Recommendations
    {
        public static void Prefix(ResearchProjectDef proj,
            out RecommendationResearchTransition __state)
            => __state = new RecommendationResearchTransition(proj);
        public static void Postfix(RecommendationResearchTransition __state)
            => __state.Publish();
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ApplyKnowledge),
        new[] { typeof(ResearchProjectDef), typeof(float), typeof(float) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
    public static class Patch_ResearchManager_ApplyKnowledge_Recommendations
    {
        public static void Prefix(ResearchProjectDef project,
            out RecommendationResearchTransition __state)
            => __state = new RecommendationResearchTransition(project);
        public static void Postfix(RecommendationResearchTransition __state)
            => __state.Publish();
    }
}
