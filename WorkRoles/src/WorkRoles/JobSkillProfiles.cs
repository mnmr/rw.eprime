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
            /// Same skills as TrainedSkills, by defName (language-independent).
            public List<string> TrainedSkillDefNames = new List<string>();
            /// The work type's relevant skills, by defName (tier split:
            /// XP inside vs outside the giver's own discipline).
            public List<string> RelevantSkillDefNames = new List<string>();
            public bool GivesXp;
            public List<SkillRange> Requirements = new List<SkillRange>();
            public string CurveLine;  // pre-formatted stat curve fact, or null
            public string SourceLine; // "Work giver: defName (mod)" footer
            public string TipCache;
        }

        public sealed class WorkTypeProfile
        {
            public List<string> TrainedSkills = new List<string>();
            public int XpGivers, TotalGivers;
            public List<SkillRange> Requirements = new List<SkillRange>();
            public string SourceLine; // "Work type: defName (mod)" footer
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

        /// Skills each vanilla/DLC non-bill giver actually grants XP in —
        /// verified against decompiled JobDriver/verb code (see the game
        /// wiring; defs carry no XP data). Empty = the work grants no XP.
        private static readonly Dictionary<string, string[]> XpByGiver = new Dictionary<string, string[]>
        {
            // Core
            ["FightFires"] = new string[0],
            ["PatientGoToBedEmergencyTreatment"] = new string[0],
            ["PatientGoToBedTreatment"] = new string[0],
            ["DoctorTendEmergency"] = new[] { "Medicine" },
            ["DoctorTendToHumanlikes"] = new[] { "Medicine" },
            ["DoctorTendToSelf"] = new[] { "Medicine" },
            ["DoctorTendToSelfEmergency"] = new[] { "Medicine" },
            ["DoctorFeedHumanlikes"] = new string[0],
            ["DoctorRescue"] = new string[0],
            ["DoctorTendToAnimals"] = new[] { "Medicine" },
            ["DoctorFeedAnimals"] = new string[0],
            ["TakeToBedToOperate"] = new string[0],
            ["VisitSickPawn"] = new string[0],
            ["PatientGoToBedRecuperate"] = new string[0],
            ["Flick"] = new string[0],
            ["BasicReleasePrisoner"] = new string[0],
            ["Open"] = new string[0],
            ["EjectFuel"] = new string[0],
            ["ExtractSkull"] = new string[0],
            ["DoExecution"] = new string[0],
            ["ExecuteGuiltyColonist"] = new string[0],
            ["ReleasePrisoner"] = new string[0],
            ["TakePrisonerToBed"] = new string[0],
            ["FeedPrisoner"] = new string[0],
            ["DeliverFoodToPrisoner"] = new string[0],
            ["ChatWithPrisoner"] = new[] { "Social" },
            ["TakeRoamingAnimalsToPen"] = new string[0],
            ["HandlingFeedPatientAnimals"] = new string[0],
            ["TakeToPen"] = new string[0],
            ["Slaughter"] = new string[0],
            ["ReleaseToWild"] = new string[0],
            ["Milk"] = new[] { "Animals" },
            ["Shear"] = new[] { "Animals" },
            ["Tame"] = new[] { "Animals" },
            ["Train"] = new[] { "Animals" },
            ["RebalanceAnimalsInPens"] = new string[0],
            ["CookFillHopper"] = new string[0],
            ["HunterHunt"] = new[] { "Shooting" },
            ["FixBrokenDownBuilding"] = new string[0],
            ["Uninstall"] = new string[0],
            ["ExtractTree"] = new[] { "Plants" },
            ["BuildRoofs"] = new string[0],
            ["RemoveRoofs"] = new string[0],
            ["DeconstructForBlueprint"] = new[] { "Construction" },
            ["ConstructFinishFrames"] = new[] { "Construction" },
            ["ConstructDeliverResourcesToFrames"] = new string[0],
            ["ConstructDeliverResourcesToBlueprints"] = new[] { "Construction" },
            ["Replant"] = new[] { "Plants" },
            ["FillIn"] = new string[0],
            ["Deconstruct"] = new[] { "Construction" },
            ["Repair"] = new[] { "Construction" },
            ["PaintBuilding"] = new[] { "Artistic" },
            ["PaintFloor"] = new[] { "Artistic" },
            ["RemovePaintBuilding"] = new string[0],
            ["RemovePaintFloor"] = new string[0],
            ["ConstructRemoveFloors"] = new[] { "Construction" },
            ["ConstructRemoveFoundations"] = new[] { "Construction" },
            ["ConstructSmoothFloors"] = new[] { "Construction" },
            ["ConstructSmoothWalls"] = new[] { "Construction" },
            ["GrowerHarvest"] = new[] { "Plants" },
            ["GrowerSow"] = new[] { "Plants" },
            ["Mine"] = new[] { "Mining" },
            ["Drill"] = new[] { "Mining" },
            ["PlantsCut"] = new[] { "Plants" },
            ["RearmTurrets"] = new string[0],
            ["Refuel"] = new string[0],
            ["UnloadCarriers"] = new string[0],
            ["HelpGatheringItemsForCaravan"] = new string[0],
            ["LoadTransporters"] = new string[0],
            ["HaulToPortal"] = new string[0],
            ["Strip"] = new string[0],
            ["HaulCorpses"] = new string[0],
            ["TakeBeerOutOfFermentingBarrel"] = new string[0],
            ["FillFermentingBarrel"] = new string[0],
            ["HaulGeneral"] = new string[0],
            ["DeliverResourcesToFrames"] = new string[0],
            ["DeliverResourcesToBlueprints"] = new string[0],
            ["HaulMerge"] = new string[0],
            ["EmptyEggBox"] = new string[0],
            ["CleanClearSnow"] = new string[0],
            ["CleanFilth"] = new string[0],
            ["Research"] = new[] { "Intellectual" },
            ["LongRangeScan"] = new[] { "Intellectual" },
            ["GroundPenetratingScan"] = new[] { "Intellectual" },
            ["PlantSeed"] = new[] { "Plants" },
            ["StudyArchotechStructures"] = new[] { "Intellectual" },
            ["Hack"] = new[] { "Intellectual" },
            // Ideology
            ["ExecuteSlave"] = new string[0],
            ["EmancipateSlave"] = new string[0],
            ["EnslavePrisoner"] = new[] { "Social" },
            ["ImprisonSlave"] = new string[0],
            ["SuppressSlave"] = new string[0],
            ["ConvertPrisoner"] = new[] { "Social" },
            ["PruneGauranlenTree"] = new[] { "Plants" },
            ["ChangeTreeMode"] = new string[0],
            ["HaulToBiosculpterPod"] = new string[0],
            // Biotech
            ["DeliverHemogenToPrisoner"] = new string[0],
            ["FeedHemogen"] = new string[0],
            ["HaulToGeneBank"] = new string[0],
            ["CreateXenogerm"] = new[] { "Intellectual" },
            ["EnterGeneExtractor"] = new string[0],
            ["EnterGrowthVat"] = new string[0],
            ["CarryToGeneExtractor"] = new string[0],
            ["CarryToGrowthVat"] = new string[0],
            ["BringBabyToSafety"] = new string[0],
            ["BreastfeedBaby"] = new string[0],
            ["PlayWithBaby"] = new string[0],
            ["BottleFeedBaby"] = new string[0],
            ["CarryToBreastfeed"] = new string[0],
            ["ChildcarerTeach"] = new[] { "Social" },
            ["HaulToGrowthVat"] = new string[0],
            ["RepairMech"] = new[] { "Crafting" },
            ["EmptyWasteContainer"] = new string[0],
            ["HaulMechsToCharger"] = new string[0],
            ["EnterSubcoreScanner"] = new string[0],
            ["CarryToSubcoreScanner"] = new string[0],
            ["HaulToCarrier"] = new string[0],
            ["HaulToSubcoreScanner"] = new string[0],
            ["HaulToWastepackAtomizer"] = new string[0],
            ["CleanClearPollution"] = new string[0],
            // Anomaly
            ["TakeEntityToHoldingPlatform"] = new string[0],
            ["ReleaseEntity"] = new string[0],
            ["TransferEntity"] = new string[0],
            ["ActivitySuppression"] = new[] { "Intellectual" },
            ["ExecuteEntity"] = new string[0],
            ["InterrogatePrisoner"] = new[] { "Social" },
            ["TakeBioferriteOutOfHarvester"] = new string[0],
            ["DoctorTendToEntities"] = new[] { "Medicine" },
            ["ExtractBioferrite"] = new string[0],
            ["StudyInteract"] = new[] { "Intellectual" },
            // Odyssey
            ["Fish"] = new[] { "Animals" },
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
                    SourceLine = SourceLineFor(workType, "WR_SkillTipTypeSource"),
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
            profile.RelevantSkillDefNames = giver.workType.relevantSkills.NullOrEmpty()
                ? new List<string>()
                : giver.workType.relevantSkills.Select(s => s.defName).ToList();
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
                profile.TrainedSkillDefNames = recipes
                    .Where(r => r.workSkill != null && r.workSkillLearnFactor > 0f)
                    .Select(r => r.workSkill.defName)
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
                // Curated code-verified XP facts; the work-type heuristic only
                // covers modded givers the table cannot know.
                if (XpByGiver.TryGetValue(giver.defName, out var xpSkills))
                {
                    profile.TrainedSkillDefNames = xpSkills.ToList();
                    profile.TrainedSkills = xpSkills
                        .Select(d => DefDatabase<SkillDef>.GetNamedSilentFail(d))
                        .Where(s => s != null).Select(SkillLabel).ToList();
                }
                else
                {
                    profile.TrainedSkills = SkillLabels(giver.workType.relevantSkills);
                    profile.TrainedSkillDefNames = giver.workType.relevantSkills.NullOrEmpty()
                        ? new List<string>()
                        : giver.workType.relevantSkills.Select(s => s.defName).ToList();
                }
                profile.GivesXp = profile.TrainedSkillDefNames.Count > 0;
                if (giver.defName == "ConstructFinishFrames")
                    profile.Requirements.Add(ConstructionRange());
                else if (giver.defName == "GrowerSow")
                    profile.Requirements.Add(SowingRange());
            }

            profile.CurveLine = CurveLineFor(giver.defName);
            profile.SourceLine = SourceLineFor(giver, "WR_SkillTipGiverSource");
            return profile;
        }

        /// Identity footer: the def's internal name and the mod that defines
        /// it. modContentPack is null for runtime-generated defs.
        private static string SourceLineFor(Def def, string key)
        {
            string mod = def.modContentPack == null
                ? "WR_SkillTipUnknownMod".Translate().ToString()
                : def.modContentPack.IsCoreMod ? "RimWorld"
                : def.modContentPack.Name;
            return key.Translate(def.defName, mod).ToString();
        }

        /// The recipes a bill giver can work: its fixed benches' recipes, or —
        /// for surgery-style givers whose bill giver is the pawn itself — the
        /// recipes of every matching race. Null when the giver isn't bill work.
        private static List<RecipeDef> BillRecipesOf(WorkGiverDef giver)
        {
            var benches = new List<ThingDef>();
            if (giver.fixedBillGiverDefs != null)
                benches.AddRange(giver.fixedBillGiverDefs.Where(b => b != null));
            // Corpse variants count as the same race source: corpse bills run the
            // race's recipes (WorkGiver_DoBill checks the corpse's inner pawn).
            if (giver.billGiversAllHumanlikes || giver.billGiversAllHumanlikesCorpses)
                benches.AddRange(DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race?.Humanlike == true));
            if (giver.billGiversAllAnimals || giver.billGiversAllAnimalsCorpses)
                benches.AddRange(DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race?.Animal == true));
            if (giver.billGiversAllMechanoids || giver.billGiversAllMechanoidsCorpses)
                benches.AddRange(DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race?.IsMechanoid == true));
            if (benches.Count == 0) return null;
            // The game runs a bill only when requiredGiverWorkType matches the
            // giver (WorkGiver_DoBill): bench-sharing givers train nothing else.
            return benches.SelectMany(b => b.AllRecipes)
                .Where(r => r.requiredGiverWorkType == null
                    || r.requiredGiverWorkType == giver.workType)
                .Distinct().ToList();
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
            if (profile.SourceLine != null)
                lines.Add(profile.SourceLine);
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
            if (profile.SourceLine != null)
                lines.Add(profile.SourceLine);
            return string.Join("\n", lines);
        }

        private static string LevelRange(SkillRange range)
            => range.Floor == range.Top ? range.Top.ToString() : $"{range.Floor}-{range.Top}";
    }
}
