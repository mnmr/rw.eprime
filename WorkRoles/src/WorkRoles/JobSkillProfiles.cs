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
            public string SkillDefName;
            public int Floor, Top;    // min/max required level among gated content
            public int Gated;         // content pieces carrying a requirement
            public int Total;         // 0 = open-ended pool (buildings, plants)
        }

        public sealed class GiverProfile
        {
            /// Undecorated def label — never the disambiguated display name;
            /// provenance belongs only to the source footer.
            public string Title;
            /// Skills the work uses for performance, from recipes for bill work and otherwise from the work type.
            public List<string> UsedSkills = new List<string>();
            /// Same skills as UsedSkills, by defName (language-independent).
            public List<string> UsedSkillDefNames = new List<string>();
            /// Skills the work grants XP in (bill givers: from recipes).
            public List<string> TrainedSkills = new List<string>();
            /// Same skills as TrainedSkills, by defName (language-independent).
            public List<string> TrainedSkillDefNames = new List<string>();
            /// The work type's relevant skills, by defName (tier split:
            /// XP inside vs outside the giver's own discipline).
            public List<string> RelevantSkillDefNames = new List<string>();
            public bool GivesXp;
            public List<SkillRange> Requirements = new List<SkillRange>();
            public string CurveHeader; // stat label, or null when no curve
            public List<(string label, string value)> CurveRows;
            public string SourceLine;  // "Work giver: defName (mod)" footer
            public string TipCache;
        }

        public sealed class WorkTypeProfile
        {
            /// Undecorated def label (see GiverProfile.Title).
            public string Title;
            public string Description;
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
            // Allow Tool: finish-off is an execution-style instant kill, no XP
            // (same footing as Slaughter/DoExecution).
            ["FinishOff"] = new string[0],
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
                    Title = (workType.gerundLabel ?? workType.labelShort ?? workType.defName).CapitalizeFirst(),
                    TrainedSkills = SkillLabels(workType.relevantSkills),
                    TotalGivers = members.Count,
                    XpGivers = members.Count(p => p.GivesXp),
                    // The vanilla Work-tab text players already know leads the tip.
                    Description = StripSelfAttribution(workType.description, workType.modContentPack),
                    SourceLine = SourceLineFor(workType, "WR_SkillTipTypeSource"),
                };
                // Per skill, the highest requirement any member content carries.
                // Only Top renders in the type tip; summing Gated here would
                // double-count recipes shared between bill givers.
                foreach (var group in members.SelectMany(p => p.Requirements)
                             .GroupBy(r => r.SkillDefName))
                    profile.Requirements.Add(new SkillRange
                    {
                        SkillLabel = group.First().SkillLabel,
                        SkillDefName = group.Key,
                        Top = group.Max(r => r.Top),
                    });
                byType[workType.defName] = profile;
            }
        }

        /// Mods often end descriptions with an "added by X" line; the source
        /// footer already carries provenance, so those lines are dropped.
        private static string StripSelfAttribution(string description, ModContentPack mod)
        {
            if (description == null || mod?.Name == null || description.IndexOf('\n') < 0)
                return description;
            return string.Join("\n", description.Split('\n').Where(line =>
                line.IndexOf(mod.Name, System.StringComparison.OrdinalIgnoreCase) < 0)).TrimEnd();
        }

        private static GiverProfile BuildGiver(WorkGiverDef giver)
        {
            var profile = new GiverProfile
            {
                Title = (giver.label ?? giver.defName).CapitalizeFirst(),
                UsedSkills = SkillLabels(giver.workType.relevantSkills),
            };
            profile.UsedSkillDefNames = giver.workType.relevantSkills.NullOrEmpty()
                ? new List<string>()
                : giver.workType.relevantSkills.Select(s => s.defName).ToList();
            profile.RelevantSkillDefNames = giver.workType.relevantSkills.NullOrEmpty()
                ? new List<string>()
                : giver.workType.relevantSkills.Select(s => s.defName).ToList();
            var recipes = BillRecipesOf(giver);

            if (recipes != null && recipes.Count > 0)
            {
                // Bill work: XP and requirements come from the recipes themselves
                // (a recipe's workSkill can differ from the work type — the drug
                // lab teaches Cooking/Intellectual under a Crafting type).
                profile.UsedSkills = recipes
                    .Where(r => r.workSkill != null)
                    .Select(r => SkillLabel(r.workSkill))
                    .Distinct().ToList();
                profile.UsedSkillDefNames = recipes
                    .Where(r => r.workSkill != null)
                    .Select(r => r.workSkill.defName)
                    .Distinct().ToList();
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
                        SkillDefName = group.Key.defName,
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
                    // Curation drift (game update, renamed skill) must be loud,
                    // not a silently shorter facts list.
                    if (profile.TrainedSkills.Count < xpSkills.Length)
                        Log.Warning($"[WorkRoles] XP table for {giver.defName} names unknown skill(s): "
                            + xpSkills.Where(d => DefDatabase<SkillDef>.GetNamedSilentFail(d) == null).ToCommaList());
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

            FillCurveFacts(profile, giver.defName);
            profile.SourceLine = SourceLineFor(giver, "WR_SkillTipGiverSource");
            return profile;
        }

        /// Identity footer: the def's internal name and the mod that defines
        /// it. modContentPack is null for runtime-generated defs. Rendered as
        /// a dim TextRow — the string itself stays uncolorized.
        private static string SourceLineFor(Def def, string key)
        {
            string mod = def.modContentPack == null
                ? "WR_SkillTipUnknownMod".Translate().ToString()
                : def.modContentPack.IsCoreMod ? "RimWorld"
                : def.modContentPack.Name;
            return key.Translate(def.defName, mod);
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
                SkillDefName = skill.defName,
                Floor = levels.Count > 0 ? levels.Min() : 0,
                Top = levels.Count > 0 ? levels.Max() : 0,
                Gated = levels.Count,
            };

        private static void FillCurveFacts(GiverProfile profile, string giverDefName)
        {
            if (!CurveStatByGiver.TryGetValue(giverDefName, out var statName)) return;
            var stat = DefDatabase<StatDef>.GetNamedSilentFail(statName);
            var need = stat?.skillNeedFactors?.OfType<SkillNeed_Direct>()
                .FirstOrDefault(n => n.skill != null && !n.valuesPerLevel.NullOrEmpty());
            if (need == null) return;

            var milestones = LowerIsBetter.Contains(statName)
                ? JobSkillMath.FallingMilestones(need.valuesPerLevel, new[] { 0.5f, 0.1f })
                : JobSkillMath.RisingMilestones(need.valuesPerLevel, new[] { 0.5f, 0.75f, 0.9f, 1f });
            if (milestones.Count <= 1) return;

            profile.CurveHeader = stat.LabelCap;
            profile.CurveRows = new List<(string, string)>();
            foreach (var (level, value) in milestones)
                profile.CurveRows.Add((
                    "WR_TipLevelN".Translate(level).ToString(), value.ToStringPercent()));
        }

        // ----- Tooltip composition (cached; defs are session-fixed, a language
        // switch clears via ClearCaches) -----

        public static string GiverTip(string defName)
        {
            var profile = ForGiver(defName);
            if (profile == null) return null;
            return profile.TipCache ?? (profile.TipCache =
                Patches.Patch_ActiveTip_TipRect.Register(BuildGiverModel(profile)));
        }

        public static string WorkTypeTip(string defName)
        {
            var profile = ForWorkType(defName);
            if (profile == null) return null;
            return profile.TipCache ?? (profile.TipCache =
                Patches.Patch_ActiveTip_TipRect.Register(BuildTypeModel(profile)));
        }

        private static TipModel BuildGiverModel(GiverProfile profile)
        {
            var model = new TipModel { Title = profile.Title };
            var facts = model.AddSection();
            if (profile.UsedSkills.Count > 0)
                facts.Fact("WR_TipSkillsLabel".Translate(), profile.UsedSkills.ToCommaList());
            facts.Fact("WR_TipTrainsLabel".Translate(), profile.TrainedSkills.Count == 0
                ? "WR_TipTrainsNothing".Translate().ToString()
                : profile.TrainedSkills.ToCommaList());
            foreach (var range in profile.Requirements)
                facts.Fact("WR_TipRequiresLabel".Translate(), range.Total > 0
                    ? "WR_TipReqBills".Translate(
                        range.SkillLabel, LevelRange(range), range.Gated, range.Total).ToString()
                    : "WR_TipReqItems".Translate(
                        range.SkillLabel, LevelRange(range), range.Gated).ToString());
            if (profile.CurveRows != null)
            {
                var curve = model.AddSection(profile.CurveHeader);
                foreach (var (label, value) in profile.CurveRows)
                    curve.Fact(label, value);
            }
            if (profile.SourceLine != null)
                model.AddSection().Text(profile.SourceLine, dim: true);
            return model;
        }

        private static TipModel BuildTypeModel(WorkTypeProfile profile)
        {
            var model = new TipModel { Title = profile.Title };
            if (!profile.Description.NullOrEmpty())
                model.AddSection().Text(profile.Description);
            var facts = model.AddSection();
            if (profile.TrainedSkills.Count > 0)
                facts.Fact("WR_TipSkillsLabel".Translate(), profile.TrainedSkills.ToCommaList());
            facts.Fact("WR_TipXpLabel".Translate(),
                "WR_TipXpValue".Translate(profile.XpGivers, profile.TotalGivers));
            foreach (var range in profile.Requirements)
                facts.Fact("WR_TipRequiresLabel".Translate(),
                    "WR_TipReqItems".Translate(
                        range.SkillLabel, LevelRange(range), range.Gated).ToString());
            if (profile.SourceLine != null)
                model.AddSection().Text(profile.SourceLine, dim: true);
            return model;
        }

        private static string LevelRange(SkillRange range)
            => range.Floor == range.Top ? range.Top.ToString() : $"{range.Floor}-{range.Top}";
    }
}
