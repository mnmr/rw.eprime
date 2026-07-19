using System.Collections.Generic;
using RimWorld;
using Verse;
using WorkRoles.Core.Signals;

namespace WorkRoles.Signals
{
    internal static class PawnSignalSnapshots
    {
        internal static PawnSignalSnapshot Build(Pawn pawn)
        {
            if (pawn == null) return PawnSignalSnapshot.Empty;
            var enabledSkills = new List<string>();
            var persistentlyBadSkills = new List<string>();
            if (pawn.skills?.skills != null)
                foreach (SkillRecord skill in pawn.skills.skills)
                    if (skill != null && !skill.TotallyDisabled)
                    {
                        enabledSkills.Add(skill.def.defName);
                        // VSE marks the stable vanilla no-passion identity as
                        // bad. Condition-changing custom passions are never
                        // inferred from absence here.
                        if (skill.passion == Passion.None)
                            persistentlyBadSkills.Add(skill.def.defName);
                    }

            SignalSnapshot signals = SignalSnapshotBuilder.Build(
                PawnSignalCollector.Collect(pawn),
                enabledSkills,
                VseSignalReflection.CrossSkillEffectsEnabled(),
                persistentlyBadSkills);
            return PawnSignalSnapshot.Create(enabledSkills, signals);
        }
    }
}
