namespace WorkRoles.Core.Tests;

public class VanillaProjectionMetadataArchitectureTests
{
    [Test]
    public async Task PawnBuildUsesCachedMetadataWithoutRebuildingProjectionPolicy()
    {
        string method = Method(GameSource("CompiledJobOrders.cs"),
            "private static Entry Build(Pawn pawn)");

        await Assert.That(method).Contains(
            "JobOrderCompiler.ToVanillaPriorities(compiled.WorkTypePriorities,");
        await Assert.That(method).Contains("ProjectionMetadata())");
        await Assert.That(method).DoesNotContain("BuildProjectionCategories");
        await Assert.That(method).DoesNotContain("WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder");
        await Assert.That(method).DoesNotContain("new VanillaProjectionCategories");
        await Assert.That(method).DoesNotContain("name => columns");
        await Assert.That(method).DoesNotContain("Func<string, bool> pawnCanDo");
        await Assert.That(method).Contains("PawnCanDoJob");
        await Assert.That(method).DoesNotContain(".Select(");
        await Assert.That(method).DoesNotContain(".ToDictionary(");
    }

    [Test]
    public async Task VanillaFallbackUsesCachedDefinitionListInsteadOfScanningDefsPerPawn()
    {
        string method = Method(GameSource("CompiledJobOrders.cs"),
            "private static void SyncVanillaFallback(Pawn pawn, Entry entry)");

        await Assert.That(method).Contains("ProjectionDefinitions().AllWorkTypes");
        await Assert.That(method).DoesNotContain(
            "DefDatabase<WorkTypeDef>.AllDefsListForReading");
    }

    [Test]
    public async Task DefinitionAndBasicsMetadataHaveSeparateLazyCacheLifetimes()
    {
        string source = GameSource("CompiledJobOrders.cs");
        string definitions = Method(source,
            "private static ProjectionDefinitionCache ProjectionDefinitions()");
        string metadata = Method(source,
            "private static VanillaProjectionMetadata ProjectionMetadata()");

        await Assert.That(definitions).Contains(
            "DefDatabase<WorkTypeDef>.AllDefsListForReading");
        await Assert.That(definitions).Contains(
            "WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder");
        await Assert.That(definitions).Contains(
            "new VanillaProjectionDefinitionMetadata(sources, priorityOrder)");
        await Assert.That(metadata).Contains(
            "projectionMetadataBasicsRevision == basicsRevision");
        await Assert.That(metadata).Contains("definitions.Metadata.WithBasics(");
        await Assert.That(metadata).Contains("BasicsWorkTypes(");
    }

    [Test]
    public async Task MetadataCompilerOverloadReusesMetadataOwnedPolicyObjects()
    {
        string method = Method(CoreSource("JobOrderCompiler.cs"),
            "VanillaProjectionMetadata metadata)");

        await Assert.That(method).Contains("metadata.ColumnResolver");
        await Assert.That(method).Contains("metadata.ProjectionCategories");
        await Assert.That(method).DoesNotContain("new VanillaProjectionCategories");
        await Assert.That(method).DoesNotContain("new Func");
    }

    [Test]
    public async Task MetadataColumnLookupUsesOwnedDictionaryNotReadOnlyWrapper()
    {
        string method = Method(CoreSource("VanillaProjectionMetadata.cs"),
            "public int ColumnOf(string workTypeName)");

        await Assert.That(method).Contains("columns.TryGetValue");
        await Assert.That(method).DoesNotContain("Columns.TryGetValue");
    }

    [Test]
    public async Task ProjectionImplementationAvoidsPerPawnLinqPolicyDelegates()
    {
        string source = CoreSource("JobOrderCompiler.cs");
        string projection = Method(source, "Func<string, int> columnOf,");
        string pass = Method(source,
            "private static Dictionary<string, int> Project(");

        await Assert.That(projection).DoesNotContain(".Where(");
        await Assert.That(projection).DoesNotContain(".Any(");
        await Assert.That(projection).DoesNotContain(".Max(");
        await Assert.That(projection).DoesNotContain(".ToList(");
        await Assert.That(pass).DoesNotContain(".OrderBy(");
    }

    [Test]
    public async Task BasicsInvalidationClearsAllWhileNormalRolePathStaysNarrow()
    {
        string source = GameSource("CompiledJobOrders.cs");
        string invalidateRole = Method(source,
            "internal static void InvalidateRole(int roleId, Action invalidateUi)");
        string invalidateBasics = Method(source,
            "private static void InvalidateBasics(Role role, Action invalidateUi)");

        await Assert.That(invalidateRole).Contains("if (IsBasicsRole(roleId, role))");
        await Assert.That(invalidateRole).Contains("InvalidateBasics(role, invalidateUi);");
        await Assert.That(invalidateRole).Contains("store.PawnsWithRole(roleId)");
        await Assert.That(invalidateBasics).Contains("InvalidateProjectionMetadata();");
        await Assert.That(invalidateBasics).Contains("cache.Clear();");
        await Assert.That(invalidateBasics).Contains("role?.InvalidateCoverage();");
        await Assert.That(Occurrences(invalidateBasics, "invalidateUi();")).IsEqualTo(1);
    }

