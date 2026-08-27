using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Immediate invalidation for user-authored pawn facts that can change while
    /// paused. XP progress (used as the auto-best tie-breaker), pawn lifecycle,
    /// and other time-driven facts use the explicitly named 250-tick
    /// responsiveness boundary to avoid per-learning-tick churn.
    internal static class ExternalPawnFactsInvalidation
    {
        internal static void NotifyChanged() =>
            QualityJobsStore.Active?.NotifyExternalPawnFactsChanged();
    }

    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.SetPriority))]
    public static class Patch_WorkPriorityFacts
    {
        public static void Prefix(Pawn_WorkSettings __instance, WorkTypeDef w,
            out int __state)
            => __state = __instance.GetPriority(w);

        public static void Postfix(Pawn_WorkSettings __instance, WorkTypeDef w,
            int __state)
        {
            if (__instance.GetPriority(w) != __state)
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Level), MethodType.Setter)]
    public static class Patch_SkillLevelFacts
    {
        public static void Prefix(SkillRecord __instance, out int __state)
            => __state = __instance.Level;

        public static void Postfix(SkillRecord __instance, int __state)
        {
            if (__instance.Level != __state)
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(InspirationHandler),
        nameof(InspirationHandler.TryStartInspiration))]
    public static class Patch_InspirationStartFacts
    {
        public static void Postfix(bool __result)
        {
            if (__result) ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(InspirationHandler), nameof(InspirationHandler.EndInspiration),
        new[] { typeof(Inspiration) })]
    public static class Patch_InspirationEndFacts
    {
        public static void Prefix(InspirationHandler __instance,
            out Inspiration? __state)
            => __state = __instance.CurState;

        public static void Postfix(InspirationHandler __instance,
            Inspiration? __state)
        {
            if (!object.ReferenceEquals(__instance.CurState, __state))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(InspirationHandler), nameof(InspirationHandler.Reset))]
    public static class Patch_InspirationResetFacts
    {
        public static void Prefix(InspirationHandler __instance,
            out Inspiration? __state)
            => __state = __instance.CurState;

        public static void Postfix(InspirationHandler __instance,
            Inspiration? __state)
        {
            if (!object.ReferenceEquals(__instance.CurState, __state))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.SetIdeo))]
    public static class Patch_PawnIdeoFacts
    {
        public static void Prefix(Pawn_IdeoTracker __instance, out Ideo? __state)
            => __state = __instance.Ideo;

        public static void Postfix(Pawn_IdeoTracker __instance, Ideo? __state)
        {
            if (!object.ReferenceEquals(__instance.Ideo, __state))
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Precept_RoleSingle), nameof(Precept_RoleSingle.Assign))]
    public static class Patch_SingleRoleAssignFacts
    {
        public static void Prefix(Precept_RoleSingle __instance, Pawn p,
            out bool __state) => __state = __instance.IsAssigned(p);

        public static void Postfix(Precept_RoleSingle __instance, Pawn p,
            bool __state)
        {
            if (__instance.IsAssigned(p) != __state)
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Precept_RoleSingle), nameof(Precept_RoleSingle.Unassign))]
    public static class Patch_SingleRoleUnassignFacts
    {
        public static void Prefix(Precept_RoleSingle __instance, Pawn p,
            out bool __state) => __state = __instance.IsAssigned(p);

        public static void Postfix(Precept_RoleSingle __instance, Pawn p,
            bool __state)
        {
            if (__instance.IsAssigned(p) != __state)
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Precept_RoleMulti), nameof(Precept_RoleMulti.Assign))]
    public static class Patch_MultiRoleAssignFacts
    {
        public static void Prefix(Precept_RoleMulti __instance, Pawn p,
            out bool __state) => __state = __instance.IsAssigned(p);

        public static void Postfix(Precept_RoleMulti __instance, Pawn p,
            bool __state)
        {
            if (__instance.IsAssigned(p) != __state)
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Precept_RoleMulti), nameof(Precept_RoleMulti.Unassign))]
    public static class Patch_MultiRoleUnassignFacts
    {
        public static void Prefix(Precept_RoleMulti __instance, Pawn p,
            out bool __state) => __state = __instance.IsAssigned(p);

        public static void Postfix(Precept_RoleMulti __instance, Pawn p,
            bool __state)
        {
            if (__instance.IsAssigned(p) != __state)
                ExternalPawnFactsInvalidation.NotifyChanged();
        }
    }
}
