using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public readonly struct SkillLine
    {
        public readonly SkillDef Def;
        public readonly string Label;      // skill label, capitalized, NO colon
        public readonly string ValueText;  // "12.37" (F2) or "-" when disabled
        public readonly Passion Passion;
        public readonly int Aptitude;
        public readonly int Level;         // raw level; 0 when disabled
        public readonly bool Disabled;

        public SkillLine(SkillDef def, string label, string valueText, Passion passion, int aptitude, int level, bool disabled)
        {
            Def = def;
            Label = label;
            ValueText = valueText;
            Passion = passion;
            Aptitude = aptitude;
            Level = level;
            Disabled = disabled;
        }
    }

    public static class SkillsTip
    {
        /// Returns per-skill structured data in natural list order (SkillDef database order).
        /// Level shown as fractional (e.g. 12.37) where the decimal is xp progress.
        public static List<SkillLine> Lines(Pawn pawn)
        {
            if (pawn.skills == null) return new List<SkillLine>();
            var result = new List<SkillLine>(pawn.skills.skills.Count);
            foreach (var s in pawn.skills.skills)
            {
                string skillLabel = s.def.skillLabel.CapitalizeFirst();
                string valueText;
                if (s.TotallyDisabled)
                {
                    valueText = "-";
                }
                else
                {
                    float progress = Mathf.Clamp(s.xpSinceLastLevel / s.XpRequiredForLevelUp, 0f, 0.99f);
                    float fractional = s.Level + progress;
                    valueText = fractional.ToString("F2");
                }
                result.Add(new SkillLine(
                    def: s.def,
                    label: skillLabel,
                    valueText: valueText,
                    passion: s.passion,
                    aptitude: s.TotallyDisabled ? 0 : s.Aptitude,
                    level: s.TotallyDisabled ? 0 : s.Level,
                    disabled: s.TotallyDisabled));
            }
            return result;
        }
    }
}
