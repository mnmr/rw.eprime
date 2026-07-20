using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
            IEnumerable<JobProfileSkillRequirementSource> skillRequirements)
        {
            RecipeIdentity = recipeIdentity;
            RequiredWorkTypeIdentity = requiredWorkTypeIdentity;
            WorkSkill = workSkill;
            WorkSkillLearnFactor = workSkillLearnFactor;
            SkillRequirements = Copy(skillRequirements);
        }

        public int RecipeIdentity { get; }
        public int? RequiredWorkTypeIdentity { get; }
        public JobProfileSkillSource? WorkSkill { get; }
        public float WorkSkillLearnFactor { get; }
        public IReadOnlyList<JobProfileSkillRequirementSource> SkillRequirements { get; }

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
            bool hasCuratedXp,
            bool usesRecipes,
            bool givesXp,
            IReadOnlyList<JobProfileRequirementFacts> requirements)
        {
            DefName = defName;
            WorkTypeIdentity = workTypeIdentity;
            UsedSkillIdentities = usedSkillIdentities;
            UsedSkillDefNames = usedSkillDefNames;
            TrainedSkillIdentities = trainedSkillIdentities;
            TrainedSkillDefNames = trainedSkillDefNames;
            RelevantSkillIdentities = relevantSkillIdentities;
            RelevantSkillDefNames = relevantSkillDefNames;
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
        public bool HasCuratedXp { get; }
        public bool UsesRecipes { get; }
        public bool GivesXp { get; }
        public IReadOnlyList<JobProfileRequirementFacts> Requirements { get; }
    }

    public sealed class JobProfileWorkTypeFacts
    {
        internal JobProfileWorkTypeFacts(
            string defName,
            int workTypeIdentity,
            IReadOnlyList<int> relevantSkillIdentities,
            IReadOnlyList<string> relevantSkillDefNames,
            IReadOnlyList<string> giverDefNames,
            int xpGivers,
            IReadOnlyList<JobProfileRequirementFacts> requirements)
        {
            DefName = defName;
            WorkTypeIdentity = workTypeIdentity;
            RelevantSkillIdentities = relevantSkillIdentities;
            RelevantSkillDefNames = relevantSkillDefNames;
            GiverDefNames = giverDefNames;
            XpGivers = xpGivers;
            Requirements = requirements;
        }

        public string DefName { get; }
        public int WorkTypeIdentity { get; }
        public IReadOnlyList<int> RelevantSkillIdentities { get; }
        public IReadOnlyList<string> RelevantSkillDefNames { get; }
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
            IDictionary<int, IReadOnlyList<int>> recipesByUser,
            IDictionary<int, IReadOnlyList<int>> recipesBySkill,
            JobProfileRequirementFacts constructionRequirement,
            JobProfileRequirementFacts sowingRequirement)
        {
            Givers = new ReadOnlyDictionary<string, JobProfileGiverFacts>(
                new Dictionary<string, JobProfileGiverFacts>(givers, StringComparer.Ordinal));
            WorkTypes = new ReadOnlyDictionary<string, JobProfileWorkTypeFacts>(
                new Dictionary<string, JobProfileWorkTypeFacts>(workTypes, StringComparer.Ordinal));
            RecipesByUser = new ReadOnlyDictionary<int, IReadOnlyList<int>>(
                new Dictionary<int, IReadOnlyList<int>>(recipesByUser));
            RecipesBySkill = new ReadOnlyDictionary<int, IReadOnlyList<int>>(
                new Dictionary<int, IReadOnlyList<int>>(recipesBySkill));
            ConstructionRequirement = constructionRequirement;
            SowingRequirement = sowingRequirement;
        }

        public IReadOnlyDictionary<string, JobProfileGiverFacts> Givers { get; }
        public IReadOnlyDictionary<string, JobProfileWorkTypeFacts> WorkTypes { get; }
        public IReadOnlyDictionary<int, IReadOnlyList<int>> RecipesByUser { get; }
        public IReadOnlyDictionary<int, IReadOnlyList<int>> RecipesBySkill { get; }
        public JobProfileRequirementFacts ConstructionRequirement { get; }
        public JobProfileRequirementFacts SowingRequirement { get; }
    }

    /// Single-use collector. The game adapter visits each Def collection once;
    /// Build then resolves all reverse associations without any game database or
    /// localization dependency.
    public sealed class JobProfileIndexBuilder
    {
        private sealed class GiverSource
        {
            internal string DefName;
            internal int WorkTypeIdentity;
            internal List<JobProfileSkillSource> RelevantSkills;
            internal List<int> FixedRecipeUserIdentities;
            internal bool AllHumanlikes;
            internal bool AllAnimals;
            internal bool AllMechanoids;
            internal bool HasCuratedXp;
            internal List<string> CuratedXpSkillDefNames;
        }

        private sealed class WorkTypeSource
        {
            internal int Identity;
            internal string DefName;
            internal List<JobProfileSkillSource> RelevantSkills;
            internal List<string> GiverDefNames;
        }

        private sealed class RecipeUserSource
        {
            internal int Identity;
            internal JobProfileRecipeUserKind Kind;
            internal List<int> DirectRecipeIdentities;
        }

        private sealed class DatabaseRecipeSource
        {
            internal int RecipeIdentity;
            internal List<int> UserIdentities;
        }

        private sealed class MutableRequirement
        {
            internal int SkillIdentity;
            internal string SkillDefName;
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
            IEnumerable<int> fixedRecipeUserIds = null,
            bool allHumanlikes = false,
            bool allAnimals = false,
            bool allMechanoids = false,
            bool hasCuratedXp = false,
            IEnumerable<string> curatedXpSkillDefNames = null)
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
                HasCuratedXp = hasCuratedXp,
                CuratedXpSkillDefNames = Copy(curatedXpSkillDefNames),
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
                FreezeLists(mutableRecipesByUser),
                BuildRecipesBySkill(),
                RangeOf(constructionLevels, constructionSkill),
                RangeOf(sowingLevels, sowingSkill));
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
                usedIds = relevantIds;
                usedNames = relevantNames;
                requirements = new List<JobProfileRequirementFacts>();
                if (source.HasCuratedXp)
                {
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
                source.HasCuratedXp,
                usesRecipes,
                trainedNames.Count > 0,
                ReadOnly(requirements));
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
                result.Add(name);
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
                for (int j = 0; j < source.GiverDefNames.Count; j++)
                {
                    string name = source.GiverDefNames[j];
                    if (!giverFacts.TryGetValue(name, out JobProfileGiverFacts giver)) continue;
                    members.Add(name);
                    if (giver.GivesXp) xpGivers++;
                    AddWorkTypeRequirements(requirements, giver.Requirements);
                }

                result[source.DefName] = new JobProfileWorkTypeFacts(
                    source.DefName,
                    source.Identity,
                    ReadOnly(SkillIdentities(source.RelevantSkills)),
                    ReadOnly(SkillNames(source.RelevantSkills)),
                    ReadOnly(members),
                    xpGivers,
                    ReadOnly(requirements));
            }
            return result;
        }

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
                        0, requirement.Top, 0, 0));
                else if (requirement.Top > target[existing].Top)
                {
                    JobProfileRequirementFacts first = target[existing];
                    target[existing] = new JobProfileRequirementFacts(
                        first.SkillIdentity, first.SkillDefName,
                        0, requirement.Top, 0, 0);
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

        private static List<T> Copy<T>(IEnumerable<T> source) =>
            source == null ? new List<T>() : new List<T>(source);

        private void EnsureMutable()
        {
            if (built) throw new InvalidOperationException("This profile index builder has already built its snapshot.");
        }
    }
}
