namespace WorkRoles.Core.Tests;

public class JobProfileIndexArchitectureTests
{
    [Test]
    public async Task CoreIndexIsPureAndRaceUsersArePreindexedOutsidePerGiverBuilds()
    {
        string source = CoreSource("JobProfileIndex.cs");
        string buildGiver = Method(source, "private JobProfileGiverFacts BuildGiver(");
        string addUser = Method(source, "public void AddRecipeUser(");

        foreach (string forbidden in new[]
                 {
                     "using RimWorld", "using Verse", ".Translate(", "TipModel",
                     "DefDatabase<", "Thread", "Task.Run",
                 })
            await Assert.That(source).DoesNotContain(forbidden);

        await Assert.That(addUser).Contains("humanlikeRecipeUsers.Add");
        await Assert.That(addUser).Contains("animalRecipeUsers.Add");
        await Assert.That(addUser).Contains("mechanoidRecipeUsers.Add");
        await Assert.That(buildGiver).Contains("benchIds.AddRange(humanlikeRecipeUsers)");
        await Assert.That(buildGiver).Contains("benchIds.AddRange(animalRecipeUsers)");
        await Assert.That(buildGiver).Contains("benchIds.AddRange(mechanoidRecipeUsers)");
        await Assert.That(buildGiver).DoesNotContain("recipeUsers.Count");
    }

    [Test]
    public async Task GameAdapterBuildsAllRawFactsInOneDefinitionPass()
    {
        string source = GameSource("JobSkillProfiles.cs");
        string build = Method(source, "private static DefinitionSnapshot BuildDefinitionFacts()");

        foreach (string defType in new[]
                 {
                     "WorkGiverDef", "WorkTypeDef", "ThingDef", "TerrainDef",
                     "RecipeDef", "SkillDef", "StatDef",
                 })
            await Assert.That(Occurrences(build,
                    $"DefDatabase<{defType}>.AllDefsListForReading"))
                .IsEqualTo(1)
                .Because($"{defType} definitions must be visited once per raw snapshot");

        await Assert.That(source).DoesNotContain("ThingDef.AllRecipes");
        await Assert.That(source).DoesNotContain(".AllRecipes");
        await Assert.That(Occurrences(build, "workGiversByPriority")).IsEqualTo(1);
        await Assert.That(build).Contains("builder.AddRecipeUser(");
        await Assert.That(build).Contains("builder.AddDatabaseRecipe(");
        await Assert.That(build).Contains("builder.AddConstructionRequirement(");
        await Assert.That(build).Contains("builder.AddSowingRequirement(");
        await Assert.That(build).Contains("fixedRecipeUsersSeen.Add(user)");
        await Assert.That(build).Contains("if (observedRecipeUsers.Contains(thing)) continue;");
        await Assert.That(build).Contains("JobProfileRecipeUserKind.None, directRecipes");
    }

    [Test]
    public async Task RawSnapshotAndMutableLocalizedFacadeHaveSeparateLifetimes()
    {
        string source = GameSource("JobSkillProfiles.cs");
        string raw = Method(source, "private static DefinitionSnapshot BuildDefinitionFacts()");
        string language = Method(source, "internal static void InvalidateLanguageCaches()");
        string definitions = Method(source, "internal static void InvalidateDefinitions()");
        string warmRaw = Method(source, "internal static void WarmDefinitionFacts()");
        string giver = Method(source, "public static GiverProfile ForGiver(string defName)");

        await Assert.That(source).Contains("private static DefinitionSnapshot definitionFacts;");
        await Assert.That(source).Contains("private static Dictionary<string, GiverProfile> byGiver;");
        await Assert.That(source).Contains("private static Dictionary<string, WorkTypeProfile> byType;");
        await Assert.That(giver).Contains("EnsureBuilt();");
        await Assert.That(language).Contains("byGiver = null;");
        await Assert.That(language).Contains("byType = null;");
        await Assert.That(language).DoesNotContain("definitionFacts = null;");
        await Assert.That(definitions).Contains("definitionFacts = null;");
        await Assert.That(definitions).Contains("InvalidateLanguageCaches();");
        await Assert.That(warmRaw).Contains("DefinitionFacts();");
        await Assert.That(warmRaw).DoesNotContain("EnsureBuilt");

        foreach (string forbidden in new[]
                 {
                     ".Translate(", "TipModel", "Patch_ActiveTip", "ToPlainText",
                 })
        {
            await Assert.That(raw).DoesNotContain(forbidden);
            await Assert.That(warmRaw).DoesNotContain(forbidden);
        }
    }

