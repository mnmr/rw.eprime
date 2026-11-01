using System;
using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Who may perform an operation and which skill its bill checks. The
    /// game gates a bill on the recipe's own work skill
    /// (Bill.PawnAllowedToStartAnew reads recipe.workSkill): Medicine for
    /// vanilla surgery, Crafting for Android Tiers' android repairs. Doctors
    /// take medical bills; Android Tiers can hand android operations to
    /// crafters instead (PlannerModel.AndroidSurgeryByCrafters). The doctor
    /// floor is kept per pool, so a floor is always a level some eligible
    /// pawn can actually meet in the skill the bill checks.
    internal readonly struct SurgeonPool
    {
        private SurgeonPool(SkillDef? skill, bool crafters)
        {
            Skill = skill;
            Crafters = crafters;
        }

        /// The skill the bill checks; null when the recipe checks none.
        internal SkillDef? Skill { get; }

        /// Android Tiers crafters (Smithing work + "repair androids")
        /// rather than doctors.
        internal bool Crafters { get; }

        /// Doctors judged by Medical skill: vanilla surgery.
        internal static SurgeonPool Doctors => new SurgeonPool(SkillDefOf.Medicine, false);

        /// The pool for one operation on one patient.
        internal static SurgeonPool For(PlannerModel model, Pawn patient, RecipeDef recipe) =>
            new SurgeonPool(recipe.workSkill,
                model.AndroidSurgeryByCrafters && AndroidTiers.IsAndroid(patient));

        /// The floor key of this pool at a colony: the bare colony id for
        /// doctors judged by Medical skill (older saves carry exactly that).
        internal string FloorKey(string colonyId) =>
            Skill == SkillDefOf.Medicine && !Crafters
                ? colonyId
                : PlannerModel.FloorKey(colonyId,
                    (Skill?.defName ?? "None") + (Crafters ? "/Crafters" : "/Doctors"));

        /// The pawn's level in the pool's skill when the pawn is eligible to
        /// take the pool's operations; -1 when not. Allocation-free.
        internal int SkillOf(Pawn pawn)
        {
            if (Skill == null) return -1;
            bool eligible = Crafters
                ? AndroidTiers.RepairsAndroids(pawn)
                : pawn.workSettings != null && pawn.workSettings.WorkIsActive(WorkTypeDefOf.Doctor);
            return eligible ? pawn.skills?.GetSkill(Skill)?.Level ?? 0 : -1;
        }

        /// Every pool automation can schedule into: doctors in each skill
        /// any catalog surgery works (Medicine first, then by defName), and
        /// the same skills for crafters while androids are operated on by
        /// crafters. Boundary pass only (allocates).
        internal static List<SurgeonPool> All(PlannerModel model)
        {
            var skills = new List<SkillDef> { SkillDefOf.Medicine };
            var extra = new List<SkillDef>();
            IReadOnlyList<ImplantCatalogEntry> catalog = Catalogs.Implants();
            for (int i = 0; i < catalog.Count; i++)
            {
                List<RecipeDef> recipes = catalog[i].SurgeryRecipes;
                for (int r = 0; r < recipes.Count; r++)
                {
                    SkillDef? skill = recipes[r].workSkill;
                    if (skill != null && !skills.Contains(skill) && !extra.Contains(skill))
                        extra.Add(skill);
                }
            }
            extra.Sort((a, b) => string.CompareOrdinal(a.defName, b.defName));
            skills.AddRange(extra);
            var pools = new List<SurgeonPool>(skills.Count * 2);
            for (int i = 0; i < skills.Count; i++)
                pools.Add(new SurgeonPool(skills[i], false));
            if (model.AndroidSurgeryByCrafters && AndroidTiers.Loaded)
                for (int i = 0; i < skills.Count; i++)
                    pools.Add(new SurgeonPool(skills[i], true));
            return pools;
        }
    }
}
