using System.Text.RegularExpressions;

namespace WorkRoles.Core.Tests.Infrastructure;

public class SourceArchitectureTests
{
    [Test]
    public async Task RecommendationsHelpParagraphRestoresGuiStateInFinally()
    {
        string source = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/RecommendationsTabView.cs"));
        Match method = Regex.Match(source, @"private static void DrawHelpParagraph\(Rect rect, string text\)(.*?)private static void DrawTuningSection", RegexOptions.Singleline);

        await Assert.That(method.Success).IsTrue();
        await Assert.That(method.Groups[1].Value).Contains("finally");
    }

    [Test]
    public async Task RuntimeTexturesHaveExplicitWorldTeardown()
    {
        string textures = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/WorkRolesTex.cs"));
        string worldTeardown = File.ReadAllText(RepositoryFile("src/WorkRoles/Patches/Patch_PawnWorkSettings.cs"));
        Match release = Regex.Match(textures, @"ReleaseForTeardown\(\)(.*?)^\s*}", RegexOptions.Singleline | RegexOptions.Multiline);

        await Assert.That(release.Success).IsTrue();
        await Assert.That(release.Groups[1].Value).Contains("Destroy(Circle)");
        await Assert.That(release.Groups[1].Value).Contains("Destroy(ScrollEdgeFade)");
        await Assert.That(worldTeardown).Contains("UI.WorkRolesTex.ReleaseForTeardown();");
    }

    [Test]
    public async Task CitedDrawMethodsDoNotMeasureTextDirectly()
    {
        string roles = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/RolesTabView.cs"));
        string export = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/Dialog_ExportPreview.cs"));
        string picker = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/Dialog_RoleFilePicker.cs"));
        Match legend = Regex.Match(roles, @"private static float DrawLegendEntry(.*?)private void ApplyHourPaint", RegexOptions.Singleline);
        Match jobTree = Regex.Match(roles, @"private void DrawJobTree(.*?)// ----- Tri-state helpers -----", RegexOptions.Singleline);

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
        string export = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/Dialog_ExportPreview.cs"));
        string picker = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/Dialog_RoleFilePicker.cs"));
        string main = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/MainTabWindow_WorkRoles.cs"));
        Match exportDraw = Regex.Match(export, @"public override void DoWindowContents(.*?)private void EnsureTextCache", RegexOptions.Singleline);
        Match pickerDraw = Regex.Match(picker, @"protected void DrawLocationRows(.*)^\s*}\s*}\s*$", RegexOptions.Singleline | RegexOptions.Multiline);
        Match mainDraw = Regex.Match(main, @"private void DrawContents(.*?)^\s*}\s*\r?\n\s*}\s*\r?\n\s*/// Unity", RegexOptions.Singleline | RegexOptions.Multiline);

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
        string main = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/MainTabWindow_WorkRoles.cs"));
        Match observe = Regex.Match(main, @"private void ObserveLanguageRevision\(\)(.*?)public override void PreOpen", RegexOptions.Singleline);

        await Assert.That(observe.Success).IsTrue();
        await Assert.That(observe.Groups[1].Value).Contains("tabs != null");
    }

    [Test]
    public async Task AuthoritativeImportApplyIsOwnedByRoleCommands()
    {
        string roleIo = File.ReadAllText(RepositoryFile("src/WorkRoles/RoleIO.cs"));

        await Assert.That(roleIo).DoesNotContain("public static string Apply(RoleStore store");
        await Assert.That(Regex.IsMatch(roleIo, @"public static partial class RoleCommands.*private static string ApplyImportToStore", RegexOptions.Singleline)).IsTrue();
    }

    [Test]
    public async Task PublishedRoleListRowsDoNotExposeLiveModelObjects()
    {
        string source = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/RolesListState.cs"));
        Match published = Regex.Match(source, @"internal sealed class RoleListSnapshot(.*?)internal sealed class RoleSection", RegexOptions.Singleline);

        await Assert.That(published.Success).IsTrue();
        await Assert.That(published.Groups[1].Value).Contains("RoleListRowSnapshot RowAt");
        await Assert.That(published.Groups[1].Value).DoesNotContain("IReadOnlyList<RoleListRowSnapshot> Rows");
        await Assert.That(published.Groups[1].Value).DoesNotContain("RoleSection section");
        await Assert.That(published.Groups[1].Value).DoesNotContain("Role role");
        await Assert.That(published.Groups[1].Value).DoesNotContain("RoleGroup group");
    }

    [Test]
    public async Task PublishedLocationSnapshotKeepsItsOwnedBufferPrivate()
    {
        string source = File.ReadAllText(RepositoryFile("src/WorkRoles/ColonyScope.cs"));

        await Assert.That(source).Contains("class LocationSnapshot : IReadOnlyList<LocationInfo>");
        await Assert.That(source).Contains("private readonly List<LocationInfo> locations;");
        await Assert.That(source).DoesNotContain("void Publish(List<LocationInfo>");
        await Assert.That(source).DoesNotContain("internal IReadOnlyList<LocationInfo> Locations");
    }

    [Test]
    public async Task RecommendationsRenderFromDetachedOneShotSnapshots()
    {
        string state = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RecommendationsTabState.cs"));
        string view = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RecommendationsTabView.cs"));
        Match owner = Regex.Match(state,
            @"internal sealed class RecommendationsTabState(.*?)internal readonly struct RecRoleMenuOption",
            RegexOptions.Singleline);

        await Assert.That(owner.Success).IsTrue();
        await Assert.That(owner.Groups[1].Value).DoesNotContain(".Clear()");
        await Assert.That(state).DoesNotContain("IReadOnlyList<Role>");
        await Assert.That(state).DoesNotContain("internal Role Role");
        await Assert.That(view).DoesNotContain("RoleById(");
        await Assert.That(view).DoesNotContain("store.roles");

        Match ensureOrder = Regex.Match(state,
            @"internal void EnsureOrder\(RoleStore store, float width\)(.*?)internal void EnsureTuning",
            RegexOptions.Singleline);
        await Assert.That(ensureOrder.Success).IsTrue();
        await Assert.That(ensureOrder.Groups[1].Value)
            .Contains("orderSnapshot.ContentEquals(rebuilt)");
    }

