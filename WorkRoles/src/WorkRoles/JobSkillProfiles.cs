using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Per-giver skill facts derived from game data once per session: what a job
    /// trains (and whether it grants XP), what skill levels its content requires,
    /// and the success/failure curve tied to it. Feeds the role editor's tooltips
    /// and, later, training-role logic. Everything except CurveStatByGiver reads
    /// the live DefDatabase, so modded recipes, patched stat curves and changed
    /// learn factors flow through automatically.
    public static class JobSkillProfiles
    {
        public sealed class SkillRange
        {
            public string SkillLabel;
            public int Floor, Top;    // min/max required level among gated content
            public int Gated;         // content pieces carrying a requirement
            public int Total;         // 0 = open-ended pool (buildings, plants)
        }

        public sealed class GiverProfile
        {
            /// Skills the work uses (performance), from the work type.
            public List<string> UsedSkills = new List<string>();
            /// Skills the work grants XP in (bill givers: from recipes).
            public List<string> TrainedSkills = new List<string>();
            public bool GivesXp;
            public List<SkillRange> Requirements = new List<SkillRange>();
            public string CurveLine;  // pre-formatted stat curve fact, or null
            public string TipCache;
        }

        public sealed class WorkTypeProfile
        {
            public List<string> TrainedSkills = new List<string>();
            public int XpGivers, TotalGivers;
            public List<SkillRange> Requirements = new List<SkillRange>();
            public string TipCache;
        }

        /// Which stat curve tells a giver's success/failure story. The stats are
        /// read (and mod-patchable) from the DefDatabase; only this association
        /// is curated — the game wires it in code, not defs.
        private static readonly Dictionary<string, string> CurveStatByGiver = new Dictionary<string, string>
        {
            ["DoBillsCook"] = "FoodPoisonChance",
            ["DoBillsCookCampfire"] = "FoodPoisonChance",
            ["ConstructFinishFrames"] = "ConstructSuccessChance",
            ["FixBrokenDownBuilding"] = "FixBrokenDownBuildingSuccessChance",
            ["DoBillsMedicalHumanOperation"] = "MedicalSurgerySuccessChance",
            ["DoBillsMedicalAnimalOperation"] = "MedicalSurgerySuccessChance",
            ["Mine"] = "MiningYield",
            ["Drill"] = "MiningYield",
            ["GrowerHarvest"] = "PlantHarvestYield",
            ["Milk"] = "AnimalGatherYield",
            ["Shear"] = "AnimalGatherYield",
            ["Fish"] = "FishingYield",
        };

        /// Curves where lower is better bottom out instead of reaching 1.0.
        private static readonly HashSet<string> LowerIsBetter = new HashSet<string> { "FoodPoisonChance" };

        private static Dictionary<string, GiverProfile> byGiver;
        private static Dictionary<string, WorkTypeProfile> byType;

        /// Language switch: every cached label and composed tip is translated
        /// text — drop the lot and rebuild lazily (see Patch_Language).
        internal static void ClearCaches()
        {
            byGiver = null;
            byType = null;
        }

        /// Defensive label: a modded SkillDef may omit skillLabel.
        private static string SkillLabel(SkillDef skill) =>
            (skill.skillLabel ?? skill.label ?? skill.defName).CapitalizeFirst();

        public static GiverProfile ForGiver(string defName)
        {
            EnsureBuilt();
            return byGiver.TryGetValue(defName, out var profile) ? profile : null;
        }

        public static WorkTypeProfile ForWorkType(string defName)
        {
            EnsureBuilt();
            return byType.TryGetValue(defName, out var profile) ? profile : null;
        }

        private static void EnsureBuilt()
        {
            if (byGiver != null) return;
            byGiver = new Dictionary<string, GiverProfile>();
            byType = new Dictionary<string, WorkTypeProfile>();

            foreach (var giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if (giver.workType == null) continue;
                byGiver[giver.defName] = BuildGiver(giver);
            }

            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                var members = workType.workGiversByPriority
                    .Select(g => byGiver.TryGetValue(g.defName, out var p) ? p : null)
                    .Where(p => p != null)
                    .ToList();
                var profile = new WorkTypeProfile
                {
                    TrainedSkills = SkillLabels(workType.relevantSkills),
                    TotalGivers = members.Count,
                    XpGivers = members.Count(p => p.GivesXp),
                };
                // Per skill, the highest requirement any member content carries.
                // Only Top renders in the type tip; summing Gated here would
                // double-count recipes shared between bill givers.
                foreach (var group in members.SelectMany(p => p.Requirements).GroupBy(r => r.SkillLabel))
                    profile.Requirements.Add(new SkillRange
                    {
                        SkillLabel = group.Key,
                        Top = group.Max(r => r.Top),
                    });
                byType[workType.defName] = profile;
            }
        }

        private static GiverProfile BuildGiver(WorkGiverDef giver)
        {
            var profile = new GiverProfile
            {
                UsedSkills = SkillLabels(giver.workType.relevantSkills),
            };
            var recipes = BillRecipesOf(giver);

            if (recipes != null && recipes.Count > 0)
            {
                // Bill work: XP and requirements come from the recipes themselves
                // (a recipe's workSkill can differ from the work type — the drug
                // lab teaches Cooking/Intellectual under a Crafting type).
                profile.TrainedSkills = recipes
                    .Where(r => r.workSkill != null && r.workSkillLearnFactor > 0f)
                    .Select(r => SkillLabel(r.workSkill))
                    .Distinct().ToList();
                profile.GivesXp = profile.TrainedSkills.Count > 0;
                foreach (var group in recipes
                    .Where(r => !r.skillRequirements.NullOrEmpty())
                    .SelectMany(r => r.skillRequirements)
                    .Where(req => req.skill != null)
                    .GroupBy(req => req.skill))
                    profile.Requirements.Add(new SkillRange
                    {
                        SkillLabel = SkillLabel(group.Key),
                        Floor = group.Min(req => req.minLevel),
                        Top = group.Max(req => req.minLevel),
                        Gated = group.Count(),
                        Total = recipes.Count,
                    });
            }
            else
            {
                // Non-bill work grants XP through driver code; the work type's
                // relevant skills are the best available statement of it.
                profile.TrainedSkills = SkillLabels(giver.workType.relevantSkills);
                profile.GivesXp = profile.TrainedSkills.Count > 0;
                if (giver.defName == "ConstructFinishFrames")
                    profile.Requirements.Add(ConstructionRange());
                else if (giver.defName == "GrowerSow")
                    profile.Requirements.Add(SowingRange());
            }

            profile.CurveLine = CurveLineFor(giver.defName);
            return profile;
        }

        /// The recipes a bill giver can work: its fixed benches' recipes, or —
        /// for surgery-style givers whose bill giver is the pawn itself — the
        /// recipes of every matching race. Null when the giver isn't bill work.
        private static List<RecipeDef> BillRecipesOf(WorkGiverDef giver)
        {
            var benches = new List<ThingDef>();
            if (giver.fixedBillGiverDefs != null)
                benches.AddRange(giver.fixedBillGiverDefs.Where(b => b != null));
            if (giver.billGiversAllHumanlikes)
                benches.AddRange(DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race?.Humanlike == true));
            if (giver.billGiversAllAnimals)
                benches.AddRange(DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race?.Animal == true));
            if (giver.billGiversAllMechanoids)
                benches.AddRange(DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race?.IsMechanoid == true));
            if (benches.Count == 0) return null;
            return benches.SelectMany(b => b.AllRecipes).Distinct().ToList();
        }

        private static List<string> SkillLabels(List<SkillDef> skills)
            => skills.NullOrEmpty()
                ? new List<string>()
                : skills.Select(SkillLabel).ToList();

        private static SkillRange ConstructionRange()
        {
            var levels = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.constructionSkillPrerequisite > 0)
                .Select(d => d.constructionSkillPrerequisite)
                .Concat(DefDatabase<TerrainDef>.AllDefsListForReading
                    .Where(d => d.constructionSkillPrerequisite > 0)
                    .Select(d => d.constructionSkillPrerequisite))
                .ToList();
            return RangeOf(levels, SkillDefOf.Construction);
        }

        private static SkillRange SowingRange()
        {
            var levels = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.plant != null && d.plant.sowMinSkill > 0 && d.plant.Sowable)
                .Select(d => d.plant.sowMinSkill)
                .ToList();
            return RangeOf(levels, SkillDefOf.Plants);
        }

        private static SkillRange RangeOf(List<int> levels, SkillDef skill)
            => new SkillRange
            {
                SkillLabel = SkillLabel(skill),
                Floor = levels.Count > 0 ? levels.Min() : 0,
                Top = levels.Count > 0 ? levels.Max() : 0,
                Gated = levels.Count,
            };

        private static string CurveLineFor(string giverDefName)
        {
            if (!CurveStatByGiver.TryGetValue(giverDefName, out var statName)) return null;
            var stat = DefDatabase<StatDef>.GetNamedSilentFail(statName);
            var need = stat?.skillNeedFactors?.OfType<SkillNeed_Direct>()
                .FirstOrDefault(n => n.skill != null && !n.valuesPerLevel.NullOrEmpty());
            if (need == null) return null;

            var milestones = LowerIsBetter.Contains(statName)
                ? JobSkillMath.FallingMilestones(need.valuesPerLevel, new[] { 0.5f, 0.1f })
                : JobSkillMath.RisingMilestones(need.valuesPerLevel, new[] { 0.5f, 0.75f, 0.9f, 1f });
            if (milestones.Count <= 1) return null;

            string skill = SkillLabel(need.skill);
            var lines = new List<string> { "WR_SkillTipCurveHeader".Translate(stat.LabelCap).ToString() };
            foreach (var (level, value) in milestones)
                lines.Add("    " + "WR_SkillTipCurveLevel".Translate(
                    value.ToStringPercent(), skill, level));
            return string.Join("\n", lines);
        }

        // ----- Tooltip composition (cached; defs are session-fixed, a language
        // switch clears via ClearCaches) -----

        public static string GiverTip(string defName)
        {
            var profile = ForGiver(defName);
            if (profile == null) return null;
            return profile.TipCache ?? (profile.TipCache =
                Patches.Patch_ActiveTip_TipRect.RegisterWide(ComposeGiverTip(profile)));
        }

        public static string WorkTypeTip(string defName)
        {
            var profile = ForWorkType(defName);
            if (profile == null) return null;
            return profile.TipCache ?? (profile.TipCache =
                Patches.Patch_ActiveTip_TipRect.RegisterWide(ComposeTypeTip(profile)));
        }

        private static string ComposeGiverTip(GiverProfile profile)
        {
            var lines = new List<string>();
            if (profile.UsedSkills.Count > 0)
                lines.Add("WR_SkillTipSkills".Translate(profile.UsedSkills.ToCommaList()).ToString());
            lines.Add(profile.TrainedSkills.Count == 0
                ? "WR_SkillTipTrainsNothing".Translate().ToString()
                : "WR_SkillTipTrains".Translate(profile.TrainedSkills.ToCommaList()).ToString());
            foreach (var range in profile.Requirements)
                lines.Add(range.Total > 0
                    ? "WR_SkillTipRequiresBills".Translate(
                        range.SkillLabel, LevelRange(range), range.Gated, range.Total).ToString()
                    : "WR_SkillTipRequiresItems".Translate(
                        range.SkillLabel, LevelRange(range), range.Gated).ToString());
            if (profile.CurveLine != null)
                lines.Add(profile.CurveLine);
            return string.Join("\n", lines);
        }

        private static string ComposeTypeTip(WorkTypeProfile profile)
        {
            var lines = new List<string>();
            if (profile.TrainedSkills.Count > 0)
                lines.Add("WR_SkillTipSkills".Translate(profile.TrainedSkills.ToCommaList()).ToString());
            lines.Add("WR_SkillTipTypeXp".Translate(profile.XpGivers, profile.TotalGivers).ToString());
            foreach (var range in profile.Requirements)
                lines.Add("WR_SkillTipTypeGated".Translate(range.SkillLabel, range.Top).ToString());
            return string.Join("\n", lines);
        }

        private static string LevelRange(SkillRange range)
            => range.Floor == range.Top ? range.Top.ToString() : $"{range.Floor}-{range.Top}";
    }
}
