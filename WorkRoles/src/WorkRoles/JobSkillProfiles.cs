using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Per-giver skill facts derived from game data once per session: what a job
    /// uses, what it trains (and whether it grants XP), what skill levels its
    /// content requires, and the success/failure curve tied to it. Recipes,
    /// requirements, and work-type relevance read the live DefDatabase; code-
    /// defined direct-job use/training and curve associations use audited
    /// baselines because defs do not declare those facts.
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
            /// Vanilla unlock age (years) of the parent work type; 0 = no gate.
            public int UnlockAge;
            public List<SkillRange> Requirements = new List<SkillRange>();
            public string CurveHeader; // stat label, or null when no curve
            public List<(string label, string value)> CurveRows;
            public string SourceLine;  // "Work giver: defName (mod)" footer
            public string TipCache;
            internal StructuredTip StructuredTipCache;
            // Hint-composed variants: one region registers one tooltip, so
            // contextual hints ride the same tip instead of a second region
            // (two stable keys on one rect keep resetting the hover gate).
            internal StructuredTip CoveredTipCache;
            internal StructuredTip DeadTipCache;
        }

        public sealed class WorkTypeProfile
        {
            /// Undecorated def label (see GiverProfile.Title).
            public string Title;
            public string Description;
            public List<string> UsedSkills = new List<string>();
            public List<string> TrainedSkills = new List<string>();
            public int XpGivers, TotalGivers;
            /// Vanilla unlock age (years); 0 = no gate.
            public int UnlockAge;
            public List<SkillRange> Requirements = new List<SkillRange>();
            public string SourceLine; // "Work type: defName (mod)" footer
            public string TipCache;
            internal StructuredTip StructuredTipCache;
            internal StructuredTip DeadTipCache;
        }

        private sealed class ReferenceIdentity<T> where T : class
        {
            private readonly Dictionary<T, int> identities =
                new Dictionary<T, int>(ReferenceComparer<T>.Instance);
            private int next = 1;

            internal int Of(T value)
            {
                if (!identities.TryGetValue(value, out int identity))
                {
                    identity = next++;
                    identities.Add(value, identity);
                }
                return identity;
            }
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            internal static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
        }

        private sealed class CurveFacts
        {
            internal string StatDefName;
            internal List<(int level, float value)> Milestones;
        }

        /// The Core index is language-independent. These maps are the sole
        /// retained game-definition owner used to materialize localized labels;
        /// definition reload invalidation drops the whole snapshot atomically.
        private sealed class DefinitionSnapshot
        {
            internal JobProfileIndex Index;
            internal readonly Dictionary<string, WorkGiverDef> Givers =
                new Dictionary<string, WorkGiverDef>(StringComparer.Ordinal);
            internal readonly Dictionary<string, WorkTypeDef> WorkTypes =
                new Dictionary<string, WorkTypeDef>(StringComparer.Ordinal);
            internal readonly Dictionary<int, SkillDef> SkillsByIdentity =
                new Dictionary<int, SkillDef>();
            internal readonly Dictionary<string, SkillDef> SkillsByName =
                new Dictionary<string, SkillDef>(StringComparer.Ordinal);
            internal readonly Dictionary<string, StatDef> StatsByName =
                new Dictionary<string, StatDef>(StringComparer.Ordinal);
            internal readonly Dictionary<string, CurveFacts> CurvesByGiver =
                new Dictionary<string, CurveFacts>(StringComparer.Ordinal);
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

        private static DefinitionSnapshot definitionFacts;
        private static Dictionary<string, GiverProfile> byGiver;
        private static Dictionary<string, WorkTypeProfile> byType;
        private static int profileGeneration;
        private static int queuedLocalizedFacadeGeneration = -1;

        /// Language switch: every cached label and composed tip is translated
        /// text. The invariant Core index and its live Def identity map survive.
        internal static void InvalidateLanguageCaches()
        {
            unchecked { profileGeneration++; }
            queuedLocalizedFacadeGeneration = -1;
            byGiver = null;
            byType = null;
        }

        /// Definition replacement: release both the invariant facts and every
        /// localized profile/TipCache before any live DefDatabase is mutated.
        internal static void InvalidateDefinitions()
        {
            definitionFacts = null;
            InvalidateLanguageCaches();
        }

        internal static void WarmDefinitionFacts()
        {
            DefinitionFacts();
        }

        internal static void WarmLocalizedFacade() => EnsureBuilt();

        /// Late language injection runs inside LongEventHandler's completion
        /// list. Append a main-thread facade-only warm and make any callback
        /// queued before a later language/definition invalidation a no-op.
        internal static void QueueLocalizedFacadeWarm()
        {
            int token = profileGeneration;
            if (queuedLocalizedFacadeGeneration == token) return;
            queuedLocalizedFacadeGeneration = token;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (token != profileGeneration
                    || queuedLocalizedFacadeGeneration != token) return;
                queuedLocalizedFacadeGeneration = -1;
                WarmLocalizedFacade();
            });
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

        /// Invariant per-giver facts from the Core index, for skill-evidence
        /// reduction that needs no localized labels.
        internal static IReadOnlyDictionary<string, JobProfileGiverFacts> GiverFacts()
            => DefinitionFacts().Index.Givers;

        /// Immutable language-independent job facts consumed by the shared
        /// recommendation catalog projection.
        internal static JobProfileIndex RecommendationIndex()
            => DefinitionFacts().Index;

        private static void EnsureBuilt()
        {
            if (byGiver != null) return;
            DefinitionSnapshot snapshot = DefinitionFacts();
            var givers = new Dictionary<string, GiverProfile>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JobProfileGiverFacts> pair in snapshot.Index.Givers)
                if (snapshot.Givers.TryGetValue(pair.Key, out WorkGiverDef giver))
                    givers[pair.Key] = BuildGiver(snapshot, giver, pair.Value);

            var workTypes = new Dictionary<string, WorkTypeProfile>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JobProfileWorkTypeFacts> pair in snapshot.Index.WorkTypes)
                if (snapshot.WorkTypes.TryGetValue(pair.Key, out WorkTypeDef workType))
                    workTypes[pair.Key] = BuildWorkType(snapshot, workType, pair.Value);

            byGiver = givers;
            byType = workTypes;
        }

        private static DefinitionSnapshot DefinitionFacts()
        {
            if (definitionFacts == null)
                definitionFacts = BuildDefinitionFacts();
            return definitionFacts;
        }

        private static DefinitionSnapshot BuildDefinitionFacts()
        {
            var snapshot = new DefinitionSnapshot();
            var builder = new JobProfileIndexBuilder();
            var workTypeIdentities = new ReferenceIdentity<WorkTypeDef>();
            var skillIdentities = new ReferenceIdentity<SkillDef>();
            var recipeUserIdentities = new ReferenceIdentity<ThingDef>();
            var recipeIdentities = new ReferenceIdentity<RecipeDef>();
            var fixedRecipeUsers = new List<ThingDef>();
            var fixedRecipeUsersSeen = new HashSet<ThingDef>(ReferenceComparer<ThingDef>.Instance);

            foreach (SkillDef skill in DefDatabase<SkillDef>.AllDefsListForReading)
            {
                if (skill == null) continue;
                int identity = skillIdentities.Of(skill);
                snapshot.SkillsByIdentity[identity] = skill;
                snapshot.SkillsByName[skill.defName] = skill;
            }

            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (workType == null) continue;
                int identity = workTypeIdentities.Of(workType);
                snapshot.WorkTypes[workType.defName] = workType;
                var members = new List<string>();
                List<WorkGiverDef> priorityGivers = workType.workGiversByPriority;
                if (!priorityGivers.NullOrEmpty())
                    for (int i = 0; i < priorityGivers.Count; i++)
                    {
                        WorkGiverDef giver = priorityGivers[i];
                        if (giver != null) members.Add(giver.defName);
                    }
                builder.AddWorkType(identity, workType.defName,
                    SkillSources(workType.relevantSkills, skillIdentities, snapshot), members);
            }

            foreach (WorkGiverDef giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if (giver?.workType == null) continue;
                snapshot.Givers[giver.defName] = giver;
                var fixedUsers = new List<int>();
                if (!giver.fixedBillGiverDefs.NullOrEmpty())
                    for (int i = 0; i < giver.fixedBillGiverDefs.Count; i++)
                    {
                        ThingDef user = giver.fixedBillGiverDefs[i];
                        if (user == null) continue;
                        fixedUsers.Add(recipeUserIdentities.Of(user));
                        if (fixedRecipeUsersSeen.Add(user)) fixedRecipeUsers.Add(user);
                    }
                bool curated = VanillaXpBaseline.XpByGiver.TryGetValue(
                    giver.defName, out string[] xpSkills);
                bool curatedUsed = VanillaUsedSkillBaseline.UsedByGiver.TryGetValue(
                    giver.defName, out IReadOnlyList<string> usedSkills);
                builder.AddGiver(
                    giver.defName,
                    workTypeIdentities.Of(giver.workType),
                    SkillSources(giver.workType.relevantSkills, skillIdentities, snapshot),
                    fixedUsers,
                    giver.billGiversAllHumanlikes || giver.billGiversAllHumanlikesCorpses,
                    giver.billGiversAllAnimals || giver.billGiversAllAnimalsCorpses,
                    giver.billGiversAllMechanoids || giver.billGiversAllMechanoidsCorpses,
                    curated,
                    xpSkills,
                    curatedUsed,
                    usedSkills);
            }

            var observedRecipeUsers = new HashSet<ThingDef>(ReferenceComparer<ThingDef>.Instance);
            foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (thing == null) continue;
                if (observedRecipeUsers.Add(thing))
                {
                    JobProfileRecipeUserKind kind = JobProfileRecipeUserKind.None;
                    if (thing.race?.Humanlike == true) kind |= JobProfileRecipeUserKind.Humanlike;
                    if (thing.race?.Animal == true) kind |= JobProfileRecipeUserKind.Animal;
                    if (thing.race?.IsMechanoid == true) kind |= JobProfileRecipeUserKind.Mechanoid;
                    var directRecipes = new List<int>();
                    if (!thing.recipes.NullOrEmpty())
                        for (int i = 0; i < thing.recipes.Count; i++)
                        {
                            RecipeDef recipe = thing.recipes[i];
                            if (recipe == null) continue;
                            directRecipes.Add(recipeIdentities.Of(recipe));
                            builder.AddRecipe(RecipeSource(recipe, recipeIdentities,
                                workTypeIdentities, skillIdentities, snapshot));
                        }
                    builder.AddRecipeUser(recipeUserIdentities.Of(thing), kind, directRecipes);
                }

                if (thing.constructionSkillPrerequisite > 0)
                    builder.AddConstructionRequirement(thing.constructionSkillPrerequisite);
                if (thing.plant != null && thing.plant.sowMinSkill > 0 && thing.plant.Sowable)
                    builder.AddSowingRequirement(thing.plant.sowMinSkill);
            }

            // A fixed bill giver may be a runtime/non-database ThingDef. The
            // old AllRecipes path still read its direct recipes; register it
            // without race classification so the reverse index does the same.
            for (int i = 0; i < fixedRecipeUsers.Count; i++)
            {
                ThingDef thing = fixedRecipeUsers[i];
                if (observedRecipeUsers.Contains(thing)) continue;
                var directRecipes = new List<int>();
                if (!thing.recipes.NullOrEmpty())
                    for (int j = 0; j < thing.recipes.Count; j++)
                    {
                        RecipeDef recipe = thing.recipes[j];
                        if (recipe == null) continue;
                        directRecipes.Add(recipeIdentities.Of(recipe));
                        builder.AddRecipe(RecipeSource(recipe, recipeIdentities,
                            workTypeIdentities, skillIdentities, snapshot));
                    }
                builder.AddRecipeUser(recipeUserIdentities.Of(thing),
                    JobProfileRecipeUserKind.None, directRecipes);
            }

            foreach (TerrainDef terrain in DefDatabase<TerrainDef>.AllDefsListForReading)
                if (terrain != null && terrain.constructionSkillPrerequisite > 0)
                    builder.AddConstructionRequirement(terrain.constructionSkillPrerequisite);

            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe == null) continue;
                var users = new List<int>();
                if (!recipe.recipeUsers.NullOrEmpty())
                    for (int i = 0; i < recipe.recipeUsers.Count; i++)
                    {
                        ThingDef user = recipe.recipeUsers[i];
                        if (user != null) users.Add(recipeUserIdentities.Of(user));
                    }
                builder.AddDatabaseRecipe(RecipeSource(recipe, recipeIdentities,
                    workTypeIdentities, skillIdentities, snapshot), users);
            }

            JobProfileSkillSource construction = SkillSource(
                SkillDefOf.Construction, skillIdentities, snapshot);
            JobProfileSkillSource plants = SkillSource(
                SkillDefOf.Plants, skillIdentities, snapshot);
            builder.SetConstructionSkill(construction.Identity, construction.DefName);
            builder.SetSowingSkill(plants.Identity, plants.DefName);

            foreach (StatDef stat in DefDatabase<StatDef>.AllDefsListForReading)
                if (stat != null)
                    snapshot.StatsByName[stat.defName] = stat;

            foreach (KeyValuePair<string, string> pair in CurveStatByGiver)
            {
                if (!snapshot.StatsByName.TryGetValue(pair.Value, out StatDef stat)) continue;
                SkillNeed_Direct need = stat.skillNeedFactors?.OfType<SkillNeed_Direct>()
                    .FirstOrDefault(n => n.skill != null && !n.valuesPerLevel.NullOrEmpty());
                if (need == null) continue;
                List<(int level, float value)> milestones = LowerIsBetter.Contains(pair.Value)
                    ? JobSkillMath.FallingMilestones(need.valuesPerLevel, new[] { 0.5f, 0.1f })
                    : JobSkillMath.RisingMilestones(need.valuesPerLevel,
                        new[] { 0.5f, 0.75f, 0.9f, 1f });
                if (milestones.Count > 1)
                    snapshot.CurvesByGiver[pair.Key] = new CurveFacts
                    {
                        StatDefName = pair.Value,
                        Milestones = milestones,
                    };
            }

            snapshot.Index = builder.Build();
            return snapshot;
        }

        private static JobProfileRecipeSource RecipeSource(
            RecipeDef recipe,
            ReferenceIdentity<RecipeDef> recipeIdentities,
            ReferenceIdentity<WorkTypeDef> workTypeIdentities,
            ReferenceIdentity<SkillDef> skillIdentities,
            DefinitionSnapshot snapshot)
        {
            JobProfileSkillSource? workSkill = recipe.workSkill == null
                ? (JobProfileSkillSource?)null
                : SkillSource(recipe.workSkill, skillIdentities, snapshot);
            var requirements = new List<JobProfileSkillRequirementSource>();
            if (!recipe.skillRequirements.NullOrEmpty())
                for (int i = 0; i < recipe.skillRequirements.Count; i++)
                {
                    SkillRequirement requirement = recipe.skillRequirements[i];
                    if (requirement?.skill == null) continue;
                    JobProfileSkillSource skill = SkillSource(
                        requirement.skill, skillIdentities, snapshot);
                    requirements.Add(new JobProfileSkillRequirementSource(
                        skill.Identity, skill.DefName, requirement.minLevel));
                }
            return new JobProfileRecipeSource(
                recipeIdentities.Of(recipe),
                recipe.requiredGiverWorkType == null
                    ? (int?)null : workTypeIdentities.Of(recipe.requiredGiverWorkType),
                workSkill,
                recipe.workSkillLearnFactor,
                requirements);
        }

        private static List<JobProfileSkillSource> SkillSources(
            List<SkillDef> skills,
            ReferenceIdentity<SkillDef> identities,
            DefinitionSnapshot snapshot)
        {
            var result = new List<JobProfileSkillSource>();
            if (!skills.NullOrEmpty())
                for (int i = 0; i < skills.Count; i++)
                    if (skills[i] != null)
                        result.Add(SkillSource(skills[i], identities, snapshot));
            return result;
        }

        private static JobProfileSkillSource SkillSource(
            SkillDef skill,
            ReferenceIdentity<SkillDef> identities,
            DefinitionSnapshot snapshot)
        {
            int identity = identities.Of(skill);
            snapshot.SkillsByIdentity[identity] = skill;
            return new JobProfileSkillSource(identity, skill.defName);
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

        private static GiverProfile BuildGiver(
            DefinitionSnapshot snapshot, WorkGiverDef giver, JobProfileGiverFacts facts)
        {
            var profile = new GiverProfile
            {
                Title = (giver.label ?? giver.defName).CapitalizeFirst(),
                UsedSkills = SkillLabels(snapshot, facts.UsedSkillIdentities, facts.UsesRecipes),
                UsedSkillDefNames = new List<string>(facts.UsedSkillDefNames),
                TrainedSkillDefNames = new List<string>(facts.TrainedSkillDefNames),
                RelevantSkillDefNames = new List<string>(facts.RelevantSkillDefNames),
                GivesXp = facts.GivesXp,
                UnlockAge = RecsAdapter.WorkTypeUnlockAge(giver.workType),
            };
            if (facts.HasCuratedUsedSkills && !facts.UsesRecipes)
                profile.UsedSkills = CuratedSkillLabels(snapshot,
                    facts.UsedSkillDefNames, giver.defName, "used-skill");

            if (facts.HasCuratedXp && !facts.UsesRecipes)
                profile.TrainedSkills = CuratedSkillLabels(snapshot,
                    facts.TrainedSkillDefNames, giver.defName, "XP");
            else
                profile.TrainedSkills = SkillLabels(
                    snapshot, facts.TrainedSkillIdentities, facts.UsesRecipes);

            for (int i = 0; i < facts.Requirements.Count; i++)
                profile.Requirements.Add(SkillRangeOf(snapshot, facts.Requirements[i]));

            FillCurveFacts(snapshot, profile, giver.defName);
            profile.SourceLine = SourceLineFor(giver, "WR_SkillTipGiverSource");
            return profile;
        }

        private static WorkTypeProfile BuildWorkType(
            DefinitionSnapshot snapshot, WorkTypeDef workType, JobProfileWorkTypeFacts facts)
        {
            var profile = new WorkTypeProfile
            {
                Title = (workType.gerundLabel ?? workType.labelShort ?? workType.defName)
                    .CapitalizeFirst(),
                UsedSkills = CuratedSkillLabels(snapshot,
                    facts.UsedSkillDefNames, workType.defName, "work-type used-skill"),
                TrainedSkills = CuratedSkillLabels(snapshot,
                    facts.TrainedSkillDefNames, workType.defName, "work-type XP"),
                TotalGivers = facts.TotalGivers,
                XpGivers = facts.XpGivers,
                UnlockAge = RecsAdapter.WorkTypeUnlockAge(workType),
                Description = StripSelfAttribution(workType.description, workType.modContentPack),
                SourceLine = SourceLineFor(workType, "WR_SkillTipTypeSource"),
            };
            for (int i = 0; i < facts.Requirements.Count; i++)
                profile.Requirements.Add(SkillRangeOf(snapshot, facts.Requirements[i]));
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

        private static List<string> SkillLabels(
            DefinitionSnapshot snapshot, IReadOnlyList<int> identities, bool distinct)
        {
            var result = new List<string>();
            HashSet<string> seen = distinct ? new HashSet<string>() : null;
            for (int i = 0; i < identities.Count; i++)
                if (snapshot.SkillsByIdentity.TryGetValue(identities[i], out SkillDef skill))
                {
                    string label = SkillLabel(skill);
                    if (seen == null || seen.Add(label)) result.Add(label);
                }
            return result;
        }

        private static List<string> CuratedSkillLabels(
            DefinitionSnapshot snapshot,
            IReadOnlyList<string> defNames,
            string giverDefName,
            string tableName)
        {
            var result = new List<string>();
            var unknown = new List<string>();
            for (int i = 0; i < defNames.Count; i++)
            {
                string name = defNames[i];
                if (snapshot.SkillsByName.TryGetValue(name, out SkillDef skill))
                    result.Add(SkillLabel(skill));
                else
                    unknown.Add(name);
            }
            if (unknown.Count > 0)
                Log.Warning($"[WorkRoles] {tableName} table for {giverDefName} names unknown skill(s): "
                    + unknown.ToCommaList());
            return result;
        }

        private static SkillRange SkillRangeOf(
            DefinitionSnapshot snapshot, JobProfileRequirementFacts facts)
        {
            snapshot.SkillsByIdentity.TryGetValue(facts.SkillIdentity, out SkillDef skill);
            return new SkillRange
            {
                SkillLabel = skill == null ? facts.SkillDefName : SkillLabel(skill),
                SkillDefName = facts.SkillDefName,
                Floor = facts.Floor,
                Top = facts.Top,
                Gated = facts.Gated,
                Total = facts.Total,
            };
        }

        private static void FillCurveFacts(
            DefinitionSnapshot snapshot, GiverProfile profile, string giverDefName)
        {
            if (!snapshot.CurvesByGiver.TryGetValue(giverDefName, out CurveFacts facts)
                || !snapshot.StatsByName.TryGetValue(facts.StatDefName, out StatDef stat)) return;
            profile.CurveHeader = stat.LabelCap;
            profile.CurveRows = new List<(string, string)>();
            foreach ((int level, float value) in facts.Milestones)
                profile.CurveRows.Add((
                    "WR_TipLevelN".Translate(level).ToString(), value.ToStringPercent()));
        }

        // ----- Tooltip composition (cached; defs are session-fixed, a language
        // switch clears via InvalidateLanguageCaches) -----

        public static string GiverTip(string defName)
        {
            var profile = ForGiver(defName);
            if (profile == null) return null;
            StructuredTip tip = EnsureGiverTip(defName, profile);
            return profile.TipCache != tip.PlainText
                ? profile.TipCache
                : tip.PlainText;
        }

        internal static StructuredTip GiverStructuredTip(string defName)
        {
            var profile = ForGiver(defName);
            return profile == null ? null : EnsureGiverTip(defName, profile);
        }

        /// Giver tip with the covered-via-work-type hint folded in.
        internal static StructuredTip GiverCoveredStructuredTip(string defName)
        {
            var profile = ForGiver(defName);
            if (profile == null) return null;
            return profile.CoveredTipCache ??= new StructuredTip(
                $"job-giver:{defName}:covered",
                BuildGiverModel(profile,
                    "WR_CoveredByTypeTip".Translate().ToString()));
        }

        /// Giver tip with the dead-entry hint folded in.
        internal static StructuredTip GiverDeadStructuredTip(string defName)
        {
            var profile = ForGiver(defName);
            if (profile == null) return null;
            return profile.DeadTipCache ??= new StructuredTip(
                $"job-giver:{defName}:dead",
                BuildGiverModel(profile,
                    "WR_DeadEntryTip".Translate().ToString()));
        }

        private static StructuredTip EnsureGiverTip(
            string defName, GiverProfile profile)
        {
            if (profile.StructuredTipCache == null)
            {
                profile.StructuredTipCache = new StructuredTip(
                    $"job-giver:{defName}", BuildGiverModel(profile));
                if (profile.TipCache == null)
                    profile.TipCache = profile.StructuredTipCache.PlainText;
            }
            return profile.StructuredTipCache;
        }

        public static string WorkTypeTip(string defName)
        {
            var profile = ForWorkType(defName);
            if (profile == null) return null;
            StructuredTip tip = EnsureWorkTypeTip(defName, profile);
            return profile.TipCache != tip.PlainText
                ? profile.TipCache
                : tip.PlainText;
        }

        internal static StructuredTip WorkTypeStructuredTip(string defName)
        {
            var profile = ForWorkType(defName);
            return profile == null ? null : EnsureWorkTypeTip(defName, profile);
        }

        /// Work-type tip with the dead-entry hint folded in.
        internal static StructuredTip WorkTypeDeadStructuredTip(string defName)
        {
            var profile = ForWorkType(defName);
            if (profile == null) return null;
            return profile.DeadTipCache ??= new StructuredTip(
                $"work-type:{defName}:dead",
                BuildTypeModel(profile,
                    "WR_DeadEntryTip".Translate().ToString()));
        }

        private static StructuredTip EnsureWorkTypeTip(
            string defName, WorkTypeProfile profile)
        {
            if (profile.StructuredTipCache == null)
            {
                profile.StructuredTipCache = new StructuredTip(
                    $"work-type:{defName}", BuildTypeModel(profile));
                if (profile.TipCache == null)
                    profile.TipCache = profile.StructuredTipCache.PlainText;
            }
            return profile.StructuredTipCache;
        }

        private static TipModel BuildGiverModel(GiverProfile profile,
            string hint = null)
        {
            var model = new TipModel { Title = profile.Title };
            var facts = model.AddSection();
            if (profile.UsedSkills.Count > 0)
                facts.Fact("WR_TipSkillsLabel".Translate(), profile.UsedSkills.ToCommaList());
            facts.Fact("WR_TipTrainsLabel".Translate(), profile.TrainedSkills.Count == 0
                ? "WR_TipTrainsNothing".Translate().ToString()
                : profile.TrainedSkills.ToCommaList());
            if (profile.UnlockAge > 0)
                facts.Fact("WR_TipUnlockAgeLabel".Translate(),
                    "WR_TipUnlockAgeValue".Translate(profile.UnlockAge));
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
            if (hint != null)
                model.AddSection().Text(hint);
            if (profile.SourceLine != null)
                model.AddSection().Text(profile.SourceLine, dim: true);
            return model;
        }

        private static TipModel BuildTypeModel(WorkTypeProfile profile,
            string hint = null)
        {
            var model = new TipModel { Title = profile.Title };
            if (!profile.Description.NullOrEmpty())
                model.AddSection().Text(profile.Description);
            var facts = model.AddSection();
            if (profile.UsedSkills.Count > 0)
                facts.Fact("WR_TipSkillsLabel".Translate(), profile.UsedSkills.ToCommaList());
            facts.Fact("WR_TipXpLabel".Translate(),
                "WR_TipXpValue".Translate(profile.XpGivers, profile.TotalGivers));
            if (profile.UnlockAge > 0)
                facts.Fact("WR_TipUnlockAgeLabel".Translate(),
                    "WR_TipUnlockAgeValue".Translate(profile.UnlockAge));
            foreach (var range in profile.Requirements)
                facts.Fact("WR_TipRequiresLabel".Translate(),
                    "WR_TipReqItems".Translate(
                        range.SkillLabel, LevelRange(range), range.Gated).ToString());
            if (hint != null)
                model.AddSection().Text(hint);
            if (profile.SourceLine != null)
                model.AddSection().Text(profile.SourceLine, dim: true);
            return model;
        }

        private static string LevelRange(SkillRange range)
            => range.Floor == range.Top ? range.Top.ToString() : $"{range.Floor}-{range.Top}";
    }
}