    [Test]
    public async Task CitedColonistRowDrawMethodsConsumePublishedRenderRows()
    {
        string source = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/ColonistsTabView.cs"));
        Match cell = Regex.Match(source, @"internal void DrawColonistCell(.*?)/// Chip-strip height", RegexOptions.Singleline);
        Match chips = Regex.Match(source, @"internal void DrawChipStrip(.*?)private static string SuppressionReason", RegexOptions.Singleline);

        await Assert.That(cell.Success).IsTrue();
        await Assert.That(chips.Success).IsTrue();
        await Assert.That(cell.Groups[1].Value).Contains("ColonistRowSnapshot row");
        await Assert.That(cell.Groups[1].Value).DoesNotContain("pawn.");
        await Assert.That(cell.Groups[1].Value).DoesNotContain("pawnSets");
        await Assert.That(chips.Groups[1].Value).Contains("ColonistRowSnapshot row");
        await Assert.That(chips.Groups[1].Value).DoesNotContain("store.");
        await Assert.That(chips.Groups[1].Value).DoesNotContain("RoleById");
        await Assert.That(chips.Groups[1].Value).DoesNotContain("RulesPass(");
        await Assert.That(chips.Groups[1].Value).DoesNotContain("RoleTipText(");
        await Assert.That(chips.Groups[1].Value).DoesNotContain(".Translate(");
    }

    [Test]
    public async Task ColonistChipStripConsumesPublishedDisplayMode()
    {
        // RimWorld/Unity rendering cannot execute in the Core test assembly.
        // This guard keeps the draw path on the mode captured by the same key
        // that owns the published row, without adding a production seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match published = Regex.Match(source,
            @"internal sealed class ColonistRowSnapshot(.*?)// Owner: Colonists window\. Key: RoleStore",
            RegexOptions.Singleline);
        Match producer = Regex.Match(source,
            @"private ColonistRowSnapshot RowSnapshotFor\(.*?private ColonistSkillCellSnapshot\[\] BuildSkillCells",
            RegexOptions.Singleline);
        Match draw = Regex.Match(source,
            @"internal void DrawChipStrip\(.*?private static string SuppressionReason",
            RegexOptions.Singleline);

