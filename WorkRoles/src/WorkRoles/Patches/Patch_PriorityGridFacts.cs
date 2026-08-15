using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkRoles.Patches
{
    public readonly struct PriorityGridSkillTransitionState
    {
        internal PriorityGridSkillTransitionState(SkillRecord skill)
        {
            Pawn = skill?.Pawn;
            Level = skill?.Level ?? 0;
            Revision = Pawn == null
                ? 0 : PriorityGridFacts.Revisions.RevisionOf(Pawn);
        }

        internal Pawn Pawn { get; }
        internal int Level { get; }
        internal int Revision { get; }

        internal void InvalidateIfChanged(SkillRecord skill)
        {
            if (Pawn == null || skill == null || skill.Level == Level
                || PriorityGridFacts.Revisions.RevisionOf(Pawn) != Revision)
                return;
            PriorityGridFacts.Invalidate(Pawn);
        }
    }

    /// Skill XP changes are intentionally irrelevant until the displayed whole
    /// level changes. Capture both mutation paths used by vanilla and mods.
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]
    public static class Patch_SkillRecord_Learn_PriorityGrid
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(SkillRecord __instance,
            out PriorityGridSkillTransitionState __state)
            => __state = new PriorityGridSkillTransitionState(__instance);

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SkillRecord __instance,
            PriorityGridSkillTransitionState __state)
            => __state.InvalidateIfChanged(__instance);
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Level), MethodType.Setter)]
    public static class Patch_SkillRecord_SetLevel_PriorityGrid
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(SkillRecord __instance,
            out PriorityGridSkillTransitionState __state)
            => __state = new PriorityGridSkillTransitionState(__instance);

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SkillRecord __instance,
            PriorityGridSkillTransitionState __state)
            => __state.InvalidateIfChanged(__instance);
    }

    [HarmonyPatch(typeof(SkillRecord),
        nameof(SkillRecord.EnsureMinLevelWithMargin))]
    public static class Patch_SkillRecord_EnsureMinimum_PriorityGrid
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(SkillRecord __instance,
            out PriorityGridSkillTransitionState __state)
            => __state = new PriorityGridSkillTransitionState(__instance);

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(SkillRecord __instance,
            PriorityGridSkillTransitionState __state)
            => __state.InvalidateIfChanged(__instance);
    }

    public sealed class PriorityGridPassionTransitionState
    {
        private readonly SkillRecord[] records;
        private readonly Passion[] passions;
        private readonly int revision;

        internal PriorityGridPassionTransitionState(Pawn pawn,
            List<SkillDef> chosenSkills)
        {
            Pawn = PriorityGridFacts.IsRelevant(pawn) ? pawn : null;
            revision = Pawn == null
                ? 0 : PriorityGridFacts.Revisions.RevisionOf(Pawn);
            if (Pawn?.skills == null || chosenSkills == null
                || chosenSkills.Count == 0)
                return;

            records = new SkillRecord[chosenSkills.Count];
            passions = new Passion[chosenSkills.Count];
            for (int i = 0; i < chosenSkills.Count; i++)
            {
                SkillRecord record = Pawn.skills.GetSkill(chosenSkills[i]);
                records[i] = record;
                passions[i] = record?.passion ?? Passion.None;
            }
        }

        private Pawn Pawn { get; }

        internal void InvalidateIfChanged()
        {
            if (Pawn == null || records == null
                || PriorityGridFacts.Revisions.RevisionOf(Pawn) != revision)
                return;
            for (int i = 0; i < records.Length; i++)
                if (records[i] != null && records[i].passion != passions[i])
                {
                    PriorityGridFacts.Invalidate(Pawn);
                    return;
                }
        }
    }

    /// Growth moments are vanilla's passion mutation that is not followed by
    /// Pawn.Notify_DisabledWorkTypesChanged (gene passion changes are).
    [HarmonyPatch(typeof(ChoiceLetter_GrowthMoment),
        nameof(ChoiceLetter_GrowthMoment.MakeChoices))]
    public static class Patch_GrowthMomentChoices_PriorityGrid
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Pawn ___pawn, List<SkillDef> skills,
            out PriorityGridPassionTransitionState __state)
            => __state = new PriorityGridPassionTransitionState(___pawn, skills);

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(PriorityGridPassionTransitionState __state)
            => __state?.InvalidateIfChanged();
    }
}