    [Test]
    public async Task DefinitionReloadPatchesReleaseBeforeMutationAndQueueLateSynchronousWarm()
    {
        string patches = GameSource("Patches", "Patch_DefGeneration.cs");
        string coordinator = GameSource("DefinitionReloadCoordinator.cs");
        string clear = Method(patches,
            "public static class Patch_PlayDataLoader_ClearAllPlayData");
        string hot = Method(patches,
            "public static class Patch_PlayDataLoader_HotReloadDefs");
        string defGeneration = Method(patches,
            "public static class Patch_DefGenerator_GenerateImpliedDefs_PostResolve");
        string queue = Method(coordinator, "internal static void QueueHotReloadWarm()");
        string release = Method(coordinator, "internal static void ReleaseBeforeReload(");
        string regenerated = Method(coordinator,
            "internal static void DefinitionsRegenerated(");
        string releaseOwners = Method(coordinator, "private static void ReleaseOwners()");

        await Assert.That(patches).Contains(
            "[HarmonyPatch(typeof(PlayDataLoader), nameof(PlayDataLoader.ClearAllPlayData))]");
        await Assert.That(clear).Contains("[HarmonyPrefix]");
        await Assert.That(clear).Contains("DefinitionReloadCoordinator.ReleaseBeforeReload();");
        await Assert.That(patches).Contains(
            "[HarmonyPatch(typeof(PlayDataLoader), nameof(PlayDataLoader.HotReloadDefs))]");
        await Assert.That(hot).Contains("[HarmonyPrefix]");
        await Assert.That(hot).Contains(
            "DefinitionReloadCoordinator.ReleaseBeforeReload();");
        await Assert.That(hot).Contains("[HarmonyPostfix]");
        await Assert.That(hot).Contains("[HarmonyPriority(Priority.Last)]");
        await Assert.That(hot).Contains("DefinitionReloadCoordinator.QueueHotReloadWarm();");
        await Assert.That(defGeneration).Contains(
            "DefinitionReloadCoordinator.DefinitionsRegenerated();");
        await Assert.That(queue).Contains("LongEventHandler.QueueLongEvent(");
        await Assert.That(queue).Contains("doAsynchronously: false");
        await Assert.That(queue).Contains("if (token != generation) return;");
        await Assert.That(queue).Contains("JobSkillProfiles.WarmDefinitionFacts();");
        await Assert.That(coordinator).DoesNotContain("Thread");
        await Assert.That(coordinator).DoesNotContain("Task.Run");
        await Assert.That(coordinator).Contains("private static int definitionRevision;");
        await Assert.That(coordinator)
            .Contains("internal static int Revision => definitionRevision;");
        await Assert.That(release).Contains("ReleaseOwners();");
        await Assert.That(Occurrences(release, "ReleaseOwners();")).IsEqualTo(1);
        await Assert.That(release).Contains("generation++");
        await Assert.That(release).Contains("queuedWarmGeneration = -1;");
        await Assert.That(regenerated).Contains("ReleaseOwners();");
        await Assert.That(regenerated).DoesNotContain("generation++");
        await Assert.That(regenerated).DoesNotContain("queuedWarmGeneration");
        await Assert.That(coordinator).DoesNotContain("hotReloadPending");
        int revision = releaseOwners.IndexOf("definitionRevision++;",
            StringComparison.Ordinal);
        int releaseProfile = releaseOwners.IndexOf(
            "JobSkillProfiles.InvalidateDefinitions();", StringComparison.Ordinal);
        int releaseCatalog = releaseOwners.IndexOf(
            "GameJobCatalog.Instance.InvalidateSessionCache();", StringComparison.Ordinal);
        int releaseCompiled = releaseOwners.IndexOf(
            "CompiledJobOrders.InvalidateDefinitions();", StringComparison.Ordinal);
        await Assert.That(revision).IsGreaterThan(-1);
        await Assert.That(releaseProfile).IsGreaterThan(revision);
        await Assert.That(releaseCatalog).IsGreaterThan(releaseProfile);
        await Assert.That(releaseCompiled).IsGreaterThan(releaseCatalog);

        await Assert.That(defGeneration).DoesNotContain(
            "JobSkillProfiles.InvalidateDefinitions();");
        await Assert.That(defGeneration).DoesNotContain(
            "GameJobCatalog.Instance.InvalidateSessionCache();");
        await Assert.That(defGeneration).DoesNotContain(
            "CompiledJobOrders.InvalidateDefinitions();");
    }

