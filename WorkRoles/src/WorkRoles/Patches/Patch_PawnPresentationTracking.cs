using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkRoles.Patches
{
    public readonly struct PawnNameTransitionState
    {
        internal PawnNameTransitionState(Pawn pawn)
        {
            Pawn = ExternalPawnFacts.IsRelevant(pawn) ? pawn : null;
            Label = Pawn?.LabelShortCap;
            Revision = Pawn == null
                ? 0 : ExternalPawnFacts.Revisions.RevisionOf(Pawn);
        }

        internal Pawn Pawn { get; }
        internal string Label { get; }
        internal int Revision { get; }
    }

    public readonly struct TraitPresentationTransitionState
    {
        private readonly Trait[] traits;
        private readonly bool[] suppressed;

        internal TraitPresentationTransitionState(Pawn pawn)
        {
            Pawn = ExternalPawnFacts.IsRelevant(pawn) ? pawn : null;
            Revision = Pawn == null
                ? 0 : ExternalPawnFacts.Revisions.RevisionOf(Pawn);
            var source = Pawn?.story?.traits?.allTraits;
            if (source == null)
            {
                traits = null;
                suppressed = null;
                return;
            }
            traits = new Trait[source.Count];
            suppressed = new bool[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                traits[i] = source[i];
                suppressed[i] = source[i]?.Suppressed == true;
            }
        }

        internal Pawn Pawn { get; }
        internal int Revision { get; }

        internal bool Changed()
        {
            var source = Pawn?.story?.traits?.allTraits;
            if (source == null) return traits != null;
            if (traits == null || source.Count != traits.Length) return true;
            for (int i = 0; i < traits.Length; i++)
                if (!ReferenceEquals(source[i], traits[i])
                    || (source[i]?.Suppressed == true) != suppressed[i])
                    return true;
            return false;
        }
    }

    internal static class PawnPresentationInvalidation
    {
        internal static void IfChanged(TraitPresentationTransitionState state)
        {
            Pawn pawn = state.Pawn;
            if (pawn == null || !state.Changed()
                || ExternalPawnFacts.Revisions.RevisionOf(pawn)
                    != state.Revision)
                return;
            ExternalPawnFacts.Invalidate(pawn);
        }
    }

    /// Pawn presentation is projected into cached roster/selected-panel
    /// snapshots. These event patches feed the existing per-pawn external-facts
    /// revision so name, trait, and portrait changes publish immediately without
    /// render-time polling or fingerprints. Trait comparisons are exact and the
    /// revision check coalesces nested recalculation calls to one invalidation.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Name), MethodType.Setter)]
    public static class Patch_Pawn_SetName_Presentation
    {
        public static void Prefix(Pawn __instance,
            ref PawnNameTransitionState __state)
            => __state = new PawnNameTransitionState(__instance);

        public static void Postfix(Pawn __instance,
            PawnNameTransitionState __state)
        {
            if (__state.Pawn != null
                && !string.Equals(__state.Label, __instance.LabelShortCap,
                    System.StringComparison.Ordinal)
                && ExternalPawnFacts.Revisions.RevisionOf(__instance)
                    == __state.Revision)
                ExternalPawnFacts.Invalidate(__instance);
        }
    }

    [HarmonyPatch(typeof(PortraitsCache), nameof(PortraitsCache.SetDirty))]
    public static class Patch_PortraitsCache_SetDirty_Presentation
    {
        public static void Postfix(Pawn pawn)
        {
            if (ExternalPawnFacts.IsRelevant(pawn))
                ExternalPawnFacts.Invalidate(pawn);
        }
    }

    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.GainTrait))]
    public static class Patch_TraitSet_GainTrait_Presentation
    {
        public static void Prefix(Pawn ___pawn,
            ref TraitPresentationTransitionState __state)
            => __state = new TraitPresentationTransitionState(___pawn);

        public static void Postfix(TraitPresentationTransitionState __state)
            => PawnPresentationInvalidation.IfChanged(__state);
    }

    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.RemoveTrait))]
    public static class Patch_TraitSet_RemoveTrait_Presentation
    {
        public static void Prefix(Pawn ___pawn,
            ref TraitPresentationTransitionState __state)
            => __state = new TraitPresentationTransitionState(___pawn);

        public static void Postfix(TraitPresentationTransitionState __state)
            => PawnPresentationInvalidation.IfChanged(__state);
    }

    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.RecalculateSuppression))]
    public static class Patch_TraitSet_RecalculateSuppression_Presentation
    {
        public static void Prefix(Pawn ___pawn,
            ref TraitPresentationTransitionState __state)
            => __state = new TraitPresentationTransitionState(___pawn);

        public static void Postfix(TraitPresentationTransitionState __state)
            => PawnPresentationInvalidation.IfChanged(__state);
    }
}
