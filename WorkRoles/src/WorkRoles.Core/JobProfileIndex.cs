using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core
{
    /// Stable identity and invariant name for one SkillDef. The game adapter
    /// assigns Identity by object reference; Core never sees a Def instance.
    public readonly struct JobProfileSkillSource
    {
        public JobProfileSkillSource(int identity, string defName)
        {
            Identity = identity;
            DefName = defName;
        }

        public int Identity { get; }
        public string DefName { get; }
    }

    public readonly struct JobProfileSkillRequirementSource
    {
        public JobProfileSkillRequirementSource(int skillIdentity, string skillDefName, int minLevel)
        {
            SkillIdentity = skillIdentity;
            SkillDefName = skillDefName;
            MinLevel = minLevel;
        }

        public int SkillIdentity { get; }
        public string SkillDefName { get; }
        public int MinLevel { get; }
    }

    /// Immutable recipe facts. RecipeIdentity and RequiredWorkTypeIdentity are
    /// reference identities assigned by the adapter, preserving the game's
    /// Distinct and requiredGiverWorkType reference semantics.
    public sealed class JobProfileRecipeSource
    {
        public JobProfileRecipeSource(
            int recipeIdentity,
            int? requiredWorkTypeIdentity,
            JobProfileSkillSource? workSkill,
            float workSkillLearnFactor,
            IEnumerable<JobProfileSkillRequirementSource> skillRequirements,
            string? defName = null,
            RoleWorkEffect effects = RoleWorkEffect.Unspecified)
        {
            RecipeIdentity = recipeIdentity;
            RequiredWorkTypeIdentity = requiredWorkTypeIdentity;
            WorkSkill = workSkill;
            WorkSkillLearnFactor = workSkillLearnFactor;
            SkillRequirements = Copy(skillRequirements);
            DefName = defName;
            Effects = effects;
        }

        public int RecipeIdentity { get; }
        public int? RequiredWorkTypeIdentity { get; }
        public JobProfileSkillSource? WorkSkill { get; }
        public float WorkSkillLearnFactor { get; }
        public IReadOnlyList<JobProfileSkillRequirementSource> SkillRequirements { get; }
        /// Stable content name for spec/tooltip use; null on sources captured
        /// before content records existed (regenerated baselines fill it).
        public string? DefName { get; }
        /// Reduced effect kinds of WorkSkill on this recipe; display-only.
        public RoleWorkEffect Effects { get; }

        private static IReadOnlyList<JobProfileSkillRequirementSource> Copy(
            IEnumerable<JobProfileSkillRequirementSource> source) =>
            new ReadOnlyCollection<JobProfileSkillRequirementSource>(
                source == null
                    ? new List<JobProfileSkillRequirementSource>()
                    : new List<JobProfileSkillRequirementSource>(source));
    }

    [Flags]
    public enum JobProfileRecipeUserKind
    {
        None = 0,
        Humanlike = 1,
        Animal = 2,
        Mechanoid = 4,
    }

    /// One curated skill-to-effect association for a code-defined giver: the
    /// adapter wires the giver to its stats, the stat's declarative skill
    /// need supplies the skill, and the effect kind is display-only.
    public readonly struct JobProfileSkillEffect
    {
        public JobProfileSkillEffect(string skillDefName, RoleWorkEffect effects)
        {
            SkillDefName = skillDefName;
            Effects = effects;
        }

        public string SkillDefName { get; }
        public RoleWorkEffect Effects { get; }
    }

    /// One gate-bearing plant or buildable: content-level minimums that never
    /// promote to giver or role gates.
    public sealed class JobProfileContentGateFacts
    {
        public JobProfileContentGateFacts(string defName, int minLevel)
        {
            DefName = defName;
            MinLevel = minLevel;
        }

        public string DefName { get; }
        public int MinLevel { get; }
    }

    public sealed class JobProfileRequirementFacts
    {
        internal JobProfileRequirementFacts(
            int skillIdentity, string skillDefName,
            int floor, int top, int gated, int total)
        {
            SkillIdentity = skillIdentity;
            SkillDefName = skillDefName;
            Floor = floor;
            Top = top;
            Gated = gated;
            Total = total;
        }

        public int SkillIdentity { get; }
        public string SkillDefName { get; }
        public int Floor { get; }
        public int Top { get; }
        public int Gated { get; }
        public int Total { get; }
    }

    public sealed class JobProfileGiverFacts
    {
        internal JobProfileGiverFacts(
            string defName,
            int workTypeIdentity,
            IReadOnlyList<int> usedSkillIdentities,
            IReadOnlyList<string> usedSkillDefNames,
            IReadOnlyList<int> trainedSkillIdentities,
            IReadOnlyList<string> trainedSkillDefNames,
            IReadOnlyList<int> relevantSkillIdentities,
            IReadOnlyList<string> relevantSkillDefNames,
            IReadOnlyList<int> recipeIdentities,
            bool hasCuratedUsedSkills,
            bool hasCuratedXp,
            bool usesRecipes,
            bool givesXp,
            IReadOnlyList<JobProfileRequirementFacts> requirements,
            IReadOnlyList<JobProfileSkillEffect>? curatedSkillEffects = null)
        {
            CuratedSkillEffects = curatedSkillEffects
                ?? Array.Empty<JobProfileSkillEffect>();
            DefName = defName;
            WorkTypeIdentity = workTypeIdentity;
            UsedSkillIdentities = usedSkillIdentities;
            UsedSkillDefNames = usedSkillDefNames;
            TrainedSkillIdentities = trainedSkillIdentities;
            TrainedSkillDefNames = trainedSkillDefNames;
            RelevantSkillIdentities = relevantSkillIdentities;
            RelevantSkillDefNames = relevantSkillDefNames;
            RecipeIdentities = recipeIdentities;
            HasCuratedUsedSkills = hasCuratedUsedSkills;
            HasCuratedXp = hasCuratedXp;
            UsesRecipes = usesRecipes;
            GivesXp = givesXp;
            Requirements = requirements;
        }

        public string DefName { get; }
        public int WorkTypeIdentity { get; }
        /// Ordered skill identities used to compose localized labels. Bill
        /// facts deliberately retain repetitions because labels deduplicate
        /// only after translation in the mutable facade.
        public IReadOnlyList<int> UsedSkillIdentities { get; }
        public IReadOnlyList<string> UsedSkillDefNames { get; }
        public IReadOnlyList<int> TrainedSkillIdentities { get; }
        public IReadOnlyList<string> TrainedSkillDefNames { get; }
        public IReadOnlyList<int> RelevantSkillIdentities { get; }
        public IReadOnlyList<string> RelevantSkillDefNames { get; }
        /// Exact recipes reachable by this giver after required-work-type
        /// filtering, in the same stable order used for aggregate facts.
        public IReadOnlyList<int> RecipeIdentities { get; }
        public bool HasCuratedUsedSkills { get; }
        public bool HasCuratedXp { get; }
        public bool UsesRecipes { get; }
        public bool HasExactUsedSkills => UsesRecipes || HasCuratedUsedSkills;
        public bool HasExactTrainedSkills => UsesRecipes || HasCuratedXp;
        public bool GivesXp { get; }
        public IReadOnlyList<JobProfileRequirementFacts> Requirements { get; }
        /// Adapter-curated effect kinds per used skill; display-only facts
        /// merged into the spec's used-skill entries.
        public IReadOnlyList<JobProfileSkillEffect> CuratedSkillEffects { get; }
    }

    public sealed class JobProfileWorkTypeFacts
    {
        internal JobProfileWorkTypeFacts(
            string defName,
            int workTypeIdentity,
            IReadOnlyList<int> relevantSkillIdentities,
            IReadOnlyList<string> relevantSkillDefNames,
            IReadOnlyList<string> usedSkillDefNames,
            IReadOnlyList<string> trainedSkillDefNames,
            bool hasExactUsedSkills,
            bool hasExactTrainedSkills,
            IReadOnlyList<string> giverDefNames,
            int xpGivers,
            IReadOnlyList<JobProfileRequirementFacts> requirements)
        {
            DefName = defName;
            WorkTypeIdentity = workTypeIdentity;
            RelevantSkillIdentities = relevantSkillIdentities;
            RelevantSkillDefNames = relevantSkillDefNames;
            UsedSkillDefNames = usedSkillDefNames;
            TrainedSkillDefNames = trainedSkillDefNames;
            HasExactUsedSkills = hasExactUsedSkills;
            HasExactTrainedSkills = hasExactTrainedSkills;
            GiverDefNames = giverDefNames;
            XpGivers = xpGivers;
            Requirements = requirements;
        }

        public string DefName { get; }
        public int WorkTypeIdentity { get; }
        public IReadOnlyList<int> RelevantSkillIdentities { get; }
        public IReadOnlyList<string> RelevantSkillDefNames { get; }
        public IReadOnlyList<string> UsedSkillDefNames { get; }
        public IReadOnlyList<string> TrainedSkillDefNames { get; }
        public bool HasExactUsedSkills { get; }
        public bool HasExactTrainedSkills { get; }
        /// Resolved declared members, including duplicate names, in the work
        /// type snapshot's original priority order.
        public IReadOnlyList<string> GiverDefNames { get; }
        public int XpGivers { get; }
        public int TotalGivers => GiverDefNames.Count;
        public IReadOnlyList<JobProfileRequirementFacts> Requirements { get; }
    }

    /// Immutable, language-independent snapshot of all facts consumed by the
    /// game-facing mutable/localized profile facade.
    public sealed class JobProfileIndex
    {
        internal JobProfileIndex(
            IDictionary<string, JobProfileGiverFacts> givers,
            IDictionary<string, JobProfileWorkTypeFacts> workTypes,
            IDictionary<int, JobProfileRecipeSource> recipes,
            IDictionary<int, IReadOnlyList<int>> recipesByUser,
            IDictionary<int, IReadOnlyList<int>> recipesBySkill,
            JobProfileRequirementFacts constructionRequirement,
            JobProfileRequirementFacts sowingRequirement,
            IReadOnlyList<JobProfileContentGateFacts>? constructionGates = null,
            IReadOnlyList<JobProfileContentGateFacts>? sowingGates = null)
        {
            ConstructionGates = constructionGates
                ?? Array.Empty<JobProfileContentGateFacts>();
            SowingGates = sowingGates
                ?? Array.Empty<JobProfileContentGateFacts>();
            Givers = new ReadOnlyDictionary<string, JobProfileGiverFacts>(
                new Dictionary<string, JobProfileGiverFacts>(givers, StringComparer.Ordinal));
            WorkTypes = new ReadOnlyDictionary<string, JobProfileWorkTypeFacts>(
                new Dictionary<string, JobProfileWorkTypeFacts>(workTypes, StringComparer.Ordinal));
            Recipes = new ReadOnlyDictionary<int, JobProfileRecipeSource>(
                new Dictionary<int, JobProfileRecipeSource>(recipes));
            RecipesByUser = new ReadOnlyDictionary<int, IReadOnlyList<int>>(
                new Dictionary<int, IReadOnlyList<int>>(recipesByUser));
            RecipesBySkill = new ReadOnlyDictionary<int, IReadOnlyList<int>>(
                new Dictionary<int, IReadOnlyList<int>>(recipesBySkill));
            ConstructionRequirement = constructionRequirement;
            SowingRequirement = sowingRequirement;
        }

        public IReadOnlyDictionary<string, JobProfileGiverFacts> Givers { get; }
        public IReadOnlyDictionary<string, JobProfileWorkTypeFacts> WorkTypes { get; }
        /// Immutable source facts for every observed recipe, keyed by adapter
        /// reference identity. This retains work skill, learn factor, explicit
        /// skill gates, and required work type without re-reading game defs.
        public IReadOnlyDictionary<int, JobProfileRecipeSource> Recipes { get; }
        public IReadOnlyDictionary<int, IReadOnlyList<int>> RecipesByUser { get; }
        public IReadOnlyDictionary<int, IReadOnlyList<int>> RecipesBySkill { get; }
        public JobProfileRequirementFacts ConstructionRequirement { get; }
        public JobProfileRequirementFacts SowingRequirement { get; }
        /// Per-content minimums behind ConstructFinishFrames and GrowerSow.
        public IReadOnlyList<JobProfileContentGateFacts> ConstructionGates { get; }
        public IReadOnlyList<JobProfileContentGateFacts> SowingGates { get; }
    }

    /// Single-use collector. The game adapter visits each Def collection once;
    /// Build then resolves all reverse associations without any game database or
    /// localization dependency.
    public sealed class JobProfileIndexBuilder
    {
        private sealed class GiverSource
        {
            internal string DefName = null!;               // always set at creation
            internal int WorkTypeIdentity;
            internal List<JobProfileSkillSource> RelevantSkills = null!;
            internal List<int> FixedRecipeUserIdentities = null!;
            internal bool AllHumanlikes;
            internal bool AllAnimals;
            internal bool AllMechanoids;
            internal bool HasCuratedUsedSkills;
            internal List<string> CuratedUsedSkillDefNames = null!;
            internal bool HasCuratedXp;
            internal List<string> CuratedXpSkillDefNames = null!;
            internal List<JobProfileSkillEffect> CuratedSkillEffects = null!;
        }

        private sealed class WorkTypeSource
        {
            internal int Identity;
            internal string DefName = null!;               // always set at creation
            internal List<JobProfileSkillSource> RelevantSkills = null!;
            internal List<string> GiverDefNames = null!;
        }

        private sealed class RecipeUserSource
        {
            internal int Identity;
            internal JobProfileRecipeUserKind Kind;
            internal List<int> DirectRecipeIdentities = null!; // always set at creation
        }

        private sealed class DatabaseRecipeSource
        {
            internal int RecipeIdentity;
            internal List<int> UserIdentities = null!;     // always set at creation
        }

        private sealed class MutableRequirement
        {
            internal int SkillIdentity;
            internal string SkillDefName = null!;          // always set at creation
            internal int Floor;
            internal int Top;
            internal int Gated;
        }

        private readonly List<GiverSource> givers = new List<GiverSource>();
        private readonly List<WorkTypeSource> workTypes = new List<WorkTypeSource>();
        private readonly List<RecipeUserSource> recipeUsers = new List<RecipeUserSource>();
        private readonly Dictionary<int, RecipeUserSource> recipeUsersByIdentity =
            new Dictionary<int, RecipeUserSource>();
        private readonly List<int> humanlikeRecipeUsers = new List<int>();
        private readonly List<int> animalRecipeUsers = new List<int>();
        private readonly List<int> mechanoidRecipeUsers = new List<int>();
        private readonly Dictionary<int, JobProfileRecipeSource> recipes =
            new Dictionary<int, JobProfileRecipeSource>();
        private readonly List<int> recipeOrder = new List<int>();
        private readonly List<DatabaseRecipeSource> databaseRecipes =
            new List<DatabaseRecipeSource>();
        private readonly List<int> constructionLevels = new List<int>();
        private readonly List<int> sowingLevels = new List<int>();
        private readonly List<JobProfileContentGateFacts> constructionGates =
            new List<JobProfileContentGateFacts>();
        private readonly List<JobProfileContentGateFacts> sowingGates =
            new List<JobProfileContentGateFacts>();
        private JobProfileSkillSource constructionSkill;
        private JobProfileSkillSource sowingSkill;
        private bool built;

        public void AddWorkType(
            int workTypeIdentity,
            string defName,
            IEnumerable<JobProfileSkillSource> relevantSkills,
            IEnumerable<string> giverDefNames)
        {
            EnsureMutable();
            workTypes.Add(new WorkTypeSource
            {
                Identity = workTypeIdentity,
                DefName = defName,
                RelevantSkills = Copy(relevantSkills),
                GiverDefNames = Copy(giverDefNames),
            });
        }

        public void AddGiver(
            string defName,
            int workTypeIdentity,
            IEnumerable<JobProfileSkillSource> relevantSkills,
            IEnumerable<int>? fixedRecipeUserIds = null,
            bool allHumanlikes = false,
            bool allAnimals = false,
            bool allMechanoids = false,
            bool hasCuratedXp = false,
            IEnumerable<string>? curatedXpSkillDefNames = null,
            bool hasCuratedUsedSkills = false,
            IEnumerable<string>? curatedUsedSkillDefNames = null,
            IEnumerable<JobProfileSkillEffect>? curatedSkillEffects = null)
        {
            EnsureMutable();
            givers.Add(new GiverSource
            {
                DefName = defName,
                WorkTypeIdentity = workTypeIdentity,
                RelevantSkills = Copy(relevantSkills),
                FixedRecipeUserIdentities = Copy(fixedRecipeUserIds),
                AllHumanlikes = allHumanlikes,
                AllAnimals = allAnimals,
                AllMechanoids = allMechanoids,
                HasCuratedUsedSkills = hasCuratedUsedSkills,
                CuratedUsedSkillDefNames = Copy(curatedUsedSkillDefNames),
                HasCuratedXp = hasCuratedXp,
                CuratedXpSkillDefNames = Copy(curatedXpSkillDefNames),
                CuratedSkillEffects = Copy(curatedSkillEffects),
            });
        }

        public void AddRecipeUser(
            int recipeUserIdentity,
            JobProfileRecipeUserKind kind,
            IEnumerable<int> directRecipeIdentities)
        {
            EnsureMutable();
            var source = new RecipeUserSource
            {
                Identity = recipeUserIdentity,
                Kind = kind,
                DirectRecipeIdentities = Copy(directRecipeIdentities),
            };
            if (!recipeUsersByIdentity.ContainsKey(recipeUserIdentity))
            {
                recipeUsers.Add(source);
                if ((kind & JobProfileRecipeUserKind.Humanlike) != 0)
                    humanlikeRecipeUsers.Add(recipeUserIdentity);
                if ((kind & JobProfileRecipeUserKind.Animal) != 0)
                    animalRecipeUsers.Add(recipeUserIdentity);
                if ((kind & JobProfileRecipeUserKind.Mechanoid) != 0)
                    mechanoidRecipeUsers.Add(recipeUserIdentity);
            }
            recipeUsersByIdentity[recipeUserIdentity] = source;
        }

        public void AddRecipe(JobProfileRecipeSource recipe)
        {
            EnsureMutable();
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (!recipes.ContainsKey(recipe.RecipeIdentity))
                recipeOrder.Add(recipe.RecipeIdentity);
            recipes[recipe.RecipeIdentity] = recipe;
        }

        public void AddDatabaseRecipe(
            JobProfileRecipeSource recipe,
            IEnumerable<int> recipeUserIdentities)
        {
            AddRecipe(recipe);
            databaseRecipes.Add(new DatabaseRecipeSource
            {
                RecipeIdentity = recipe.RecipeIdentity,
                UserIdentities = Copy(recipeUserIdentities),
            });
        }

        public void SetConstructionSkill(int skillIdentity, string skillDefName)
        {
            EnsureMutable();
            constructionSkill = new JobProfileSkillSource(skillIdentity, skillDefName);
        }

        public void SetSowingSkill(int skillIdentity, string skillDefName)
        {
            EnsureMutable();
            sowingSkill = new JobProfileSkillSource(skillIdentity, skillDefName);
        }

        public void AddConstructionRequirement(int level)
        {
            EnsureMutable();
            if (level > 0) constructionLevels.Add(level);
        }

        public void AddSowingRequirement(int level)
        {
            EnsureMutable();
            if (level > 0) sowingLevels.Add(level);
        }

        /// Content-record variants: retain the gated def's identity for spec
        /// units and tooltips while feeding the aggregate range facts.
        public void AddConstructionGate(string defName, int level)
        {
            AddConstructionRequirement(level);
            if (level > 0 && !string.IsNullOrEmpty(defName))
                constructionGates.Add(
                    new JobProfileContentGateFacts(defName, level));
        }

        public void AddSowingGate(string defName, int level)
        {
            AddSowingRequirement(level);
            if (level > 0 && !string.IsNullOrEmpty(defName))
                sowingGates.Add(new JobProfileContentGateFacts(defName, level));
        }

        public JobProfileIndex Build()
        {
            EnsureMutable();
            built = true;

            Dictionary<int, List<int>> mutableRecipesByUser = BuildRecipesByUser();
            Dictionary<string, JobProfileGiverFacts> giverFacts =
                BuildGiverFacts(mutableRecipesByUser);
            Dictionary<string, JobProfileWorkTypeFacts> workTypeFacts =
                BuildWorkTypeFacts(giverFacts);

            return new JobProfileIndex(
                giverFacts,
                workTypeFacts,
                recipes,
                FreezeLists(mutableRecipesByUser),
                BuildRecipesBySkill(),
                RangeOf(constructionLevels, constructionSkill),
                RangeOf(sowingLevels, sowingSkill),
                ReadOnly(constructionGates),
                ReadOnly(sowingGates));
        }

        private Dictionary<int, List<int>> BuildRecipesByUser()
        {
            var result = new Dictionary<int, List<int>>();
            for (int i = 0; i < recipeUsers.Count; i++)
            {
                RecipeUserSource source = recipeUsers[i];
                result[source.Identity] = new List<int>(source.DirectRecipeIdentities);
            }

            for (int i = 0; i < databaseRecipes.Count; i++)
            {
                DatabaseRecipeSource source = databaseRecipes[i];
                var seenUsers = new HashSet<int>();
                for (int j = 0; j < source.UserIdentities.Count; j++)
                {
                    int user = source.UserIdentities[j];
                    if (!seenUsers.Add(user)) continue;
                    if (!result.TryGetValue(user, out List<int> userRecipes))
                    {
                        userRecipes = new List<int>();
                        result.Add(user, userRecipes);
                    }
                    userRecipes.Add(source.RecipeIdentity);
                }
            }
            return result;
        }

        private Dictionary<string, JobProfileGiverFacts> BuildGiverFacts(
            Dictionary<int, List<int>> recipesByUser)
        {
            var result = new Dictionary<string, JobProfileGiverFacts>(StringComparer.Ordinal);
            for (int i = 0; i < givers.Count; i++)
            {
                GiverSource source = givers[i];
                result[source.DefName] = BuildGiver(source, recipesByUser);
            }
            return result;
        }

        private JobProfileGiverFacts BuildGiver(
            GiverSource source,
            Dictionary<int, List<int>> recipesByUser)
        {
            var relevantIds = SkillIdentities(source.RelevantSkills);
            var relevantNames = SkillNames(source.RelevantSkills);
            var benchIds = new List<int>(source.FixedRecipeUserIdentities);
            if (source.AllHumanlikes) benchIds.AddRange(humanlikeRecipeUsers);
            if (source.AllAnimals) benchIds.AddRange(animalRecipeUsers);
            if (source.AllMechanoids) benchIds.AddRange(mechanoidRecipeUsers);

            var giverRecipeIds = new List<int>();
            var seenRecipes = new HashSet<int>();
            for (int i = 0; i < benchIds.Count; i++)
            {
                if (!recipesByUser.TryGetValue(benchIds[i], out List<int> userRecipes)) continue;
                for (int j = 0; j < userRecipes.Count; j++)
                {
                    int recipeIdentity = userRecipes[j];
                    if (!seenRecipes.Add(recipeIdentity)
                        || !recipes.TryGetValue(recipeIdentity, out JobProfileRecipeSource recipe))
                        continue;
                    if (recipe.RequiredWorkTypeIdentity.HasValue
                        && recipe.RequiredWorkTypeIdentity.Value != source.WorkTypeIdentity)
                        continue;
                    giverRecipeIds.Add(recipeIdentity);
                }
            }

            bool usesRecipes = giverRecipeIds.Count > 0;
            List<int> usedIds;
            List<string> usedNames;
            List<int> trainedIds;
            List<string> trainedNames;
            List<JobProfileRequirementFacts> requirements;
            if (usesRecipes)
            {
                usedIds = RecipeSkillIdentities(giverRecipeIds, trainedOnly: false);
                usedNames = DistinctRecipeSkillNames(giverRecipeIds, trainedOnly: false);
                trainedIds = RecipeSkillIdentities(giverRecipeIds, trainedOnly: true);
                trainedNames = DistinctRecipeSkillNames(giverRecipeIds, trainedOnly: true);
                requirements = RecipeRequirements(giverRecipeIds);
            }
            else
            {
                requirements = new List<JobProfileRequirementFacts>();
                if (source.HasCuratedUsedSkills)
                {
                    // Code-defined jobs carry no declarative skill-use data.
                    // An exact curated list, including an exact empty list,
                    // must win over broad parent-work-type metadata.
                    usedIds = new List<int>();
                    usedNames = new List<string>(source.CuratedUsedSkillDefNames);
                }
                else if (source.HasCuratedXp)
                {
                    // Backward-compatible XP-as-use inference for callers that
                    // have not supplied independent exact used-skill facts.
                    usedIds = new List<int>();
                    usedNames = new List<string>(source.CuratedXpSkillDefNames);
                }
                else
                {
                    usedIds = relevantIds;
                    usedNames = relevantNames;
                }

                if (source.HasCuratedXp)
                {
                    // XP is independently curated because a job can use a
                    // skill without training it. Identities are unknown for
                    // curated names; consumers resolve them by defName.
                    trainedIds = new List<int>();
                    trainedNames = new List<string>(source.CuratedXpSkillDefNames);
                }
                else
                {
                    trainedIds = new List<int>(relevantIds);
                    trainedNames = new List<string>(relevantNames);
                }

                if (source.DefName == "ConstructFinishFrames")
                    requirements.Add(RangeOf(constructionLevels, constructionSkill));
                else if (source.DefName == "GrowerSow")
                    requirements.Add(RangeOf(sowingLevels, sowingSkill));
            }

            return new JobProfileGiverFacts(
                source.DefName,
                source.WorkTypeIdentity,
                ReadOnly(usedIds),
                ReadOnly(usedNames),
                ReadOnly(trainedIds),
                ReadOnly(trainedNames),
                ReadOnly(relevantIds),
                ReadOnly(relevantNames),
                ReadOnly(giverRecipeIds),
                source.HasCuratedUsedSkills,
                source.HasCuratedXp,
                usesRecipes,
                trainedNames.Count > 0,
                ReadOnly(requirements),
                ReadOnly(source.CuratedSkillEffects));
        }

        private List<int> RecipeSkillIdentities(List<int> recipeIdentities, bool trainedOnly)
        {
            var result = new List<int>();
            for (int i = 0; i < recipeIdentities.Count; i++)
            {
                JobProfileRecipeSource recipe = recipes[recipeIdentities[i]];
                if (!recipe.WorkSkill.HasValue
                    || (trainedOnly && recipe.WorkSkillLearnFactor <= 0f))
                    continue;
                result.Add(recipe.WorkSkill.Value.Identity);
            }
            return result;
        }

        private List<string> DistinctRecipeSkillNames(
            List<int> recipeIdentities, bool trainedOnly)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool seenNull = false;
            for (int i = 0; i < recipeIdentities.Count; i++)
            {
                JobProfileRecipeSource recipe = recipes[recipeIdentities[i]];
                if (!recipe.WorkSkill.HasValue
                    || (trainedOnly && recipe.WorkSkillLearnFactor <= 0f))
                    continue;
                string name = recipe.WorkSkill.Value.DefName;
                if (name == null ? seenNull : !seen.Add(name)) continue;
                if (name == null) seenNull = true;
                result.Add(name!); // a single null entry is deliberately retained (deduped via seenNull)
            }
            return result;
        }

        private List<JobProfileRequirementFacts> RecipeRequirements(List<int> recipeIdentities)
        {
            var groups = new List<MutableRequirement>();
            var groupBySkill = new Dictionary<int, MutableRequirement>();
            for (int i = 0; i < recipeIdentities.Count; i++)
            {
                IReadOnlyList<JobProfileSkillRequirementSource> requirements =
                    recipes[recipeIdentities[i]].SkillRequirements;
                for (int j = 0; j < requirements.Count; j++)
                {
                    JobProfileSkillRequirementSource requirement = requirements[j];
                    if (!groupBySkill.TryGetValue(requirement.SkillIdentity,
                            out MutableRequirement group))
                    {
                        group = new MutableRequirement
                        {
                            SkillIdentity = requirement.SkillIdentity,
                            SkillDefName = requirement.SkillDefName,
                            Floor = requirement.MinLevel,
                            Top = requirement.MinLevel,
                        };
                        groupBySkill.Add(requirement.SkillIdentity, group);
                        groups.Add(group);
                    }
                    if (requirement.MinLevel < group.Floor) group.Floor = requirement.MinLevel;
                    if (requirement.MinLevel > group.Top) group.Top = requirement.MinLevel;
                    group.Gated++;
                }
            }

            var result = new List<JobProfileRequirementFacts>(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                MutableRequirement group = groups[i];
                result.Add(new JobProfileRequirementFacts(
                    group.SkillIdentity, group.SkillDefName,
                    group.Floor, group.Top, group.Gated, recipeIdentities.Count));
            }
            return result;
        }

        private Dictionary<string, JobProfileWorkTypeFacts> BuildWorkTypeFacts(
            Dictionary<string, JobProfileGiverFacts> giverFacts)
        {
            var result = new Dictionary<string, JobProfileWorkTypeFacts>(StringComparer.Ordinal);
            for (int i = 0; i < workTypes.Count; i++)
            {
                WorkTypeSource source = workTypes[i];
                var members = new List<string>();
                int xpGivers = 0;
                var requirements = new List<JobProfileRequirementFacts>();
                var usedNames = new List<string>();
                var trainedNames = new List<string>();
                var seenUsedNames = new HashSet<string>(StringComparer.Ordinal);
                var seenTrainedNames = new HashSet<string>(StringComparer.Ordinal);
                bool hasExactUsedSkills = true;
                bool hasExactTrainedSkills = true;
                for (int j = 0; j < source.GiverDefNames.Count; j++)
                {
                    string name = source.GiverDefNames[j];
                    if (!giverFacts.TryGetValue(name, out JobProfileGiverFacts giver)) continue;
                    members.Add(name);
                    if (giver.GivesXp) xpGivers++;
                    if (!giver.HasExactUsedSkills) hasExactUsedSkills = false;
                    if (!giver.HasExactTrainedSkills) hasExactTrainedSkills = false;
                    AddDistinctNames(usedNames, seenUsedNames,
                        giver.UsedSkillDefNames);
                    AddDistinctNames(trainedNames, seenTrainedNames,
                        giver.TrainedSkillDefNames);
                    AddWorkTypeRequirements(requirements, giver.Requirements);
                }

                result[source.DefName] = new JobProfileWorkTypeFacts(
                    source.DefName,
                    source.Identity,
                    ReadOnly(SkillIdentities(source.RelevantSkills)),
                    ReadOnly(SkillNames(source.RelevantSkills)),
                    ReadOnly(usedNames),
                    ReadOnly(trainedNames),
                    hasExactUsedSkills,
                    hasExactTrainedSkills,
                    ReadOnly(members),
                    xpGivers,
                    ReadOnly(requirements));
            }
            return result;
        }

        private static void AddDistinctNames(
            List<string> target,
            HashSet<string> seen,
            IReadOnlyList<string> source)
        {
            for (int i = 0; i < source.Count; i++)
                if (seen.Add(source[i])) target.Add(source[i]);
        }

        /// Work-type rows keep real aggregates: the floor is the lowest gate
        /// among the member givers' gated content (a gate is always a
        /// minimum, never "from zero"), and the gated count sums the
        /// members' gated content.
        private static void AddWorkTypeRequirements(
            List<JobProfileRequirementFacts> target,
            IReadOnlyList<JobProfileRequirementFacts> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                JobProfileRequirementFacts requirement = source[i];
                int existing = -1;
                for (int j = 0; j < target.Count; j++)
                    if (string.Equals(target[j].SkillDefName,
                            requirement.SkillDefName, StringComparison.Ordinal))
                    {
                        existing = j;
                        break;
                    }

                if (existing < 0)
                    target.Add(new JobProfileRequirementFacts(
                        requirement.SkillIdentity, requirement.SkillDefName,
                        requirement.Floor, requirement.Top,
                        requirement.Gated, requirement.Total));
                else
                {
                    JobProfileRequirementFacts first = target[existing];
                    target[existing] = new JobProfileRequirementFacts(
                        first.SkillIdentity, first.SkillDefName,
                        Math.Min(first.Floor, requirement.Floor),
                        Math.Max(first.Top, requirement.Top),
                        first.Gated + requirement.Gated,
                        first.Total + requirement.Total);
                }
            }
        }

        private IDictionary<int, IReadOnlyList<int>> BuildRecipesBySkill()
        {
            var mutable = new Dictionary<int, List<int>>();
            for (int i = 0; i < recipeOrder.Count; i++)
            {
                int recipeIdentity = recipeOrder[i];
                JobProfileSkillSource? skill = recipes[recipeIdentity].WorkSkill;
                if (!skill.HasValue) continue;
                if (!mutable.TryGetValue(skill.Value.Identity, out List<int> skillRecipes))
                {
                    skillRecipes = new List<int>();
                    mutable.Add(skill.Value.Identity, skillRecipes);
                }
                skillRecipes.Add(recipeIdentity);
            }
            return FreezeLists(mutable);
        }

        private static JobProfileRequirementFacts RangeOf(
            List<int> levels, JobProfileSkillSource skill)
        {
            int floor = 0;
            int top = 0;
            if (levels.Count > 0)
            {
                floor = levels[0];
                top = levels[0];
                for (int i = 1; i < levels.Count; i++)
                {
                    if (levels[i] < floor) floor = levels[i];
                    if (levels[i] > top) top = levels[i];
                }
            }
            return new JobProfileRequirementFacts(
                skill.Identity, skill.DefName, floor, top, levels.Count, 0);
        }

        private static List<int> SkillIdentities(List<JobProfileSkillSource> skills)
        {
            var result = new List<int>(skills.Count);
            for (int i = 0; i < skills.Count; i++) result.Add(skills[i].Identity);
            return result;
        }

        private static List<string> SkillNames(List<JobProfileSkillSource> skills)
        {
            var result = new List<string>(skills.Count);
            for (int i = 0; i < skills.Count; i++) result.Add(skills[i].DefName);
            return result;
        }

        private static IDictionary<int, IReadOnlyList<int>> FreezeLists(
            Dictionary<int, List<int>> source)
        {
            var result = new Dictionary<int, IReadOnlyList<int>>();
            foreach (KeyValuePair<int, List<int>> pair in source)
                result.Add(pair.Key, ReadOnly(pair.Value));
            return result;
        }

        private static IReadOnlyList<T> ReadOnly<T>(List<T> source) =>
            new ReadOnlyCollection<T>(source);

        private static List<T> Copy<T>(IEnumerable<T>? source) =>
            source == null ? new List<T>() : new List<T>(source);

        private void EnsureMutable()
        {
            if (built) throw new InvalidOperationException("This profile index builder has already built its snapshot.");
        }
    }
}
