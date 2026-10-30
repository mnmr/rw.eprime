using Implanner.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace Implanner
{
    /// One-shot projection at reconciliation and surgery boundaries. Never
    /// edits live hediffs to simulate healing. Nonpermanent wounds cannot
    /// justify amputating a limb that would regain its original function.
    internal static class RecoverablePartEfficiency
    {
        internal static float Of(Pawn pawn, BodyPartRecord part)
        {
            HediffSet set = pawn.health.hediffSet;
            float current = PawnCapacityUtility.CalculatePartEfficiency(set, part);
            for (BodyPartRecord? parent = part.parent; parent != null; parent = parent.parent)
            {
                Hediff? added = set.GetDirectlyAddedPartFor(parent);
                if (added != null) return Mathf.Max(current, ImplantQualities.InstalledEfficiency(added));
            }
            bool regenerates = set.HasRegeneration;
            if (set.PartIsMissing(part)) return regenerates ? Mathf.Max(1f, current) : current;

            bool healsScars = regenerates;
            var genes = pawn.genes?.GenesListForReading;
            for (int i = 0; genes != null && i < genes.Count; i++)
                if (genes[i] is Gene_Healing && genes[i].Active) healsScars = true;
            var hediffs = set.hediffs;
            for (int i = 0; !healsScars && i < hediffs.Count; i++)
                if (hediffs[i].TryGetComp<HediffComp_HealPermanentWounds>() != null) healsScars = true;

            float maximum = part.def.GetMaxHealth(pawn);
            float health = maximum;
            float intrinsic = 1f, offsets = 0f;
            bool ignoreHealth = false;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff.Part != part) continue;
                HediffStage? stage = hediff.CurStage;
                if (hediff is Hediff_AddedPart)
                    intrinsic *= ImplantQualities.InstalledEfficiency(hediff) - (stage?.partEfficiencyOffset ?? 0f);
                // Unknown negative stage effects are not proof of permanent
                // loss. Retain bonuses, conservatively ignore penalties.
                if (stage != null)
                {
                    offsets += Mathf.Max(0f, stage.partEfficiencyOffset);
                    ignoreHealth |= stage.partIgnoreMissingHP;
                }
                if (!healsScars && hediff is Hediff_Injury injury && injury.IsPermanent())
                    health = injury.destroysBodyParts ? health - injury.Severity
                        : Mathf.Max(health - injury.Severity, 1f);
            }
            health = Mathf.Max(health, part.def.destroyableByDamage ? 0f : 1f);
            float fraction = maximum > 0f ? Mathf.Clamp01(Mathf.RoundToInt(health) / maximum) : 1f;
            if (DamageWorker_AddInjury.ShouldReduceDamageToPreservePart(part))
                fraction = Mathf.InverseLerp(.1f, 1f, fraction);
            return Mathf.Max(current, intrinsic * (ignoreHealth ? 1f : fraction) + offsets);
        }
    }
}