        await Assert.That(published.Success).IsTrue();
        await Assert.That(producer.Success).IsTrue();
        await Assert.That(draw.Success).IsTrue();
        await Assert.That(published.Groups[1].Value)
            .Contains("internal ChipDisplay ChipDisplay");
        await Assert.That(published.Groups[1].Value)
            .Contains("ChipDisplay = chipDisplay");
        await Assert.That(producer.Groups[0].Value)
            .Contains("ChipDisplay chipDisplay = (ChipDisplay)(display >> 1)");
        await Assert.That(draw.Groups[0].Value)
            .Contains("display: row.ChipDisplay");
        await Assert.That(draw.Groups[0].Value)
            .DoesNotContain("display: TableChips");
    }

    [Test]
    public async Task ColonistNameCellRestoresCallerGuiState()
    {
        // The RimWorld/Unity drawing assembly is outside the Core executable
        // test boundary. This guard protects exact, exception-safe restoration
        // without adding a production seam solely for GUI-state testing.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match cell = Regex.Match(source,
            @"internal void DrawColonistCell\(.*?/// Chip-strip height",
            RegexOptions.Singleline);

        await Assert.That(cell.Success).IsTrue();
        await Assert.That(cell.Groups[0].Value)
            .Contains("GameFont oldFont = Text.Font");
        await Assert.That(cell.Groups[0].Value)
            .Contains("TextAnchor oldAnchor = Text.Anchor");
        await Assert.That(cell.Groups[0].Value)
            .Contains("Color oldColor = GUI.color");
        await Assert.That(cell.Groups[0].Value).Contains("finally");
        await Assert.That(cell.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(cell.Groups[0].Value)
            .Contains("Text.Anchor = oldAnchor");
        await Assert.That(cell.Groups[0].Value)
            .Contains("GUI.color = oldColor");
        await Assert.That(cell.Groups[0].Value)
            .DoesNotContain("GUI.color = Color.white");
        await Assert.That(cell.Groups[0].Value)
            .DoesNotContain("Text.Anchor = TextAnchor.UpperLeft");
    }

    [Test]
    public async Task ColonistSkillCellRestoresCallerGuiState()
    {
        // The RimWorld/Unity drawing assembly is outside the Core executable
        // test boundary. This guard covers both the normal path and the
        // disabled-cell early return without adding a production seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match cell = Regex.Match(source,
            @"internal void DrawSkillCell\(.*?// Owner: Colonists window\. Key: \(role id",
            RegexOptions.Singleline);

        await Assert.That(cell.Success).IsTrue();
        await Assert.That(cell.Groups[0].Value)
            .Contains("GameFont oldFont = Text.Font");
        await Assert.That(cell.Groups[0].Value)
            .Contains("TextAnchor oldAnchor = Text.Anchor");
        await Assert.That(cell.Groups[0].Value)
            .Contains("Color oldColor = GUI.color");
        await Assert.That(cell.Groups[0].Value).Contains("finally");
        await Assert.That(cell.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(cell.Groups[0].Value)
            .Contains("Text.Anchor = oldAnchor");
        await Assert.That(cell.Groups[0].Value)
            .Contains("GUI.color = oldColor");
        await Assert.That(cell.Groups[0].Value)
            .DoesNotContain("GUI.color = Color.white");
        await Assert.That(cell.Groups[0].Value)
            .DoesNotContain("Text.Anchor = TextAnchor.UpperLeft");
    }

    [Test]
    public async Task ColonistSkillCellsConsumePublishedRowProjection()
    {
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        string roster = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsRosterState.cs"));
        Match row = Regex.Match(source,
            @"private void DrawRow\(.*?// ----- Keyboard navigation -----",
            RegexOptions.Singleline);
        Match cell = Regex.Match(source,
            @"internal void DrawSkillCell\(.*?// Owner: Colonists window\. Key: \(role id",
            RegexOptions.Singleline);
        Match cache = Regex.Match(source,
            @"private ColonistRowSnapshot RowSnapshotFor\(.*?private ColonistSkillCellSnapshot\[\] BuildSkillCells",
            RegexOptions.Singleline);
        Match publishColumns = Regex.Match(roster,
            @"private void PublishSkillColumns\(.*?private void SaveSkillColumns",
            RegexOptions.Singleline);

        await Assert.That(row.Success).IsTrue();
        await Assert.That(cell.Success).IsTrue();
        await Assert.That(cache.Success).IsTrue();
        await Assert.That(publishColumns.Success).IsTrue();
        await Assert.That(row.Groups[0].Value)
            .Contains("publishedRow.SkillAt(columnIndex)");
        await Assert.That(row.Groups[0].Value)
            .DoesNotContain("rosterState.SkillColumns");
        await Assert.That(row.Groups[0].Value)
            .DoesNotContain("SkillColumnWidth(");
        await Assert.That(cell.Groups[0].Value)
            .Contains("ColonistSkillCellSnapshot skill");
        await Assert.That(cell.Groups[0].Value)
            .DoesNotContain("statsState.");
        await Assert.That(cell.Groups[0].Value)
            .DoesNotContain("SkillLineSnapshot(");
        await Assert.That(cell.Groups[0].Value)
            .DoesNotContain("PresentationFor(");
        await Assert.That(cell.Groups[0].Value).DoesNotContain("foreach");
        await Assert.That(cache.Groups[0].Value)
            .Contains("skillColumnsRevision = rosterState.SkillColumnsRevision");
        await Assert.That(cache.Groups[0].Value)
            .Contains("chipLayoutSkillColumnsRevision != skillColumnsRevision");
        await Assert.That(cache.Groups[0].Value)
            .Contains("chipLayoutSkillColumnsRevision = skillColumnsRevision");
        await Assert.That(publishColumns.Groups[0].Value)
            .Contains("skillColumns.ContentEquals(rebuilt)");
        int noOpReturn = publishColumns.Groups[0].Value.IndexOf("return;",
            StringComparison.Ordinal);
        int revisionAdvance = publishColumns.Groups[0].Value.IndexOf(
            "skillColumnsRevision++;", StringComparison.Ordinal);
        await Assert.That(noOpReturn).IsGreaterThanOrEqualTo(0);
        await Assert.That(revisionAdvance).IsGreaterThan(noOpReturn);
    }

    [Test]
    public async Task ColonistRowHoverUsesPublishedTargetWithoutWrapperAllocation()
    {
        // The game-only drawing API has no executable boundary in Core tests.
        // This focused guard prevents the steady hover path from rebuilding
        // LookTargets and its mutable target list around published row data.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match row = Regex.Match(source,
            @"private void DrawRow\(.*?// ----- Keyboard navigation -----",
            RegexOptions.Singleline);

        await Assert.That(row.Success).IsTrue();
        await Assert.That(row.Groups[0].Value)
            .Contains("TargetHighlighter.Highlight(publishedRow.Pawn");
        await Assert.That(row.Groups[0].Value)
            .DoesNotContain("new LookTargets");
    }

    [Test]
    public async Task ColonistRowRestoresCallerGuiColor()
    {
        // RimWorld/Unity GUI state is unavailable at the Core executable
        // boundary. This guard protects the row method's exact, exception-safe
        // restoration without introducing a game-assembly production seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match row = Regex.Match(source,
            @"private void DrawRow\(.*?// ----- Keyboard navigation -----",
            RegexOptions.Singleline);

        await Assert.That(row.Success).IsTrue();
        await Assert.That(row.Groups[0].Value)
            .Contains("Color oldColor = GUI.color");
        await Assert.That(row.Groups[0].Value).Contains("finally");
        await Assert.That(row.Groups[0].Value)
            .Contains("GUI.color = oldColor");
        await Assert.That(row.Groups[0].Value)
            .DoesNotContain("GUI.color = Color.white");
    }

    [Test]
    public async Task ColonistGroupHeadersConsumePublishedCollapseState()
    {
        // RimWorld/Unity rendering has no executable boundary in Core tests.
        // This focused guard protects the cache invariant without introducing
        // a production seam solely to instantiate the game UI.
        string view = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match rowType = Regex.Match(view,
            @"private readonly struct TableLayoutRow(.*?)// Owner: Colonists window\. Key: section snapshot identity",
            RegexOptions.Singleline);
        Match producer = Regex.Match(view,
            @"private void EnsureTableLayout\((.*?)private readonly struct ColonistTableHeaderColumnSnapshot",
            RegexOptions.Singleline);
        Match tableDraw = Regex.Match(view,
            @"private void DrawPawnTable\((.*?)private static void DrawScrollEdgeFades",
            RegexOptions.Singleline);
        Match groupDraw = Regex.Match(view,
            @"private void DrawGroupHeader\((.*?)private void DrawRow",
            RegexOptions.Singleline);

        await Assert.That(rowType.Success).IsTrue();
        await Assert.That(producer.Success).IsTrue();
        await Assert.That(tableDraw.Success).IsTrue();
        await Assert.That(groupDraw.Success).IsTrue();
        await Assert.That(rowType.Groups[1].Value)
            .Contains("internal bool Collapsed");
        await Assert.That(producer.Groups[1].Value)
            .Contains("bool collapsed = rosterState.IsCollapsed(section.Key)");
        await Assert.That(producer.Groups[1].Value)
            .Contains("new TableLayoutRow(section, null,");
        await Assert.That(producer.Groups[1].Value)
            .Contains("if (collapsed) continue;");
        await Assert.That(tableDraw.Groups[1].Value)
            .Contains("DrawGroupHeader(rowRect, row)");
        await Assert.That(groupDraw.Groups[1].Value)
            .Contains("row.Collapsed");
        await Assert.That(groupDraw.Groups[1].Value)
            .DoesNotContain("rosterState.IsCollapsed");
        await Assert.That(groupDraw.Groups[1].Value)
            .Contains("TextAnchor oldAnchor = Text.Anchor");
        await Assert.That(groupDraw.Groups[1].Value)
            .Contains("finally");
        await Assert.That(groupDraw.Groups[1].Value)
            .Contains("Text.Anchor = oldAnchor");
    }

    [Test]
    public async Task ColonistRosterChromeConsumesDetachedPublishedSnapshots()
    {
        string view = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        string state = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsRosterState.cs"));
        string catalog = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsRosterCatalog.cs"));

        Match drawPalette = Regex.Match(view,
            @"private void DrawPalette(.*?)// ----- Filter row -----",
            RegexOptions.Singleline);
        Match drawFilters = Regex.Match(view,
            @"private void DrawFilterRow(.*?)private ChipDisplay TableChips",
            RegexOptions.Singleline);
        Match ensureSizes = Regex.Match(view,
            @"private void EnsureSizes\(\)(.*?)public float DesiredWidth",
            RegexOptions.Singleline);
        Match publishedCatalog = Regex.Match(catalog,
            @"internal sealed class ColonistsRosterCatalogSnapshot(.*?)internal static ColonistsRosterCatalogSnapshot Build",
            RegexOptions.Singleline);

        await Assert.That(drawPalette.Success).IsTrue();
        await Assert.That(drawFilters.Success).IsTrue();
        await Assert.That(ensureSizes.Success).IsTrue();
        await Assert.That(publishedCatalog.Success).IsTrue();
        await Assert.That(drawPalette.Groups[1].Value).DoesNotContain("RoleById");
        await Assert.That(drawPalette.Groups[1].Value).DoesNotContain("store.roles");
        await Assert.That(drawPalette.Groups[1].Value).DoesNotContain("DefDatabase");
        await Assert.That(drawFilters.Groups[1].Value).DoesNotContain("RoleById");
        await Assert.That(drawFilters.Groups[1].Value).DoesNotContain("store.roles");
        await Assert.That(drawFilters.Groups[1].Value).DoesNotContain("DefDatabase");
        await Assert.That(ensureSizes.Groups[1].Value).DoesNotContain("foreach");
        await Assert.That(ensureSizes.Groups[1].Value).DoesNotContain("pawnSets");
        await Assert.That(ensureSizes.Groups[1].Value).DoesNotContain("RoleById");
        await Assert.That(publishedCatalog.Groups[1].Value).DoesNotContain("Role role");
        await Assert.That(publishedCatalog.Groups[1].Value).DoesNotContain("List<Role>");
        await Assert.That(state).Contains("private ColonistSectionsSnapshot sections;");
        await Assert.That(state).Contains("sections.ContentEquals(rebuilt)");
        await Assert.That(state).Contains("private ColonistSkillColumnsSnapshot skillColumns;");
        await Assert.That(state).Contains("skillColumns.ContentEquals(rebuilt)");
        await Assert.That(view).Contains("paletteSnapshot.ContentEquals(rebuilt)");
    }

    [Test]
    public async Task PaletteRoleTooltipsArePublishedBeforeDrawing()
    {
        // This boundary is RimWorld/Unity-runtime-only: the executable test
        // project references deterministic Core, not the net472 game assembly.
        // A production seam solely to instantiate this UI cache is forbidden,
        // so this focused architecture guard verifies the complete publication,
        // dependency, reuse, and teardown contract in source.
        string view = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        string tips = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/TipModels.cs"));
        Match draw = Regex.Match(view,
            @"private void DrawPalette\(.*?// ----- Filter row -----",
            RegexOptions.Singleline);
        Match publishedChip = Regex.Match(view,
            @"private readonly struct PaletteChipSnapshot(.*?)private readonly struct PaletteLabelSnapshot",
            RegexOptions.Singleline);
        Match producer = Regex.Match(view,
            @"private PaletteTipSnapshot EnsurePaletteTips\(.*?/// Lays out the palette",
            RegexOptions.Singleline);
        Match externalRefresh = Regex.Match(view,
            @"internal void RefreshExternalSnapshotIfNeeded\(\)(.*?)/// Window close",
            RegexOptions.Singleline);
        Match paletteLayout = Regex.Match(view,
            @"private PaletteLayoutSnapshot PaletteLayout\(.*?// Owner: Colonists window\. Key: palette layout revision",
            RegexOptions.Singleline);
        Match reset = Regex.Match(view,
            @"public void Reset\(\)(.*?)/// Language-only invalidation",
            RegexOptions.Singleline);
        Match release = Regex.Match(view,
            @"internal void ReleaseSnapshots\(\)(.*?)private void InvalidateTableLayout",
            RegexOptions.Singleline);
        Match assignmentTips = Regex.Match(view,
            @"private StructuredTip RoleTip\(int roleId.*?private TipModel BuildRoleTip",
            RegexOptions.Singleline);

        await Assert.That(draw.Success).IsTrue();
        await Assert.That(publishedChip.Success).IsTrue();
        await Assert.That(producer.Success).IsTrue();
        await Assert.That(externalRefresh.Success).IsTrue();
        await Assert.That(paletteLayout.Success).IsTrue();
        await Assert.That(reset.Success).IsTrue();
        await Assert.That(release.Success).IsTrue();
        await Assert.That(assignmentTips.Success).IsTrue();
        await Assert.That(draw.Groups[0].Value).Contains("role.Tooltip");
        await Assert.That(draw.Groups[0].Value).DoesNotContain("RoleTip(");
        await Assert.That(draw.Groups[0].Value).DoesNotContain("BuildRoleTip(");
        await Assert.That(draw.Groups[0].Value).DoesNotContain("RoleById(");
        await Assert.That(draw.Groups[0].Value).DoesNotContain("store.roles");
        await Assert.That(draw.Groups[0].Value).DoesNotContain("BestFits(");
        await Assert.That(publishedChip.Groups[1].Value)
            .Contains("StructuredTip Tooltip");
        await Assert.That(producer.Groups[0].Value)
            .Contains("paletteTips.ContentEquals(rebuilt)");
        int cacheHit = producer.Groups[0].Value.IndexOf(
            "return paletteTips;", StringComparison.Ordinal);
        int liveRoles = producer.Groups[0].Value.IndexOf(
            "store.roles", StringComparison.Ordinal);
        int tipBuild = producer.Groups[0].Value.IndexOf(
            "BuildRoleTip(", StringComparison.Ordinal);
        await Assert.That(cacheHit).IsGreaterThanOrEqualTo(0);
        await Assert.That(liveRoles).IsGreaterThan(cacheHit);
        await Assert.That(tipBuild).IsGreaterThan(cacheHit);
        await Assert.That(producer.Groups[0].Value)
            .Contains("paletteTipBuiltExternalGeneration");
        await Assert.That(producer.Groups[0].Value)
            .Contains("== paletteTipExternalGeneration");
        await Assert.That(producer.Groups[0].Value)
            .Contains("ReferenceEquals(paletteTipOwner, store)");
        await Assert.That(producer.Groups[0].Value)
            .Contains("paletteTipScopeStamp == stamp");
        await Assert.That(producer.Groups[0].Value)
            .Contains("paletteTipLanguageRevision == languageRevision");
        await Assert.That(producer.Groups[0].Value)
            .Contains("paletteTipDefinitionRevision == definitionRevision");
        await Assert.That(producer.Groups[0].Value)
            .DoesNotContain("rowWidth");
        await Assert.That(producer.Groups[0].Value)
            .DoesNotContain("paletteMode");
        await Assert.That(externalRefresh.Groups[1].Value)
            .Contains("paletteTipExternalGeneration++");
        await Assert.That(paletteLayout.Groups[0].Value)
            .Contains("paletteMode == PaletteMode.Hidden");
        await Assert.That(paletteLayout.Groups[0].Value)
            .Contains("PaletteTipSnapshot.Empty : EnsurePaletteTips(store)");
        await Assert.That(paletteLayout.Groups[0].Value)
            .Contains("ReferenceEquals(paletteLayoutTips, tips)");
        await Assert.That(reset.Groups[1].Value)
            .Contains("paletteTips = null");
        await Assert.That(release.Groups[1].Value)
            .Contains("paletteTips = null");
        await Assert.That(release.Groups[1].Value)
            .Contains("paletteTipOwner = null");
        await Assert.That(assignmentTips.Groups[0].Value)
            .Contains("ActivityTracker.RevisionOf(pawn)");
        await Assert.That(assignmentTips.Groups[0].Value)
            .Contains("RoleTipContext.AssignmentChip");
        await Assert.That(tips)
            .Contains("internal bool ContentEquals(StructuredTip other)");
    }

    [Test]
    public async Task ColonistChromeUsesPublishedSnapshot()
    {
        // RimWorld/Unity UI code has no executable boundary in the Core test
        // assembly; this source guard verifies the cache gate and draw contract
        // without adding a production-only-for-tests seam.
        string view = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match palette = Regex.Match(view,
            @"private void DrawPalette\(.*?// ----- Filter row -----",
            RegexOptions.Singleline);
        Match filters = Regex.Match(view,
            @"private void DrawFilterRow\(.*?private void OpenRoleFilterMenu",
            RegexOptions.Singleline);
        Match table = Regex.Match(view,
            @"private void DrawPawnTable\(.*?private void EnsureTableLayout",
            RegexOptions.Singleline);
        Match emptyTable = Regex.Match(view,
            @"if \(tableListedCount == 0 && rosterState\.FiltersActive\)(.*?)DrawTableHeader",
            RegexOptions.Singleline);
        Match producer = Regex.Match(view,
            @"private ColonistsChromeSnapshot ChromeSnapshot\(.*?private readonly struct PaletteChipSnapshot",
            RegexOptions.Singleline);
        Match published = Regex.Match(view,
            @"private sealed class ColonistsChromeSnapshot(.*?)// Owner: Colonists window\. Key: RoleStore/catalog identity",
            RegexOptions.Singleline);
        Match reset = Regex.Match(view,
            @"public void Reset\(\)(.*?)/// Language-only invalidation",
            RegexOptions.Singleline);
        Match release = Regex.Match(view,
            @"internal void ReleaseSnapshots\(\)(.*?)private void InvalidateTableLayout",
            RegexOptions.Singleline);

        await Assert.That(palette.Success).IsTrue();
        await Assert.That(filters.Success).IsTrue();
        await Assert.That(table.Success).IsTrue();
        await Assert.That(emptyTable.Success).IsTrue();
        await Assert.That(producer.Success).IsTrue();
        await Assert.That(published.Success).IsTrue();
        await Assert.That(reset.Success).IsTrue();
        await Assert.That(release.Success).IsTrue();
        await Assert.That(palette.Groups[0].Value)
            .Contains("ColonistsChromeSnapshot chrome");
        await Assert.That(palette.Groups[0].Value)
            .Contains("chrome.PaletteModeLabel");
        await Assert.That(filters.Groups[0].Value)
            .Contains("ColonistsChromeSnapshot chrome");
        await Assert.That(table.Groups[0].Value)
            .Contains("chrome.NoFilterMatchesLabel");
        await Assert.That(table.Groups[0].Value)
            .DoesNotContain("WR_NoFilterMatches\".Translate(");
        await Assert.That(emptyTable.Groups[1].Value).Contains("finally");
        await Assert.That(emptyTable.Groups[1].Value)
            .Contains("Text.Anchor = oldAnchor");
        await Assert.That(emptyTable.Groups[1].Value)
            .Contains("GUI.color = oldColor");
        foreach (string forbidden in new[]
        {
            ".Translate(", "WrText.FitWidth", ".Truncate(",
            "ColonyScope.LabelOf", "rosterState.ScopeOptions",
            "profile.GetGroupBy", "rosterState.SkillColumns", "new List<"
        })
        {
            await Assert.That(palette.Groups[0].Value)
                .DoesNotContain(forbidden);
            await Assert.That(filters.Groups[0].Value)
                .DoesNotContain(forbidden);
        }

        int cacheHit = producer.Groups[0].Value.IndexOf(
            "return chromeSnapshot;", StringComparison.Ordinal);
        int translation = producer.Groups[0].Value.IndexOf(
            ".Translate(", StringComparison.Ordinal);
        int scopeResolution = producer.Groups[0].Value.IndexOf(
            "ColonyScope.LabelOf", StringComparison.Ordinal);
        int measurement = producer.Groups[0].Value.IndexOf(
            "WrText.FitWidth", StringComparison.Ordinal);
        await Assert.That(cacheHit).IsGreaterThanOrEqualTo(0);
        await Assert.That(translation).IsGreaterThan(cacheHit);
        await Assert.That(scopeResolution).IsGreaterThan(cacheHit);
        await Assert.That(measurement).IsGreaterThan(cacheHit);
        await Assert.That(producer.Groups[0].Value)
            .Contains("chromeSnapshot.ContentEquals(rebuilt)");
        await Assert.That(producer.Groups[0].Value)
            .Contains("ReferenceEquals(chromeOwner, store)");
        await Assert.That(producer.Groups[0].Value)
            .Contains("ReferenceEquals(chromeCatalog, catalog)");
        await Assert.That(producer.Groups[0].Value)
            .Contains("chromePawnListRevision == pawnListRevision");
        await Assert.That(producer.Groups[0].Value)
            .Contains("chromeLocationRevision == locationRevision");
        await Assert.That(producer.Groups[0].Value)
            .Contains("chromeMapId == mapId");
        await Assert.That(producer.Groups[0].Value)
            .Contains("chromeLanguageRevision == languageRevision");
        await Assert.That(producer.Groups[0].Value)
            .Contains("chromeDefinitionRevision == definitionRevision");
        await Assert.That(producer.Groups[0].Value)
            .DoesNotContain("UiVersion.Current");
        await Assert.That(published.Groups[1].Value)
            .Contains("private readonly List<ColonistsScopeMenuOption> scopeOptions;");
        await Assert.That(published.Groups[1].Value)
            .Contains("internal bool ContentEquals(ColonistsChromeSnapshot other)");
        await Assert.That(published.Groups[1].Value)
            .Contains("internal string NoFilterMatchesLabel");
        await Assert.That(published.Groups[1].Value)
            .Contains("other.NoFilterMatchesLabel");
        await Assert.That(producer.Groups[0].Value).DoesNotContain("ToArray()");
        await Assert.That(reset.Groups[1].Value)
            .Contains("InvalidateChromeSnapshot()");
        await Assert.That(release.Groups[1].Value)
            .Contains("InvalidateChromeSnapshot()");
    }

    [Test]
    public async Task ColonistTableHeaderConsumesPublishedSnapshot()
    {
        // This header and its text measurement dependencies exist only in the
        // net472 RimWorld/Unity assembly. Adding a production seam solely for
        // Core tests is forbidden, so this guard protects the complete cache
        // gate, publication, draw, invalidation, equality, and teardown shape.
        string view = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match draw = Regex.Match(view,
            @"private void DrawTableHeader\(.*?private void DrawGroupHeader",
            RegexOptions.Singleline);
        Match producer = Regex.Match(view,
            @"private ColonistTableHeaderSnapshot TableHeaderSnapshot\(.*?/// Fixed header",
            RegexOptions.Singleline);
        Match published = Regex.Match(view,
            @"private sealed class ColonistTableHeaderSnapshot(.*?)// Owner: Colonists window\. Key: RoleStore",
            RegexOptions.Singleline);
        Match refresh = Regex.Match(view,
            @"internal void RefreshExternalSnapshotIfNeeded\(\)(.*?)/// Window close",
            RegexOptions.Singleline);
        Match reset = Regex.Match(view,
            @"public void Reset\(\)(.*?)/// Language-only invalidation",
            RegexOptions.Singleline);
        Match release = Regex.Match(view,
            @"internal void ReleaseSnapshots\(\)(.*?)private void InvalidateTableLayout",
            RegexOptions.Singleline);

        await Assert.That(draw.Success).IsTrue();
        await Assert.That(producer.Success).IsTrue();
        await Assert.That(published.Success).IsTrue();
        await Assert.That(refresh.Success).IsTrue();
        await Assert.That(reset.Success).IsTrue();
        await Assert.That(release.Success).IsTrue();
        await Assert.That(draw.Groups[0].Value)
            .Contains("ColonistTableHeaderSnapshot header");
        await Assert.That(draw.Groups[0].Value)
            .Contains("header.ColumnAt(i)");
        await Assert.That(draw.Groups[0].Value).Contains("finally");
        await Assert.That(draw.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(draw.Groups[0].Value)
            .Contains("Text.Anchor = oldAnchor");
        await Assert.That(draw.Groups[0].Value)
            .Contains("Text.WordWrap = oldWordWrap");
        await Assert.That(draw.Groups[0].Value)
            .Contains("GUI.color = oldColor");
        foreach (string forbidden in new[]
        {
            "rosterState.Catalog(", ".SkillOrNull(",
            "rosterState.SkillColumns", "SkillColumnWidth(",
            "SkillHeaderLabel(", ".Translate(", "profile.GetSortColumn",
            "profile.GetColonistOrder", "new List<"
        })
            await Assert.That(draw.Groups[0].Value)
                .DoesNotContain(forbidden);

        int cacheHit = producer.Groups[0].Value.IndexOf(
            "return tableHeaderSnapshot;", StringComparison.Ordinal);
        int sortResolution = producer.Groups[0].Value.IndexOf(
            "columns.IndexOfDefName", StringComparison.Ordinal);
        int translation = producer.Groups[0].Value.IndexOf(
            ".Translate(", StringComparison.Ordinal);
        int measurement = producer.Groups[0].Value.IndexOf(
            "SkillColumnWidth(", StringComparison.Ordinal);
        await Assert.That(cacheHit).IsGreaterThanOrEqualTo(0);
        await Assert.That(sortResolution).IsGreaterThan(cacheHit);
        await Assert.That(translation).IsGreaterThan(cacheHit);
        await Assert.That(measurement).IsGreaterThan(cacheHit);
        await Assert.That(producer.Groups[0].Value)
            .Contains("ReferenceEquals(tableHeaderOwner, store)");
        await Assert.That(producer.Groups[0].Value)
            .Contains("ReferenceEquals(tableHeaderColumns, columns)");
        await Assert.That(producer.Groups[0].Value)
            .Contains("tableHeaderLanguageRevision == languageRevision");
        await Assert.That(producer.Groups[0].Value)
            .Contains("tableHeaderDefinitionRevision == definitionRevision");
        await Assert.That(producer.Groups[0].Value)
            .Contains("tableHeaderBuiltExternalGeneration == externalGeneration");
        await Assert.That(producer.Groups[0].Value)
            .Contains("tableHeaderSnapshot.ContentEquals(rebuilt)");
        await Assert.That(producer.Groups[0].Value)
            .DoesNotContain("UiVersion.Current");
        await Assert.That(published.Groups[1].Value)
            .Contains("private readonly List<ColonistTableHeaderColumnSnapshot> columns;");
        await Assert.That(published.Groups[1].Value)
            .Contains("internal bool ContentEquals(ColonistTableHeaderSnapshot other)");
        await Assert.That(producer.Groups[0].Value).DoesNotContain("ToArray()");
        await Assert.That(refresh.Groups[1].Value)
            .Contains("tableHeaderExternalGeneration++");
        await Assert.That(reset.Groups[1].Value)
            .Contains("ReleaseTableHeaderSnapshot()");
        await Assert.That(release.Groups[1].Value)
            .Contains("ReleaseTableHeaderSnapshot()");
    }

    [Test]
    public async Task SelectedColonistChromeConsumesActivityRevisionGatedSnapshot()
    {
        string view = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        string state = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistSelectedPanelState.cs"));
        string activityEvents = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/Patches/Patch_ActivityTracking.cs"));
        string presentationEvents = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/Patches/Patch_PawnPresentationTracking.cs"));
        Match draw = Regex.Match(view,
            @"private void DrawStatsPanel(.*?)/// Preview entries from the colony plan",
            RegexOptions.Singleline);

        await Assert.That(draw.Success).IsTrue();
        await Assert.That(draw.Groups[1].Value)
            .Contains("ColonistSelectedPanelSnapshot selected");
        await Assert.That(draw.Groups[1].Value).DoesNotContain("selectedPawn.");
        await Assert.That(draw.Groups[1].Value).DoesNotContain("PortraitsCache.Get");
        await Assert.That(draw.Groups[1].Value).DoesNotContain(".story");
        await Assert.That(draw.Groups[1].Value).DoesNotContain("RoleById");
        await Assert.That(draw.Groups[1].Value).DoesNotContain("WrText.FitWidth");
        await Assert.That(state).Contains("ActivityTracker.RevisionOf(selected)");
        await Assert.That(state).Contains("uiRevision != nextUi");
        await Assert.That(state)
            .Contains("activity.ContentEquals(nextActivitySnapshot)");
        await Assert.That(activityEvents).Contains("Pawn_DraftController");
        await Assert.That(activityEvents).Contains("TryStartMentalState");
        await Assert.That(activityEvents).Contains("ClearMentalStateDirect");
        await Assert.That(presentationEvents).Contains("Pawn.Name");
        await Assert.That(presentationEvents).Contains("PortraitsCache.SetDirty");
        await Assert.That(presentationEvents).Contains("TraitSet.GainTrait");
        await Assert.That(presentationEvents)
            .Contains("TraitSet.RecalculateSuppression");
    }

    [Test]
    public async Task RecommendedRolesPanelConsumesDetachedRenderSnapshot()
    {
        string view = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        string state = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistRecommendationState.cs"));
        Match draw = Regex.Match(view,
            @"// Recommended Roles section:(.*?)/// Preview entries from the colony plan",
            RegexOptions.Singleline);
        Match published = Regex.Match(state,
            @"internal sealed class ColonistRecommendationRenderSnapshot(.*?)^\s*}\s*}\s*$",
            RegexOptions.Singleline | RegexOptions.Multiline);

        await Assert.That(draw.Success).IsTrue();
        await Assert.That(published.Success).IsTrue();
        await Assert.That(draw.Groups[1].Value)
            .Contains("ColonistRecommendationRenderSnapshot preview");
        await Assert.That(draw.Groups[1].Value).DoesNotContain("store.");
        await Assert.That(draw.Groups[1].Value).DoesNotContain("pawnSets");
        await Assert.That(draw.Groups[1].Value).DoesNotContain("RoleById");
        await Assert.That(draw.Groups[1].Value).DoesNotContain("FirstOrDefault");
        await Assert.That(draw.Groups[1].Value).DoesNotContain(".Translate(");
        await Assert.That(draw.Groups[1].Value).DoesNotContain("RoleChipUI.WidthFor");
        await Assert.That(published.Groups[1].Value)
            .DoesNotContain("PawnFixPlan Plan");
        await Assert.That(published.Groups[1].Value)
            .DoesNotContain("IReadOnlyList<(Role role");
        await Assert.That(state).Contains("preview.ContentEquals(rebuilt)");

        int cacheHit = state.IndexOf("return preview;",
            state.IndexOf("RenderSnapshot(", StringComparison.Ordinal),
            StringComparison.Ordinal);
        int externalRead = state.IndexOf("externalSnapshot(pawn);",
            state.IndexOf("RenderSnapshot(", StringComparison.Ordinal),
            StringComparison.Ordinal);
        await Assert.That(cacheHit).IsGreaterThan(0);
        await Assert.That(externalRead).IsGreaterThan(cacheHit);
    }

    [Test]
    public async Task CitedRoleEditorDrawMethodsConsumePublishedRenderState()
    {
        string source = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/RolesTabView.cs"));
        Match editor = Regex.Match(source, @"private void DrawEditor(.*?)/// The role's group", RegexOptions.Singleline);
        Match rules = Regex.Match(source, @"private void DrawRulesSection(.*?)private const float LegendSwatch", RegexOptions.Singleline);
        Match entries = Regex.Match(source, @"private void DrawEntries(.*?)// ----- Available Jobs", RegexOptions.Singleline);
        Match tree = Regex.Match(source, @"private void DrawJobTree(.*?)// ----- Tri-state helpers", RegexOptions.Singleline);

        await AssertConsumesPublishedState(editor);
        await AssertConsumesPublishedState(rules);
        await AssertConsumesPublishedState(entries);
        await AssertConsumesPublishedState(tree);

        static async Task AssertConsumesPublishedState(Match method)
        {
            await Assert.That(method.Success).IsTrue();
            await Assert.That(method.Groups[1].Value).Contains("RoleEditorSnapshot model");
            await Assert.That(method.Groups[1].Value).DoesNotContain("role.");
            await Assert.That(method.Groups[1].Value).DoesNotContain("store.");
            await Assert.That(method.Groups[1].Value).DoesNotContain("RoleStore.Current");
            await Assert.That(method.Groups[1].Value).DoesNotContain(".Translate(");
        }
    }

    [Test]
    public async Task CitedCommandsGuardNoOpRevisionChanges()
    {
        string source = File.ReadAllText(RepositoryFile("src/WorkRoles/RoleCommands.cs"));

        await Assert.That(Method(source, "RestoreSelected")).Contains("restored.Count == 0");
        await Assert.That(Method(source, "SetRoleGroup")).Contains("bool changed");
        await Assert.That(Method(source, "RenameGroup")).Contains("string.Equals");
        await Assert.That(Method(source, "RenameRole")).Contains("string.Equals");
        await Assert.That(Method(source, "SetRoleAutoAssign")).Contains("role.autoAssign == value");
        await Assert.That(Method(source, "MoveEntry")).Contains("from == to");
        await Assert.That(Method(source, "MoveRoleOnPawn")).Contains("from == to");
        await Assert.That(Method(source, "SetCustomSwatch")).Contains("ColorEqual");
    }

    [Test]
    public async Task RoleFileDialogsDeferExternalIoOutsideOnGui()
    {
        string picker = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/Dialog_RoleFilePicker.cs"));
        string import = File.ReadAllText(RepositoryFile("src/WorkRoles/UI/Dialog_ImportSource.cs"));
        Match cachedPath = Regex.Match(picker, @"protected string CachedResolvedPath(.*?)private void RefreshPathOutsideOnGUI", RegexOptions.Singleline);
        Match importDraw = Regex.Match(import, @"public override void DoWindowContents(.*?)private void EnsureTextCache", RegexOptions.Singleline);

        await Assert.That(cachedPath.Success).IsTrue();
        await Assert.That(importDraw.Success).IsTrue();
        await Assert.That(cachedPath.Groups[1].Value).DoesNotContain("File.Exists");
        await Assert.That(importDraw.Groups[1].Value).DoesNotContain("File.ReadAllText");
        await Assert.That(importDraw.Groups[1].Value).DoesNotContain("systemCopyBuffer");
        await Assert.That(importDraw.Groups[1].Value).DoesNotContain("TryOpenPreview(");
    }

    [Test]
    public async Task SpecifiedCachesDocumentTheirRequiredContractInline()
    {
        var caches = new (string file, string anchor)[]
        {
            ("src/WorkRoles/ColonyScope.cs", "mapClassifications ="),
            ("src/WorkRoles/ColonyScope.cs", "locationSnapshots ="),
            ("src/WorkRoles/UI/ColonistsRosterState.cs", "private ScopeOption scope;"),
            ("src/WorkRoles/UI/ColonistsRosterState.cs", "private ColonistsRosterCatalogSnapshot catalog;"),
            ("src/WorkRoles/UI/ColonistsRosterState.cs", "private ColonistSectionsSnapshot sections;"),
            ("src/WorkRoles/UI/ColonistsRosterState.cs", "private ColonistSkillColumnsSnapshot skillColumns;"),
            ("src/WorkRoles/UI/ColonistStatsState.cs", "externalSnapshots ="),
            ("src/WorkRoles/UI/ColonistStatsState.cs", "rosterCellWidths ="),
            ("src/WorkRoles/UI/ColonistStatsState.cs", "presentations ="),
            ("src/WorkRoles/UI/ColonistStatsState.cs", "private int statsStamp"),
            ("src/WorkRoles/UI/ColonistSelectedPanelState.cs", "private RoleStore owner;"),
            ("src/WorkRoles/UI/ColonistRecommendationState.cs", "private RoleStore previewOwner;"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private ColonistsChromeSnapshot chromeSnapshot;"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private PaletteTipSnapshot paletteTips;"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private ColonistSectionsSnapshot tableLayoutSections;"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private ScopeCacheStamp sizeStamp"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private PaletteLayoutSnapshot paletteSnapshot;"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private ScopeCacheStamp skillColumnsWidthStamp"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private static readonly Dictionary<SkillDef, string> skillHeaderLabels"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private readonly Dictionary<(int roleId, RoleTipContext context"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private ColonistTableHeaderSnapshot tableHeaderSnapshot;"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private readonly Dictionary<Pawn, ColonistRowSnapshot> chipLayouts"),
            ("src/WorkRoles/UI/ColonistsTabView.cs", "private Dictionary<Pawn, ColonistChipSequenceSnapshot> chipSequences"),
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
        Match match = Regex.Match(source, $@"public static void {name}\(.*?^\s*}}", RegexOptions.Singleline | RegexOptions.Multiline);
        if (!match.Success)
            throw new InvalidOperationException(name);
        return match.Value;
    }

    private static string InlineContractBefore(string source, string anchor)
    {
        int anchorAt = source.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorAt < 0)
            throw new InvalidOperationException(anchor);
        int ownerAt = source.LastIndexOf("// Owner:", anchorAt, StringComparison.Ordinal);
        if (ownerAt < 0 || anchorAt - ownerAt > 1400)
            return "";
        return source.Substring(ownerAt, anchorAt - ownerAt);
    }

    private static string RepositoryFile(string relativePath)
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(relativePath);
    }
}
