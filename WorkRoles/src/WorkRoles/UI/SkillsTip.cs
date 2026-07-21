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
        public readonly float SortValue;   // level plus fractional XP; -1 when disabled
        public readonly bool Disabled;

        public SkillLine(SkillDef def, string label, string valueText, Passion passion,
            int aptitude, int level, bool disabled)
            : this(def, label, valueText, passion, aptitude, level,
                disabled ? -1f : level, disabled)
        {
        }

        public SkillLine(SkillDef def, string label, string valueText, Passion passion,
            int aptitude, int level, float sortValue, bool disabled)
        {
            Def = def;
            Label = label;
            ValueText = valueText;
            Passion = passion;
            Aptitude = aptitude;
            Level = level;
            SortValue = sortValue;
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
                result.Add(Line(s));
            return result;
        }

        /// The same immutable display line used by both the bottom stats panel
        /// and optional skill columns in the colonist table.
        public static SkillLine Line(SkillRecord skill)
        {
            bool disabled = skill.TotallyDisabled;
            string valueText = "-";
            float sortValue = -1f;
            if (!disabled)
            {
                float progress = Mathf.Clamp(
                    skill.xpSinceLastLevel / skill.XpRequiredForLevelUp, 0f, 0.99f);
                sortValue = skill.Level + progress;
                valueText = sortValue.ToString("F2");
            }
            return new SkillLine(
                def: skill.def,
                label: skill.def.skillLabel.CapitalizeFirst(),
                valueText: valueText,
                passion: skill.passion,
                aptitude: disabled ? 0 : skill.Aptitude,
                level: disabled ? 0 : skill.Level,
                sortValue: sortValue,
                disabled: disabled);
        }
    }
}