    [Test]
    public async Task StartupLanguageAndWorldLifecycleWarmOrClearTheCorrectLayer()
    {
        string finalize = Method(GameSource("WorkRolesGameComponent.cs"),
            "public override void FinalizeInit()");
        string language = Method(GameSource("LanguageChangeCoordinator.cs"),
            "internal static void Complete()");
        string teardown = Method(GameSource("Patches", "Patch_PawnWorkSettings.cs"),
            "public static class Patch_MemoryUtility_ClearAllMapsAndWorld");
        string profiles = GameSource("JobSkillProfiles.cs");
        string queueLocalized = Method(profiles,
            "internal static void QueueLocalizedFacadeWarm()");
        string cancel = Method(GameSource("DefinitionReloadCoordinator.cs"),
            "internal static void CancelPendingWarm()");

        int scrub = finalize.IndexOf("RoleCommands.ScrubDeadEntriesDirect(role)",
            StringComparison.Ordinal);
        int metadata = finalize.IndexOf("CompiledJobOrders.WarmProjectionMetadata();",
            StringComparison.Ordinal);
        int profilesWarm = finalize.IndexOf("JobSkillProfiles.WarmDefinitionFacts();",
            StringComparison.Ordinal);
        await Assert.That(metadata).IsGreaterThan(scrub);
        await Assert.That(profilesWarm).IsGreaterThan(metadata);

        int invalidate = language.IndexOf("JobSkillProfiles.InvalidateLanguageCaches();",
            StringComparison.Ordinal);
        int queue = language.IndexOf("JobSkillProfiles.QueueLocalizedFacadeWarm();",
            StringComparison.Ordinal);
        int registry = language.IndexOf("Patch_ActiveTip_TipRect.Clear();",
            StringComparison.Ordinal);
        await Assert.That(queue).IsGreaterThan(invalidate);
        await Assert.That(registry).IsGreaterThan(queue);
        await Assert.That(queueLocalized).Contains("LongEventHandler.ExecuteWhenFinished");
        await Assert.That(queueLocalized).Contains("profileGeneration");
        await Assert.That(queueLocalized).Contains("WarmLocalizedFacade();");
        await Assert.That(queueLocalized).DoesNotContain("GiverTip(");
        await Assert.That(queueLocalized).DoesNotContain("WorkTypeTip(");

        int releaseDefinitions = teardown.IndexOf(
            "DefinitionReloadCoordinator.ReleaseForTeardown();",
            StringComparison.Ordinal);
        int clearTips = teardown.IndexOf("Patch_ActiveTip_TipRect.Clear();",
            StringComparison.Ordinal);
        int cancelWarm = teardown.IndexOf("DefinitionReloadCoordinator.CancelPendingWarm();",
            StringComparison.Ordinal);
        await Assert.That(cancelWarm).IsGreaterThan(-1);
        await Assert.That(releaseDefinitions).IsGreaterThan(cancelWarm);
        await Assert.That(releaseDefinitions).IsGreaterThan(-1);
        await Assert.That(clearTips).IsGreaterThan(releaseDefinitions)
            .Because("cached profile tip strings must be discarded before the registry");
        await Assert.That(cancel).Contains("generation++");
        await Assert.That(cancel).Contains("queuedWarmGeneration = -1;");
    }

    private static int Occurrences(string source, string value)
    {
        int count = 0;
        for (int index = 0; (index = source.IndexOf(
                 value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            count++;
        return count;
    }

    private static string Method(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) return "";
        int open = source.IndexOf('{', start);
        if (open < 0) return "";
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(start, i - start + 1);
        }
        return "";
    }

    private static string GameSource(params string[] parts) =>
        Source(Path.Combine(new[] { "src", "WorkRoles" }.Concat(parts).ToArray()));

    private static string CoreSource(params string[] parts) =>
        Source(Path.Combine(new[] { "src", "WorkRoles.Core" }.Concat(parts).ToArray()));

    private static string Source(string relativePath)
    {
        string path = Path.Combine(RepoRoot(), relativePath);
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
