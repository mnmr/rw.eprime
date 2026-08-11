using System.Text.RegularExpressions;

namespace WorkRoles.Core.Tests;

public class SourceArchitectureTests
{
    [Test]
    public async Task RecommendationsHelpParagraphRestoresGuiStateInFinally()
    {
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RecommendationsTabView.cs"));
        Match method = Regex.Match(source,
            @"private static void DrawHelpParagraph\(Rect rect, string text\)(.*?)private static void DrawTuningSection",
            RegexOptions.Singleline);

        await Assert.That(method.Success).IsTrue();
        await Assert.That(method.Groups[1].Value).Contains("finally");
    }

    [Test]
    public async Task RuntimeTexturesHaveExplicitWorldTeardown()
    {
        string textures = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/WorkRolesTex.cs"));
        string worldTeardown = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/Patches/Patch_PawnWorkSettings.cs"));
        Match release = Regex.Match(textures,
            @"ReleaseForTeardown\(\)(.*?)^\s*}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        await Assert.That(release.Success).IsTrue();
        await Assert.That(release.Groups[1].Value).Contains("Destroy(Circle)");
        await Assert.That(release.Groups[1].Value).Contains("Destroy(ScrollEdgeFade)");
        await Assert.That(worldTeardown).Contains(
            "UI.WorkRolesTex.ReleaseForTeardown();");
    }

    [Test]
    public async Task CitedDrawMethodsDoNotMeasureTextDirectly()
    {
        string roles = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesTabView.cs"));
        string export = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_ExportPreview.cs"));
        string picker = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_RoleFilePicker.cs"));
        Match legend = Regex.Match(roles,
            @"private static float DrawLegendEntry(.*?)private void ApplyHourPaint",
            RegexOptions.Singleline);
        Match jobTree = Regex.Match(roles,
            @"private void DrawJobTree(.*?)// ----- Tri-state helpers -----",
            RegexOptions.Singleline);

        await Assert.That(legend.Success).IsTrue();
        await Assert.That(jobTree.Success).IsTrue();
        await Assert.That(legend.Groups[1].Value).DoesNotContain("Text.Calc");
        await Assert.That(jobTree.Groups[1].Value).DoesNotContain("Text.Calc");
        await Assert.That(export).DoesNotContain("Text.CalcSize");
        await Assert.That(picker).DoesNotContain("Text.CalcSize");
    }

    [Test]
    public async Task CitedWindowDrawMethodsUsePretranslatedLabels()
    {
        string export = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_ExportPreview.cs"));
        string picker = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_RoleFilePicker.cs"));
        string main = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/MainTabWindow_WorkRoles.cs"));
        Match exportDraw = Regex.Match(export,
            @"public override void DoWindowContents(.*?)private void EnsureTextCache",
            RegexOptions.Singleline);
        Match pickerDraw = Regex.Match(picker,
            @"protected void DrawLocationRows(.*)^\s*}\s*}\s*$",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Match mainDraw = Regex.Match(main,
            @"private void DrawContents(.*?)^\s*}\s*\r?\n\s*}\s*\r?\n\s*/// Unity",
            RegexOptions.Singleline | RegexOptions.Multiline);

        await Assert.That(exportDraw.Success).IsTrue();
        await Assert.That(pickerDraw.Success).IsTrue();
        await Assert.That(mainDraw.Success).IsTrue();
        await Assert.That(exportDraw.Groups[1].Value).DoesNotContain(".Translate(");
        await Assert.That(pickerDraw.Groups[1].Value).DoesNotContain(".Translate(");
        await Assert.That(mainDraw.Groups[1].Value).DoesNotContain(".Translate(");
    }

    [Test]
    public async Task MainLanguageCacheRebuildsAfterWindowTeardown()
    {
        string main = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/MainTabWindow_WorkRoles.cs"));
        Match observe = Regex.Match(main,
            @"private void ObserveLanguageRevision\(\)(.*?)public override void PreOpen",
            RegexOptions.Singleline);

        await Assert.That(observe.Success).IsTrue();
        await Assert.That(observe.Groups[1].Value).Contains("tabs != null");
    }

    [Test]
    public async Task AuthoritativeImportApplyIsOwnedByRoleCommands()
    {
        string roleIo = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/RoleIO.cs"));

        await Assert.That(roleIo).DoesNotContain(
            "public static string Apply(RoleStore store");
        await Assert.That(Regex.IsMatch(roleIo,
            @"public static partial class RoleCommands.*private static string ApplyImportToStore",
            RegexOptions.Singleline)).IsTrue();
    }

    [Test]
    public async Task PublishedRoleListRowsDoNotExposeLiveModelObjects()
    {
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesListState.cs"));
        Match published = Regex.Match(source,
            @"internal sealed class RoleListSnapshot(.*?)internal sealed class RoleSection",
            RegexOptions.Singleline);

        await Assert.That(published.Success).IsTrue();
        await Assert.That(published.Groups[1].Value).Contains(
            "RoleListRowSnapshot RowAt");
        await Assert.That(published.Groups[1].Value).DoesNotContain(
            "IReadOnlyList<RoleListRowSnapshot> Rows");
        await Assert.That(published.Groups[1].Value).DoesNotContain(
            "RoleSection section");
        await Assert.That(published.Groups[1].Value).DoesNotContain(
            "Role role");
        await Assert.That(published.Groups[1].Value).DoesNotContain(
            "RoleGroup group");
    }

    [Test]
    public async Task PublishedLocationSnapshotKeepsItsOwnedBufferPrivate()
    {
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/ColonyScope.cs"));

        await Assert.That(source).Contains(
            "class LocationSnapshot : IReadOnlyList<LocationInfo>");
        await Assert.That(source).DoesNotContain(
            "internal IReadOnlyList<LocationInfo> Locations");
    }

    [Test]
    public async Task CitedColonistRowDrawMethodsConsumePublishedRenderRows()
    {
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match cell = Regex.Match(source,
            @"internal void DrawColonistCell(.*?)/// Chip-strip height",
            RegexOptions.Singleline);
        Match chips = Regex.Match(source,
            @"internal void DrawChipStrip(.*?)private static string SuppressionReason",
            RegexOptions.Singleline);

        await Assert.That(cell.Success).IsTrue();
        await Assert.That(chips.Success).IsTrue();
        await Assert.That(cell.Groups[1].Value).Contains(
            "ColonistRowSnapshot row");
        await Assert.That(cell.Groups[1].Value).DoesNotContain("pawn.");
        await Assert.That(cell.Groups[1].Value).DoesNotContain("pawnSets");
        await Assert.That(chips.Groups[1].Value).Contains(
            "ColonistRowSnapshot row");
        await Assert.That(chips.Groups[1].Value).DoesNotContain("store.");
        await Assert.That(chips.Groups[1].Value).DoesNotContain("RoleById");
        await Assert.That(chips.Groups[1].Value).DoesNotContain("RulesPass(");
        await Assert.That(chips.Groups[1].Value).DoesNotContain("RoleTipText(");
        await Assert.That(chips.Groups[1].Value).DoesNotContain(".Translate(");
    }

    [Test]
    public async Task CitedRoleEditorDrawMethodsConsumePublishedRenderState()
    {
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesTabView.cs"));
        Match editor = Regex.Match(source,
            @"private void DrawEditor(.*?)/// The role's group",
            RegexOptions.Singleline);
        Match rules = Regex.Match(source,
            @"private void DrawRulesSection(.*?)private const float LegendSwatch",
            RegexOptions.Singleline);
        Match entries = Regex.Match(source,
            @"private void DrawEntries(.*?)// ----- Available Jobs",
            RegexOptions.Singleline);
        Match tree = Regex.Match(source,
            @"private void DrawJobTree(.*?)// ----- Tri-state helpers",
            RegexOptions.Singleline);

        foreach (Match method in new[] { editor, rules, entries, tree })
        {
            await Assert.That(method.Success).IsTrue();
            await Assert.That(method.Groups[1].Value).Contains(
                "RoleEditorSnapshot model");
            await Assert.That(method.Groups[1].Value).DoesNotContain("role.");
            await Assert.That(method.Groups[1].Value).DoesNotContain("store.");
            await Assert.That(method.Groups[1].Value).DoesNotContain(
                "RoleStore.Current");
            await Assert.That(method.Groups[1].Value).DoesNotContain(".Translate(");
        }
    }

    [Test]
    public async Task CitedCommandsGuardNoOpRevisionChanges()
    {
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/RoleCommands.cs"));

        await Assert.That(Method(source, "RestoreSelected")).Contains(
            "restored.Count == 0");
        await Assert.That(Method(source, "SetRoleGroup")).Contains(
            "bool changed");
        await Assert.That(Method(source, "RenameGroup")).Contains(
            "string.Equals");
        await Assert.That(Method(source, "RenameRole")).Contains(
            "string.Equals");
        await Assert.That(Method(source, "SetRoleAutoAssign")).Contains(
            "role.autoAssign == value");
        await Assert.That(Method(source, "MoveEntry")).Contains("from == to");
        await Assert.That(Method(source, "MoveRoleOnPawn")).Contains(
            "from == to");
        await Assert.That(Method(source, "SetCustomSwatch")).Contains(
            "ColorEqual");
    }

    [Test]
    public async Task RoleFileDialogsDeferExternalIoOutsideOnGui()
    {
        string picker = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_RoleFilePicker.cs"));
        string import = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_ImportSource.cs"));
        Match cachedPath = Regex.Match(picker,
            @"protected string CachedResolvedPath(.*?)private void RefreshPathOutsideOnGUI",
            RegexOptions.Singleline);
        Match importDraw = Regex.Match(import,
            @"public override void DoWindowContents(.*?)private void EnsureTextCache",
            RegexOptions.Singleline);

        await Assert.That(cachedPath.Success).IsTrue();
        await Assert.That(importDraw.Success).IsTrue();
        await Assert.That(cachedPath.Groups[1].Value).DoesNotContain(
            "File.Exists");
        await Assert.That(importDraw.Groups[1].Value).DoesNotContain(
            "File.ReadAllText");
        await Assert.That(importDraw.Groups[1].Value).DoesNotContain(
            "systemCopyBuffer");
        await Assert.That(importDraw.Groups[1].Value).DoesNotContain(
            "TryOpenPreview(");
    }

    [Test]
    public async Task SpecifiedCachesDocumentTheirRequiredContractInline()
    {
        var caches = new (string file, string anchor)[]
        {
            ("src/WorkRoles/ColonyScope.cs", "mapClassifications ="),
            ("src/WorkRoles/ColonyScope.cs", "locationSnapshots ="),
            ("src/WorkRoles/UI/ColonistsRosterState.cs", "private ScopeOption scope;"),
            ("src/WorkRoles/UI/ColonistsRosterState.cs", "private List<GroupSection<Pawn>> sections;"),
            ("src/WorkRoles/UI/ColonistStatsState.cs", "externalSnapshots ="),
            ("src/WorkRoles/UI/ColonistStatsState.cs", "rosterCellWidths ="),
            ("src/WorkRoles/UI/ColonistStatsState.cs", "presentations ="),
            ("src/WorkRoles/UI/ColonistStatsState.cs", "private int statsStamp"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private IReadOnlyList<GroupSection<Pawn>> tableLayoutSections;"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private ScopeCacheStamp sizeStamp"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private int paletteStamp"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private static Dictionary<int, string> abbrevCache;"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private static readonly Dictionary<SkillDef, string> skillHeaderLabels"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private readonly Dictionary<(int roleId, RoleTipContext context"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private string colonistHeaderCache;"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private readonly Dictionary<Pawn, ColonistRowSnapshot> chipLayouts"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private readonly Dictionary<(int roleId, Pawn pawn), bool> rulesPassCache"),
            ("src/WorkRoles/UI/RolesListState.cs", "private static readonly List<RoleSection>[] sectionsCache"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private int tipsStamp"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private List<RoleSkillPresentation> skillsUsed;"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private List<RoleHolderPresentation> holders;"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private HashSet<int> deadEntries;"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private readonly Dictionary<(JobEntryKind kind, string defName)"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private HashSet<string> uncoveredGivers;"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private List<RoleJobTreeNode> treeNodes;"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private int entrySetsStamp"),
            ("src/WorkRoles/UI/RolesTabView.cs", "private readonly MemoizedFactory<int, System.Action<int, int>>"),
            ("src/WorkRoles/UI/RolesTabView.cs", "private string jobFilterCachedFor"),
            ("src/WorkRoles/UI/Dialog_RoleFilePicker.cs", "private Location cachedLocation;"),
            ("src/WorkRoles/UI/Dialog_ExportPreview.cs", "private float measuredWidth"),
            ("src/WorkRoles/UI/Dialog_ImportSource.cs", "private string clip;"),
        };

        foreach ((string file, string anchor) in caches)
        {
            string source = File.ReadAllText(RepositoryFile(file));
            string contract = InlineContractBefore(source, anchor);
            await Assert.That(contract).Contains("Owner:");
            await Assert.That(contract).Contains("Key:");
            await Assert.That(contract).Contains("Value:");
            await Assert.That(contract).Contains("Dependencies:");
            await Assert.That(contract).Contains("Refresh:");
            await Assert.That(contract).Contains("Equality:");
            await Assert.That(contract).Contains("Teardown:");
        }
    }

    private static string Method(string source, string name)
    {
        Match match = Regex.Match(source,
            $@"public static void {name}\(.*?^\s*}}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        if (!match.Success) throw new InvalidOperationException(name);
        return match.Value;
    }

    private static string InlineContractBefore(string source, string anchor)
    {
        int anchorAt = source.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorAt < 0) throw new InvalidOperationException(anchor);
        int ownerAt = source.LastIndexOf("// Owner:", anchorAt,
            StringComparison.Ordinal);
        if (ownerAt < 0 || anchorAt - ownerAt > 1400)
            return "";
        return source.Substring(ownerAt, anchorAt - ownerAt);
    }

    private static string RepositoryFile(string relativePath)
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(relativePath);
    }
}
