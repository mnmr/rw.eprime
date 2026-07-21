namespace WorkRoles.Core.Tests;

public class GroupNameArchitectureTests
{
    [Test]
    public async Task CommandsRouteDefaultIdentityAndValidateUserGroupRenames()
    {
        string source = Source("RoleCommands.cs");
        string resolve = Section(source, "private static RoleGroup ResolveOrCreateGroup", "private static List<Role> MovingBlock");
        string rename = Section(source, "public static void RenameGroup", "public static void MoveGroupInList");

        await Assert.That(resolve).Contains("GroupNameRules.IsDefault(groupName)");
        await Assert.That(resolve).Contains("Store.EnsureDefaultGroup()");
        await Assert.That(resolve).Contains("GroupNameRules.IsAvailable(");
        await Assert.That(resolve).DoesNotContain("CatalogNameRules.IsAvailable(");
        await Assert.That(rename).Contains("GroupNameRules.IsAvailable(");
        await Assert.That(rename).DoesNotContain("CatalogNameRules.IsAvailable(");
    }

    [Test]
    public async Task ImportMapsDefaultDefinitionsAndReferencesWithoutCreatingAUserGroup()
    {
        string source = Source("RoleIO.cs");
        string materialization = Section(source,
            "IReadOnlyList<FileGroup> fileGroups", "if (rolesOverwrite)");
        string reference = Section(source, "private static int GroupIdFor", "private static void RecolorRoles");
        int idResolution = reference.IndexOf("RoleFile.ResolveGroup(", StringComparison.Ordinal);
        int defaultFallback = reference.IndexOf("GroupNameRules.IsDefault(name)", StringComparison.Ordinal);

        await Assert.That(materialization).Contains("ImportIdentityPlanner.Plan(groupImports, groupTargets)");
        await Assert.That(materialization).Contains("GroupNameRules.IsDefault(decision.DisplayLabel)");
        await Assert.That(materialization).Contains("store.EnsureDefaultGroup()");
        await Assert.That(reference).Contains("GroupNameRules.IsDefault(name)");
        await Assert.That(reference).Contains("store.EnsureDefaultGroup().id");
        await Assert.That(reference).Contains("GroupNameRules.IsAvailable(");
        await Assert.That(reference).DoesNotContain("StringComparison.Ordinal");
        await Assert.That(idResolution >= 0 && defaultFallback > idResolution).IsTrue();
    }

    [Test]
    public async Task StoreAndDialogUseTheReservedGroupNameSeam()
    {
        string store = Section(Source("RoleStore.cs"),
            "public RoleGroup EnsureDefaultGroup", "public void SyncSwatchNames");
        string dialog = Section(Source("UI", "Dialog_RenameRole.cs"),
            "private bool IsNameTaken", "private void EnsureValidation");

        await Assert.That(store).Contains("label = GroupNameRules.DefaultName");
        await Assert.That(dialog).Contains("GroupNameRules.IsAvailable(");
    }

    [Test]
    public async Task DependentImportCanReuseExistingRolesWithoutUpdatingMatchedRows()
    {
        string source = Source("RoleIO.cs");
        string matching = Section(source, "public static Role MatchRole", "public static List<PaletteRow> PaletteMergeRows");
        string apply = Section(source, "public static string Apply", "private static Role RuntimeRole");
        string paths = Section(apply, "if (pathsInclude)", "if (orderInclude");
        string order = Section(apply, "if (orderInclude", "return \"WR_ImportSummary\"");

        await Assert.That(matching).Contains("imported.templateDef");
        await Assert.That(matching).Contains("imported.label");
        await Assert.That(matching).DoesNotContain("imported.fileId");
        await Assert.That(paths).Contains("RoleFile.ResolvePathEntries(filePath, doc,");
        await Assert.That(paths).Contains("runtimeRoles.TryGetValue(fileRole, out var runtime)");
        await Assert.That(paths).Contains("RuntimeRole(doc, runtimeRoles,");
        await Assert.That(order).Contains("RuntimeRole(");
        await Assert.That(order).Contains("RuntimeRole(doc, runtimeRoles, reference)");
    }

    [Test]
    public async Task ImportUsesOccurrenceAwarePlansForRolesGroupsAndOverwrite()
    {
        string source = Source("RoleIO.cs");
        string rows = Section(source, "public static List<RoleRow> RoleRows", "/// Roles deleted by an overwrite");
        string apply = Section(source, "public static string Apply", "private static Role RuntimeRole");

        await Assert.That(rows).Contains("ImportIdentityPlanner.Plan(imports, existing,");
        await Assert.That(rows).Contains("decision.ExistingIndex");
        await Assert.That(rows).Contains("displayLabel = decision.DisplayLabel");
        await Assert.That(apply).Contains("RoleFile.GroupsWithStableIds(doc)");
        await Assert.That(apply).Contains("ImportIdentityPlanner.Plan(groupImports, groupTargets)");
        await Assert.That(apply).Contains("OverwriteDeletes(store, plannedRoles)");
        await Assert.That(apply).Contains("RoleRows(store, doc, rolesOverwrite)");
        await Assert.That(apply).Contains("!row.displayLabel.NullOrEmpty()");
        await Assert.That(apply).Contains("row.existing.label = null;");
        await Assert.That(apply).Contains("row.preservesExistingLabel");
        await Assert.That(apply).Contains("unchangedLegacyDuplicate");
        await Assert.That(apply).Contains("RoleFile.AnchorWithStableId(filePath)");
        await Assert.That(apply).Contains("RoleFile.RecommendationOrderWithStableIds(doc)");
    }

    [Test]
    public async Task RoleCommandsKeepAuthoritativeNameGuards()
    {
        string source = Source("RoleCommands.cs");
        string create = Section(source, "public static void CreateRole", "internal static Role CreateRoleFromDef");
        string seed = Section(source, "internal static Role CreateRoleFromDef", "internal static RoleGroup EnsureGroup");
        string rename = Section(source, "public static void RenameRole", "public static void DuplicateRole");
        string duplicate = Section(source, "public static void DuplicateRole", "public static void MoveRoleInCatalog");

        await Assert.That(create).Contains("CatalogNameRules.IsAvailable(");
        await Assert.That(seed).Contains("CatalogNameRules.Unique(");
        await Assert.That(rename).Contains("CatalogNameRules.IsAvailable(");
        await Assert.That(duplicate).Contains("CatalogNameRules.IsAvailable(");
    }

    [Test]
    public async Task CoverageReportsTheCreatedRolesPossiblySuffixedLabel()
    {
        string source = Source("Seeding.cs");
        string coverage = Section(source, "public static List<string> EnsureWorkTypeCoverage", "private static HashSet<string> CoveredWorkTypes");

        await Assert.That(coverage).Contains("result.Add(role.label);");
        await Assert.That(coverage).DoesNotContain("result.Add(label);");
    }

    private static string Section(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = start < 0 ? -1 : source.IndexOf(endMarker, start, StringComparison.Ordinal);
        return start < 0 || end < 0 ? "" : source.Substring(start, end - start);
    }

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "WorkRoles" }.Concat(parts).ToArray()));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