    [Test]
    public async Task BroadInvalidationResetsBasicsMetadataButHourlyDoesNot()
    {
        string source = GameSource("CompiledJobOrders.cs");
        string broad = Method(source, "public static void InvalidateAll()");
        string hourly = Method(source, "public static void InvalidateAllTimeRuled()");
        string pawn = Method(source, "public static void Invalidate(Pawn pawn)");

        await Assert.That(broad).Contains("InvalidateProjectionMetadata();");
        await Assert.That(hourly).DoesNotContain("InvalidateProjectionMetadata");
        await Assert.That(pawn).DoesNotContain("InvalidateProjectionMetadata");
    }

    [Test]
    public async Task CreatingBasicsTemplateUsesCentralRoleInvalidation()
    {
        string method = Method(GameSource("RoleCommands.cs"),
            "internal static Role CreateRoleFromDef(RoleDef def)");

        await Assert.That(method).Contains("role.templateDefName == \"WS_Basics\"");
        await Assert.That(method).Contains("CompiledJobOrders.InvalidateRole(role.id);");
    }

    [Test]
    public async Task DefinitionLifecyclePatchIsExactLatePostfixAndResetsOwnersInOrder()
    {
        string source = GameSource("Patches", "Patch_DefGeneration.cs");

        await Assert.That(source).Contains(
            "[HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PostResolve), typeof(bool))]");
        await Assert.That(source).Contains("[HarmonyPostfix]");
        await Assert.That(source).Contains("[HarmonyPriority(Priority.Last)]");
        await Assert.That(source).Contains("public static void Postfix(bool hotReload)");
        await Assert.That(source).Contains(
            "DefinitionReloadCoordinator.DefinitionsRegenerated();");
        await Assert.That(source).DoesNotContain(
            "GameJobCatalog.Instance.InvalidateSessionCache();");
        await Assert.That(source).DoesNotContain(
            "CompiledJobOrders.InvalidateDefinitions();");
    }

    [Test]
    public async Task DefinitionResetDropsDefinitionSnapshotThenAllCompiledAndCoverageState()
    {
        string method = Method(GameSource("CompiledJobOrders.cs"),
            "internal static void InvalidateDefinitions()");

        await Assert.That(method).Contains("projectionDefinitions = null;");
        await Assert.That(method).Contains("InvalidateAll();");
        await Assert.That(method.IndexOf("projectionDefinitions = null;", StringComparison.Ordinal))
            .IsLessThan(method.IndexOf("InvalidateAll();", StringComparison.Ordinal));
    }

    [Test]
    public async Task WorldTeardownDropsDefinitionSnapshotInsteadOfRetainingDefInstances()
    {
        string method = Method(GameSource("Patches", "Patch_PawnWorkSettings.cs"),
            "public static class Patch_MemoryUtility_ClearAllMapsAndWorld");

        await Assert.That(method).Contains(
            "DefinitionReloadCoordinator.ReleaseForTeardown();");
        await Assert.That(method).DoesNotContain("CompiledJobOrders.InvalidateDefinitions();");
        await Assert.That(method).DoesNotContain("CompiledJobOrders.InvalidateAll();");
    }

    [Test]
    public async Task FinalizeInitWarmsMetadataOnlyAfterSeedingCoverageAndScrubbing()
    {
        string method = Method(GameSource("WorkRolesGameComponent.cs"),
            "public override void FinalizeInit()");

        int seed = method.IndexOf("Seeding.SeedIfNeeded();", StringComparison.Ordinal);
        int coverage = method.IndexOf("Seeding.EnsureWorkTypeCoverage();", StringComparison.Ordinal);
        int scrub = method.IndexOf("RoleCommands.ScrubDeadEntriesDirect(role)", StringComparison.Ordinal);
        int warm = method.IndexOf("CompiledJobOrders.WarmProjectionMetadata();", StringComparison.Ordinal);
        await Assert.That(seed).IsGreaterThan(-1);
        await Assert.That(coverage).IsGreaterThan(seed);
        await Assert.That(scrub).IsGreaterThan(coverage);
        await Assert.That(warm).IsGreaterThan(scrub);
    }

    [Test]
    public async Task ExistingImportAndSeedingBroadPathsFlowThroughMetadataReset()
    {
        await Assert.That(GameSource("RoleIO.cs")).Contains(
            "CompiledJobOrders.InvalidateAll();");
        await Assert.That(GameSource("Seeding.cs")).Contains(
            "CompiledJobOrders.InvalidateAll();");
        await Assert.That(GameSource("RoleStore.cs")).Contains(
            "CompiledJobOrders.InvalidateAll();");
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
