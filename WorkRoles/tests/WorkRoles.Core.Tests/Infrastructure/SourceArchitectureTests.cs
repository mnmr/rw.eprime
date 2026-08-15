using System.Text.RegularExpressions;

namespace WorkRoles.Core.Tests.Infrastructure;

public class SourceArchitectureTests
{
    [Test]
    public async Task ChangesPreviewPublishesDetachedRowsBeforeDrawing()
    {
        // The RimWorld/Unity producer cannot execute at the Core boundary. This
        // guard protects the published rendering boundary without introducing a
        // production seam solely for tests; command-time pawn IDs remain live.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_ChangesPreview.cs"));
        Match producer = Regex.Match(source,
            @"private void EnsureRenderSnapshot\(.*?public override void DoWindowContents",
            RegexOptions.Singleline);
        Match drawChip = Regex.Match(source,
            @"private static void DrawStateChip\(.*?private void EnsureRenderSnapshot",
            RegexOptions.Singleline);
        Match drawRows = Regex.Match(source,
            @"private void DrawVisibleEntries\(.*?^[ ]{8}\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        await Assert.That(producer.Success).IsTrue();
        await Assert.That(drawChip.Success).IsTrue();
        await Assert.That(drawRows.Success).IsTrue();
        await Assert.That(source)
            .Contains("private ChangesPreviewRenderSnapshot renderSnapshot;");
        await Assert.That(source)
            .Contains("// Owner: changes-preview dialog.");
        await Assert.That(source)
            .Contains("internal RoleChipRenderData RenderData { get; }");
        await Assert.That(source)
            .Contains("internal string PawnLabel { get; }");
        await Assert.That(producer.Groups[0].Value)
            .Contains("RoleChipRenderData.From(currentRole)");
        await Assert.That(producer.Groups[0].Value)
            .Contains("entry.pawn?.LabelShortCap");
        await Assert.That(producer.Groups[0].Value)
            .Contains("ExternalPawnFacts.Revisions.RevisionOf(entries[i].pawn)");
        await Assert.That(producer.Groups[0].Value)
            .Contains("UiVersion.Current");
        await Assert.That(producer.Groups[0].Value)
            .Contains("!renderSnapshot.ContentEquals(rebuilt)");
        await Assert.That(drawChip.Groups[0].Value)
            .Contains("RoleChipRenderData role");
        await Assert.That(drawChip.Groups[0].Value)
            .Contains("RoleChipUI.Draw(rect, role");
        await Assert.That(drawChip.Groups[0].Value)
            .DoesNotContain("Role role");
        await Assert.That(drawRows.Groups[0].Value)
            .Contains("descriptor.PawnLabel");
        await Assert.That(drawRows.Groups[0].Value)
            .DoesNotContain("entry.pawn.LabelShortCap");
        await Assert.That(drawRows.Groups[0].Value)
            .DoesNotContain("chip.Role");
        await Assert.That(source)
            .Contains("renderSnapshot = null;");
    }

    [Test]
    public async Task PriorityGridPublishesCellsBeforeDrawingAndTracksLiveDependencies()
    {
        // The RimWorld/Unity producer cannot execute at the Core boundary. This
        // guard keeps authoritative pawn/store/skill reads in the gated producer
        // and verifies the event-driven invalidation/teardown wiring without an
        // artificial production seam solely for tests.
        string grid = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_PriorityGrid.cs"));
        string workSettings = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/Patches/Patch_PawnWorkSettings.cs"));
        string liveFacts = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/Patches/Patch_PriorityGridFacts.cs"));
        Match producer = Regex.Match(grid,
            @"private void EnsureGridSnapshot\(\).*?private void DrawVisibleRows",
            RegexOptions.Singleline);
        Match draw = Regex.Match(grid,
            @"private void DrawVisibleRows\(.*?private void ToggleSort",
            RegexOptions.Singleline);
        Match sort = Regex.Match(grid,
            @"private void ToggleSort\(.*?private static void DrawWorkBoxBackground",
            RegexOptions.Singleline);

        await Assert.That(producer.Success).IsTrue();
        await Assert.That(draw.Success).IsTrue();
        await Assert.That(sort.Success).IsTrue();
        await Assert.That(grid).Contains("private PriorityGridSnapshot gridSnapshot;");
        await Assert.That(grid).Contains("// Owner: priority-grid dialog.");
        await Assert.That(producer.Groups[0].Value)
            .Contains("PriorityGridFacts.Revisions.Current");
        await Assert.That(producer.Groups[0].Value)
            .Contains("PriorityGridFacts.Revisions.RevisionOf(pawns[r])");
        await Assert.That(producer.Groups[0].Value)
            .Contains("UiVersion.Current");
        await Assert.That(producer.Groups[0].Value)
            .Contains("DefinitionReloadCoordinator.Revision");
        await Assert.That(producer.Groups[0].Value)
            .Contains("!gridSnapshot.ContentEquals(rebuilt)");
        foreach (string liveRead in new[]
        {
            "RoleStore.Current", "WorkTypeIsDisabled", "CompiledJobOrders.PriorityFor",
            "CompiledJobOrders.VanillaPriorityFor", "AverageOfRelevantSkillsFor",
            "MaxPassionOfRelevantSkillsFor", "workSettings?.GetPriority"
        })
            await Assert.That(producer.Groups[0].Value).Contains(liveRead);
        foreach (string forbidden in new[]
        {
            "RoleStore.Current", "WorkTypeIsDisabled", "CompiledJobOrders",
            ".skills", ".workSettings", "PriorityFor("
        })
        {
            await Assert.That(draw.Groups[0].Value).DoesNotContain(forbidden);
            await Assert.That(sort.Groups[0].Value).DoesNotContain(forbidden);
        }
        await Assert.That(draw.Groups[0].Value).Contains("gridSnapshot.CellAt(");
        await Assert.That(sort.Groups[0].Value).Contains("CopyPriorities(");
        await Assert.That(grid).Contains("public override void PostClose()");
        await Assert.That(grid).Contains("PriorityGridFacts.Acquire(pawns[r]);");
        await Assert.That(grid).Contains("PriorityGridFacts.ReleaseWatch(pawns[r]);");
        await Assert.That(grid).Contains("gridSnapshot = null;");
        await Assert.That(workSettings).Contains("PriorityGridFacts.Invalidate(__instance);");
        await Assert.That(workSettings).Contains("PriorityGridPriorityTransitionState");
        await Assert.That(liveFacts).Contains("typeof(SkillRecord), nameof(SkillRecord.Learn)");
        await Assert.That(liveFacts).Contains("nameof(SkillRecord.Level), MethodType.Setter");
        await Assert.That(liveFacts).Contains("typeof(ChoiceLetter_GrowthMoment)");
    }

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
        await Assert.That(Regex.IsMatch(roleIo, @"public static partial class RoleCommands.*private static ImportApplyResult ApplyImportToStore", RegexOptions.Singleline)).IsTrue();
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
    public async Task ColonistRowCaptionBuilderRestoresCallerFont()
    {
        // This builder measures game-font text in the RimWorld/Unity assembly,
        // outside the Core executable test boundary. The guard protects the
        // global font contract without adding a production-only test seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match builder = Regex.Match(source,
            @"private List<RowCaptionSegment> BuildRowCaption\(.*?internal float StripHeightFor",
            RegexOptions.Singleline);

        await Assert.That(builder.Success).IsTrue();
        await Assert.That(builder.Groups[0].Value)
            .Contains("GameFont oldFont = Text.Font");
        await Assert.That(builder.Groups[0].Value).Contains("finally");
        await Assert.That(builder.Groups[0].Value)
            .Contains("Text.Font = oldFont");
    }

    [Test]
    public async Task SelectedActivitySlotRestoresCallerGuiState()
    {
        // The RimWorld/Unity drawing assembly is outside the Core executable
        // test boundary. This guard protects exact, exception-safe restoration
        // without adding a production seam solely for GUI-state testing.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match slot = Regex.Match(source,
            @"private static void DrawActivitySlot\(.*?private void DrawStatsPanel",
            RegexOptions.Singleline);

        await Assert.That(slot.Success).IsTrue();
        await Assert.That(slot.Groups[0].Value)
            .Contains("GameFont oldFont = Text.Font");
        await Assert.That(slot.Groups[0].Value)
            .Contains("TextAnchor oldAnchor = Text.Anchor");
        await Assert.That(slot.Groups[0].Value)
            .Contains("bool oldWordWrap = Text.WordWrap");
        await Assert.That(slot.Groups[0].Value)
            .Contains("Color oldColor = GUI.color");
        await Assert.That(slot.Groups[0].Value).Contains("finally");
        await Assert.That(slot.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(slot.Groups[0].Value)
            .Contains("Text.Anchor = oldAnchor");
        await Assert.That(slot.Groups[0].Value)
            .Contains("Text.WordWrap = oldWordWrap");
        await Assert.That(slot.Groups[0].Value)
            .Contains("GUI.color = oldColor");
        await Assert.That(slot.Groups[0].Value)
            .DoesNotContain("GUI.color = Color.white");
        await Assert.That(slot.Groups[0].Value)
            .DoesNotContain("Text.Anchor = TextAnchor.UpperLeft");
        await Assert.That(slot.Groups[0].Value)
            .DoesNotContain("Text.Font = GameFont.Small");
    }

    [Test]
    public async Task SelectedStatsPanelRestoresCallerGuiState()
    {
        // The RimWorld/Unity drawing assembly is outside the Core executable
        // test boundary. This guard protects the panel's exception-safe outer
        // GUI-state boundary without adding a production-only test seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match panel = Regex.Match(source,
            @"private void DrawStatsPanel\(.*?/// Preview entries from the colony plan",
            RegexOptions.Singleline);

        await Assert.That(panel.Success).IsTrue();
        await Assert.That(panel.Groups[0].Value)
            .Contains("GameFont oldFont = Text.Font");
        await Assert.That(panel.Groups[0].Value)
            .Contains("TextAnchor oldAnchor = Text.Anchor");
        await Assert.That(panel.Groups[0].Value)
            .Contains("bool oldWordWrap = Text.WordWrap");
        await Assert.That(panel.Groups[0].Value)
            .Contains("Color oldColor = GUI.color");
        await Assert.That(panel.Groups[0].Value).Contains("finally");
        await Assert.That(panel.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(panel.Groups[0].Value)
            .Contains("Text.Anchor = oldAnchor");
        await Assert.That(panel.Groups[0].Value)
            .Contains("Text.WordWrap = oldWordWrap");
        await Assert.That(panel.Groups[0].Value)
            .Contains("GUI.color = oldColor");
    }

    [Test]
    public async Task SelectedStatsPanelIndexesPublishedSignalIcons()
    {
        // RimWorld/Unity drawing cannot execute at the Core test boundary.
        // This focused guard prevents interface enumeration from returning to
        // the steady render loop without adding a production-only test seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match panel = Regex.Match(source,
            @"private void DrawStatsPanel\(.*?/// Preview entries from the colony plan",
            RegexOptions.Singleline);

        await Assert.That(panel.Success).IsTrue();
        await Assert.That(panel.Groups[0].Value)
            .Contains("int signalIconCount = signalIcons.Count");
        await Assert.That(panel.Groups[0].Value)
            .Contains("signalIcons[signalIconIndex]");
        await Assert.That(panel.Groups[0].Value)
            .DoesNotContain("foreach (Texture2D texture in signalIcons)");
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
    public async Task ToastDrawConsumesRevisionGatedPublishedLayout()
    {
        // Toast measurement and Unity GUI drawing live outside the executable
        // Core boundary. This guard protects their scheduling and publication
        // contract without introducing a game-assembly production seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/WrToast.cs"));
        string window = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/MainTabWindow_WorkRoles.cs"));
        Match update = Regex.Match(source,
            @"internal static void Update\(\)(.*?)internal static void RefreshLayout",
            RegexOptions.Singleline);
        Match refresh = Regex.Match(source,
            @"internal static void RefreshLayout\(.*?public static void Draw",
            RegexOptions.Singleline);
        int drawAt = source.IndexOf("public static void Draw(Rect inRect)",
            StringComparison.Ordinal);
        string draw = drawAt < 0 ? "" : source.Substring(drawAt);

        await Assert.That(update.Success).IsTrue();
        await Assert.That(refresh.Success).IsTrue();
        await Assert.That(draw).IsNotEmpty();
        await Assert.That(window).Contains("WrToast.Update()");
        await Assert.That(window)
            .Contains("WrToast.RefreshLayout(inRect.width)");
        await Assert.That(update.Groups[1].Value)
            .Contains("Time.realtimeSinceStartup");
        await Assert.That(update.Groups[1].Value).Contains("toasts.RemoveAt(i)");
        await Assert.That(update.Groups[1].Value).DoesNotContain("RemoveAll");
        await Assert.That(update.Groups[1].Value).DoesNotContain("foreach");
        await Assert.That(refresh.Groups[0].Value)
            .Contains("layoutToastRevision == toastRevision");
        await Assert.That(refresh.Groups[0].Value)
            .Contains("layoutMaxWidth == maxWidth");
        await Assert.That(refresh.Groups[0].Value)
            .Contains("layoutLanguageRevision == languageRevision");
        await Assert.That(refresh.Groups[0].Value).Contains("Text.CalcSize");
        await Assert.That(refresh.Groups[0].Value).Contains("Text.CalcHeight");
        await Assert.That(refresh.Groups[0].Value)
            .Contains("publishedSnapshot.ContentEquals(rows)");
        await Assert.That(draw)
            .Contains("ToastLayoutSnapshot published = publishedSnapshot");
        await Assert.That(draw).Contains("published.RowAt(i)");
        await Assert.That(draw).Contains("finally");
        await Assert.That(draw).Contains("Text.Font = oldFont");
        await Assert.That(draw).Contains("Text.Anchor = oldAnchor");
        await Assert.That(draw).Contains("Text.WordWrap = oldWordWrap");
        await Assert.That(draw).Contains("GUI.color = oldColor");
        await Assert.That(draw).DoesNotContain("toasts.");
        await Assert.That(draw).DoesNotContain("Time.realtimeSinceStartup");
        await Assert.That(draw).DoesNotContain("Text.Calc");
        await Assert.That(draw).DoesNotContain("foreach");
    }

    [Test]
    public async Task SmallConfirmConsumesCachedPresentation()
    {
        // The RimWorld/Unity dialog cannot execute at the Core boundary. This
        // guard keeps measurement and translation behind their instance cache
        // without introducing a production-only test seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_SmallConfirm.cs"));
        Match measure = Regex.Match(source,
            @"private Vector2 MeasureInitialSize\(.*?private void EnsureChrome",
            RegexOptions.Singleline);
        Match chrome = Regex.Match(source,
            @"private void EnsureChrome\(\)(.*?)public override void OnAcceptKeyPressed",
            RegexOptions.Singleline);
        Match draw = Regex.Match(source,
            @"public override void DoWindowContents\(.*?^[ ]{8}\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        await Assert.That(measure.Success).IsTrue();
        await Assert.That(chrome.Success).IsTrue();
        await Assert.That(draw.Success).IsTrue();
        await Assert.That(source)
            .Contains("public override Vector2 InitialSize => initialSize");
        await Assert.That(measure.Groups[0].Value).Contains("Text.CalcHeight");
        await Assert.That(measure.Groups[0].Value).Contains("finally");
        await Assert.That(measure.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(measure.Groups[0].Value)
            .Contains("Text.WordWrap = oldWordWrap");
        await Assert.That(chrome.Groups[0].Value)
            .Contains("LanguageChangeCoordinator.Revision");
        await Assert.That(chrome.Groups[0].Value).Contains(".Translate()");
        await Assert.That(chrome.Groups[0].Value)
            .Contains("chromeSnapshot.ContentEquals(rebuilt)");
        await Assert.That(draw.Groups[0].Value)
            .Contains("DialogChromeSnapshot chrome = chromeSnapshot");
        await Assert.That(draw.Groups[0].Value).DoesNotContain("Text.Calc");
        await Assert.That(draw.Groups[0].Value).DoesNotContain(".Translate()");
        await Assert.That(draw.Groups[0].Value).Contains("finally");
        await Assert.That(draw.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(draw.Groups[0].Value)
            .Contains("Text.Anchor = oldAnchor");
        await Assert.That(draw.Groups[0].Value)
            .Contains("Text.WordWrap = oldWordWrap");
        await Assert.That(draw.Groups[0].Value)
            .Contains("GUI.color = oldColor");
    }

    [Test]
    public async Task RestorePreviewPublishesWarningGeometryBeforeDrawing()
    {
        // The RimWorld/Unity dialog cannot execute at the Core boundary. This
        // guard protects the warning snapshot and exact GUI-state boundary
        // without introducing a production-only test seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_RestorePreview.cs"));
        Match builder = Regex.Match(source,
            @"private RestoreWarningSnapshot WarningSnapshot\(.*?public override void DoWindowContents",
            RegexOptions.Singleline);
        Match draw = Regex.Match(source,
            @"public override void DoWindowContents\(.*?private void DrawVisibleRows",
            RegexOptions.Singleline);
        Match rows = Regex.Match(source,
            @"private void DrawVisibleRows\(.*?^[ ]{8}\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        await Assert.That(builder.Success).IsTrue();
        await Assert.That(draw.Success).IsTrue();
        await Assert.That(rows.Success).IsTrue();
        await Assert.That(builder.Groups[0].Value)
            .Contains("LanguageChangeCoordinator.Revision");
        await Assert.That(builder.Groups[0].Value)
            .Contains("warningWidth == width");
        await Assert.That(builder.Groups[0].Value)
            .Contains("warningLanguageRevision == languageRevision");
        await Assert.That(builder.Groups[0].Value).Contains("GameFont.Small");
        await Assert.That(builder.Groups[0].Value).Contains("Text.WordWrap = true");
        await Assert.That(builder.Groups[0].Value).Contains("Text.CalcHeight");
        await Assert.That(builder.Groups[0].Value).Contains("finally");
        await Assert.That(builder.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(builder.Groups[0].Value)
            .Contains("Text.WordWrap = oldWordWrap");
        await Assert.That(builder.Groups[0].Value)
            .Contains("warningSnapshot.ContentEquals(rebuilt)");
        await Assert.That(draw.Groups[0].Value)
            .Contains("RestoreWarningSnapshot warning = WarningSnapshot(inRect.width)");
        await Assert.That(draw.Groups[0].Value).DoesNotContain("Text.CalcHeight");
        await Assert.That(draw.Groups[0].Value)
            .DoesNotContain("WR_RestoreOverwriteWarning");
        await Assert.That(draw.Groups[0].Value).Contains("finally");
        await Assert.That(draw.Groups[0].Value)
            .Contains("GUI.color = oldColor");
        await Assert.That(draw.Groups[0].Value)
            .DoesNotContain("GUI.color = Color.white");
        await Assert.That(rows.Groups[0].Value).Contains("finally");
        await Assert.That(rows.Groups[0].Value)
            .Contains("GUI.color = oldColor");
        await Assert.That(rows.Groups[0].Value)
            .DoesNotContain("GUI.color = Color.white");
    }

    [Test]
    public async Task RoleColorPickerPublishesRevisionGatedInitialSize()
    {
        // The vanilla color-picker and Unity text APIs cannot execute at the
        // Core boundary. This guard protects their presentation cache without
        // introducing a production-only test seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_RoleColorPicker.cs"));
        Match property = Regex.Match(source,
            @"public override Vector2 InitialSize.*?private RoleColorPickerSizeSnapshot SizeSnapshot",
            RegexOptions.Singleline);
        Match builder = Regex.Match(source,
            @"private RoleColorPickerSizeSnapshot SizeSnapshot\(.*?private const int BaseColumns",
            RegexOptions.Singleline);

        await Assert.That(property.Success).IsTrue();
        await Assert.That(builder.Success).IsTrue();
        await Assert.That(property.Groups[0].Value)
            .Contains("InitialSize => SizeSnapshot().Size");
        await Assert.That(property.Groups[0].Value).DoesNotContain("Text.Calc");
        await Assert.That(property.Groups[0].Value).DoesNotContain(".Translate()");
        await Assert.That(builder.Groups[0].Value)
            .Contains("LanguageChangeCoordinator.Revision");
        await Assert.That(builder.Groups[0].Value)
            .Contains("sizePickableCount == pickableCount");
        await Assert.That(builder.Groups[0].Value)
            .Contains("sizeLanguageRevision == languageRevision");
        await Assert.That(builder.Groups[0].Value).Contains("GameFont.Medium");
        await Assert.That(builder.Groups[0].Value).Contains("GameFont.Small");
        await Assert.That(builder.Groups[0].Value).Contains("Text.WordWrap = true");
        await Assert.That(builder.Groups[0].Value).Contains("Text.CalcHeight");
        await Assert.That(builder.Groups[0].Value).Contains("finally");
        await Assert.That(builder.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(builder.Groups[0].Value)
            .Contains("Text.WordWrap = oldWordWrap");
        await Assert.That(builder.Groups[0].Value)
            .Contains("sizeSnapshot.ContentEquals(rebuilt)");
    }

    [Test]
    public async Task ImportPreviewPreservesEqualRenderSnapshotIdentity()
    {
        // The game dialog cannot execute at the Core boundary. This guard
        // protects publication and snapshot-only drawing without adding a
        // production seam solely for game-assembly cache behavior.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_ImportPreview.cs"));
        Match snapshot = Regex.Match(source,
            @"private sealed class ImportRenderSnapshot.*?private const float RowH",
            RegexOptions.Singleline);
        Match draw = Regex.Match(source,
            @"public override void DoWindowContents\(.*?private void EnsureUiText",
            RegexOptions.Singleline);
        Match builder = Regex.Match(source,
            @"private void EnsureRenderRows\(.*?private void AddSection",
            RegexOptions.Singleline);

        await Assert.That(snapshot.Success).IsTrue();
        await Assert.That(draw.Success).IsTrue();
        await Assert.That(builder.Success).IsTrue();
        await Assert.That(snapshot.Groups[0].Value)
            .Contains("private readonly RenderRow[] rows");
        await Assert.That(snapshot.Groups[0].Value)
            .Contains("private readonly VariableViewportLayout layout");
        await Assert.That(snapshot.Groups[0].Value).Contains("ContentEquals");
        await Assert.That(snapshot.Groups[0].Value).Contains("layout.ExtentOf(i)");
        await Assert.That(draw.Groups[0].Value)
            .Contains("ImportRenderSnapshot snapshot = renderSnapshot");
        await Assert.That(draw.Groups[0].Value).Contains("snapshot.Calculate");
        await Assert.That(draw.Groups[0].Value)
            .DoesNotContain("renderRows[");
        await Assert.That(draw.Groups[0].Value)
            .DoesNotContain("rowLayout.");
        await Assert.That(source)
            .Contains("DrawMergeRow(row.Section, row.SourceIndex, row.Text");
        await Assert.That(source)
            .Contains("DrawSectionHeader(row.Section, row.Text, width, y)");
        await Assert.That(source).DoesNotContain("row.label, ref row.included");
        await Assert.That(builder.Groups[0].Value)
            .Contains("renderSnapshot.ContentEquals(nextRows, heights)");
        await Assert.That(builder.Groups[0].Value)
            .Contains("new ImportRenderSnapshot");
        await Assert.That(builder.Groups[0].Value).Contains("GameFont.Small");
        await Assert.That(builder.Groups[0].Value)
            .Contains("Text.WordWrap = true");
        await Assert.That(builder.Groups[0].Value).Contains("finally");
        await Assert.That(builder.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(builder.Groups[0].Value)
            .Contains("Text.WordWrap = oldWordWrap");
    }

    [Test]
    public async Task RenameRoleDrawConsumesPublishedLocalizedChrome()
    {
        // The RimWorld/Unity dialog cannot execute at the Core boundary. This
        // guard keeps localization out of repeated OnGUI drawing without
        // adding a production-only test seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_RenameRole.cs"));
        Match builder = Regex.Match(source,
            @"private void EnsureChrome\(\).*?private bool IsNameTaken",
            RegexOptions.Singleline);
        Match draw = Regex.Match(source,
            @"public override void DoWindowContents\(.*?^[ ]{8}\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        await Assert.That(builder.Success).IsTrue();
        await Assert.That(draw.Success).IsTrue();
        await Assert.That(builder.Groups[0].Value)
            .Contains("LanguageChangeCoordinator.Revision");
        await Assert.That(builder.Groups[0].Value).Contains(".Translate()");
        await Assert.That(builder.Groups[0].Value)
            .Contains("chromeSnapshot.ContentEquals(rebuilt)");
        await Assert.That(draw.Groups[0].Value)
            .Contains("RenameChromeSnapshot chrome = chromeSnapshot");
        await Assert.That(draw.Groups[0].Value).Contains("chrome.Title");
        await Assert.That(draw.Groups[0].Value).Contains("chrome.CopySource");
        await Assert.That(draw.Groups[0].Value).Contains("chrome.NameTaken");
        await Assert.That(draw.Groups[0].Value).Contains("chrome.Cancel");
        await Assert.That(draw.Groups[0].Value).Contains("chrome.Ok");
        await Assert.That(draw.Groups[0].Value).DoesNotContain(".Translate(");
        await Assert.That(draw.Groups[0].Value).Contains("finally");
        await Assert.That(draw.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(draw.Groups[0].Value)
            .Contains("GUI.color = oldColor");
        await Assert.That(draw.Groups[0].Value)
            .DoesNotContain("GUI.color = Color.white");
    }

    [Test]
    public async Task RolesTabSteadyChromeIsPublishedBeforeDrawing()
    {
        // The RimWorld/Unity view cannot execute at the Core boundary. This
        // guard protects its localization/def-resolution cache without adding
        // a production-only test seam.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesTabView.cs"));
        Match builder = Regex.Match(source,
            @"private RolesTabChromeSnapshot ChromeSnapshot\(\).*?public void Draw",
            RegexOptions.Singleline);
        Match draw = Regex.Match(source,
            @"public void Draw\(Rect rect\).*?private static void FilterCaption",
            RegexOptions.Singleline);
        Match filters = Regex.Match(source,
            @"private void DrawListFilterRow\(.*?private string pendingSelectLabel",
            RegexOptions.Singleline);

        await Assert.That(builder.Success).IsTrue();
        await Assert.That(draw.Success).IsTrue();
        await Assert.That(filters.Success).IsTrue();
        await Assert.That(builder.Groups[0].Value)
            .Contains("LanguageChangeCoordinator.Revision");
        await Assert.That(builder.Groups[0].Value)
            .Contains("DefinitionReloadCoordinator.Revision");
        await Assert.That(builder.Groups[0].Value)
            .Contains("chromeJobFilter");
        await Assert.That(builder.Groups[0].Value)
            .Contains("GetNamedSilentFail(jobFilter)");
        await Assert.That(builder.Groups[0].Value).Contains(".Translate()");
        await Assert.That(builder.Groups[0].Value).Contains(".Truncate(200f)");
        await Assert.That(builder.Groups[0].Value).Contains("finally");
        await Assert.That(builder.Groups[0].Value)
            .Contains("Text.Font = oldFont");
        await Assert.That(builder.Groups[0].Value)
            .Contains("chromeSnapshot.ContentEquals(rebuilt)");
        await Assert.That(draw.Groups[0].Value)
            .Contains("RolesTabChromeSnapshot chrome = ChromeSnapshot()");
        await Assert.That(draw.Groups[0].Value)
            .Contains("chrome.SelectOrCreateRole");
        await Assert.That(draw.Groups[0].Value).DoesNotContain(".Translate(");
        await Assert.That(filters.Groups[0].Value)
            .Contains("chrome.SearchCaption");
        await Assert.That(filters.Groups[0].Value)
            .Contains("chrome.DisplayModeCaption");
        await Assert.That(filters.Groups[0].Value)
            .Contains("chrome.JobFilterCaption");
        await Assert.That(filters.Groups[0].Value)
            .Contains("chrome.JobFilterShown");
        await Assert.That(filters.Groups[0].Value)
            .DoesNotContain("GetNamedSilentFail");
        await Assert.That(filters.Groups[0].Value)
            .DoesNotContain(".Translate(");
        await Assert.That(source)
            .Contains("Widgets.ButtonText(deleteRect, chrome.DeleteLabel");
    }

    [Test]
    public async Task RolesTabSelectionUsesPublishedRoleCatalog()
    {
        // Role list production depends on game-owned Role objects outside the
        // Core executable boundary. This guard keeps live catalog traversal in
        // the gated producer rather than repeated OnGUI drawing.
        string stateSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesListState.cs"));
        string viewSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesTabView.cs"));
        Match producer = Regex.Match(stateSource,
            @"internal RoleSelectionSnapshot SelectionSnapshot\(.*?private static RoleListRowSnapshot PublishRoleRow",
            RegexOptions.Singleline);
        Match draw = Regex.Match(viewSource,
            @"public void Draw\(Rect rect\).*?private static void FilterCaption",
            RegexOptions.Singleline);
        Match list = Regex.Match(viewSource,
            @"private void DrawRoleList\(.*?// ----- Role-list drag & drop",
            RegexOptions.Singleline);

        await Assert.That(producer.Success).IsTrue();
        await Assert.That(draw.Success).IsTrue();
        await Assert.That(list.Success).IsTrue();
        await Assert.That(producer.Groups[0].Value)
            .Contains("UiVersion.Current");
        await Assert.That(producer.Groups[0].Value)
            .Contains("ReferenceEquals(selectionOwner, store)");
        await Assert.That(producer.Groups[0].Value).Contains("store.roles[i]");
        await Assert.That(producer.Groups[0].Value)
            .Contains("selectionSnapshot.ContentEquals(rebuilt)");
        await Assert.That(draw.Groups[0].Value)
            .Contains("RoleSelectionSnapshot selection = listState.SelectionSnapshot(store)");
        await Assert.That(draw.Groups[0].Value).Contains("selection.FirstRoleId");
        await Assert.That(draw.Groups[0].Value).DoesNotContain("store.roles");
        await Assert.That(list.Groups[0].Value)
            .Contains("selection.NewestRoleIdWithLabel");
        await Assert.That(list.Groups[0].Value).Contains("selection.TryGetRole");
        await Assert.That(list.Groups[0].Value).DoesNotContain("store.roles");
        await Assert.That(list.Groups[0].Value).DoesNotContain("RoleById");
    }

    [Test]
    public async Task RolesTabDesiredHeightUsesRevisionGatedPublishedGeometry()
    {
        // Window sizing and RimWorld defs cannot execute at the Core boundary.
        // This guard keeps authoritative collection reads behind the existing
        // producer and the definition revision gate without adding a seam.
        string viewSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesTabView.cs"));
        string windowSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/MainTabWindow_WorkRoles.cs"));
        Match height = Regex.Match(viewSource,
            @"public float DesiredHeight\(\).*?/// Set on selection change",
            RegexOptions.Singleline);
        Match reset = Regex.Match(viewSource,
            @"public void Reset\(\).*?internal void ReleaseWindowData",
            RegexOptions.Singleline);

        await Assert.That(height.Success).IsTrue();
        await Assert.That(reset.Success).IsTrue();
        await Assert.That(height.Groups[0].Value)
            .Contains("listState.SelectionSnapshot(store)");
        await Assert.That(height.Groups[0].Value).Contains("selection.Count");
        await Assert.That(height.Groups[0].Value)
            .Contains("DefinitionReloadCoordinator.Revision");
        await Assert.That(height.Groups[0].Value)
            .Contains("ReferenceEquals(desiredHeightOwner, store)");
        await Assert.That(height.Groups[0].Value)
            .Contains("desiredHeightRoleCount == roleCount");
        await Assert.That(height.Groups[0].Value)
            .Contains("desiredHeightDefinitionRevision == definitionRevision");
        await Assert.That(height.Groups[0].Value)
            .DoesNotContain("store.roles");
        await Assert.That(height.Groups[0].Value.IndexOf(
                "DefDatabase<WorkTypeDef>.AllDefsListForReading.Count",
                StringComparison.Ordinal))
            .IsGreaterThan(height.Groups[0].Value.IndexOf(
                "ReferenceEquals(desiredHeightOwner, store)",
                StringComparison.Ordinal));
        await Assert.That(reset.Groups[0].Value)
            .Contains("ReleaseDesiredHeightCache()");
        await Assert.That(windowSource)
            .Contains("rolesTab.DesiredHeight()");
        await Assert.That(windowSource)
            .DoesNotContain("RolesTabView.DesiredHeight()");
    }

    [Test]
    public async Task DragGhostsRenderPresentationCapturedAtPress()
    {
        // Unity drag rendering cannot execute at the Core boundary. This guard
        // protects the detached press-to-release session without introducing a
        // production seam solely for game-assembly UI behavior.
        string dragSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RoleDrag.cs"));
        string chipSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RoleChipUI.cs"));
        string listSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesListState.cs"));
        string rolesSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesTabView.cs"));
        string colonistsSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        string recommendationsSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RecommendationsTabView.cs"));
        Match press = Regex.Match(dragSource,
            @"internal static void OnPress\(.*?public static void ObserveSource",
            RegexOptions.Singleline);
        Match update = Regex.Match(dragSource,
            @"public static void Update\(\).*?public static void ResolveMouseUp",
            RegexOptions.Singleline);
        Match cancel = Regex.Match(dragSource,
            @"public static void Cancel\(\).*?^[ ]{8}\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Match roleGhost = Regex.Match(chipSource,
            @"internal static void DrawDragGhost\(\).*?/// Compact chips",
            RegexOptions.Singleline);
        Match groupGhost = Regex.Match(rolesSource,
            @"private static void DrawGroupDragGhost\(\).*?// ----- Right: editor",
            RegexOptions.Singleline);

        await Assert.That(press.Success).IsTrue();
        await Assert.That(update.Success).IsTrue();
        await Assert.That(cancel.Success).IsTrue();
        await Assert.That(roleGhost.Success).IsTrue();
        await Assert.That(groupGhost.Success).IsTrue();
        await Assert.That(press.Groups[0].Value)
            .Contains("RoleChipRenderData role");
        await Assert.That(press.Groups[0].Value)
            .Contains("pendingRoleGhost = role");
        await Assert.That(press.Groups[0].Value)
            .Contains("pendingGroupGhostLabel = ghostLabel");
        await Assert.That(update.Groups[0].Value)
            .Contains("roleGhost = pendingRoleGhost");
        await Assert.That(update.Groups[0].Value)
            .Contains("groupGhostLabel = pendingGroupGhostLabel");
        await Assert.That(cancel.Groups[0].Value)
            .Contains("roleGhost = default(RoleChipRenderData)");
        await Assert.That(cancel.Groups[0].Value)
            .Contains("groupGhostLabel = null");
        await Assert.That(roleGhost.Groups[0].Value)
            .Contains("RoleDrag.RoleGhost");
        await Assert.That(roleGhost.Groups[0].Value)
            .DoesNotContain("RoleStore");
        await Assert.That(roleGhost.Groups[0].Value)
            .DoesNotContain("RoleById");
        await Assert.That(roleGhost.Groups[0].Value)
            .DoesNotContain("RoleChipRenderData.From");
        await Assert.That(groupGhost.Groups[0].Value)
            .Contains("RoleDrag.GroupGhostLabel");
        await Assert.That(groupGhost.Groups[0].Value)
            .Contains("RoleDrag.GroupGhostWidth");
        await Assert.That(groupGhost.Groups[0].Value)
            .DoesNotContain("RoleStore");
        await Assert.That(groupGhost.Groups[0].Value)
            .DoesNotContain("GroupById");
        await Assert.That(groupGhost.Groups[0].Value)
            .DoesNotContain("WrText.FitWidth");
        await Assert.That(listSource)
            .Contains("RoleChipRenderData.From(role)");
        await Assert.That(listSource)
            .Contains("GroupDragWidth = groupDragWidth");
        await Assert.That(listSource)
            .Contains("WrText.FitWidth(section.commandName) + 4f");
        await Assert.That(rolesSource)
            .Contains("RoleDrag.OnPress(dragControlId, publishedRow.Chip");
        await Assert.That(rolesSource)
            .Contains("section.CommandName, section.GroupDragWidth");
        await Assert.That(colonistsSource)
            .Contains("RoleChipUI.DrawDragGhost();");
        await Assert.That(recommendationsSource)
            .Contains("RoleChipUI.DrawDragGhost();");
        await Assert.That(colonistsSource)
            .DoesNotContain("RoleChipUI.DrawDragGhost(dragChip)");
        await Assert.That(recommendationsSource)
            .DoesNotContain("RoleChipUI.DrawDragGhost(dragChip)");
    }

    [Test]
    public async Task RoleGroupHeadersUsePublishedCollapseState()
    {
        // The settings-backed game UI cannot execute at the Core boundary.
        // This guard keeps collapse polling in the revision-gated producer and
        // ensures row publication and the header arrow consume one value.
        string stateSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesListState.cs"));
        string viewSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesTabView.cs"));
        Match builder = Regex.Match(stateSource,
            @"internal RoleListSnapshot Snapshot\(.*?internal RoleSelectionSnapshot",
            RegexOptions.Singleline);
        Match section = Regex.Match(stateSource,
            @"internal sealed class RoleListSectionSnapshot.*?internal sealed class RoleSection",
            RegexOptions.Singleline);
        Match draw = Regex.Match(viewSource,
            @"private void DrawGroupHeader\(.*?/// Organize-only drop",
            RegexOptions.Singleline);

        await Assert.That(builder.Success).IsTrue();
        await Assert.That(section.Success).IsTrue();
        await Assert.That(draw.Success).IsTrue();
        await Assert.That(builder.Groups[0].Value)
            .Contains("bool collapsed = IsSectionCollapsed(section.key)");
        await Assert.That(builder.Groups[0].Value)
            .Contains("PublishSection(section, store, collapsed)");
        await Assert.That(builder.Groups[0].Value)
            .Contains("if (!publishedSection.Collapsed)");
        await Assert.That(section.Groups[0].Value)
            .Contains("Collapsed = collapsed");
        await Assert.That(section.Groups[0].Value)
            .Contains("internal bool Collapsed { get; }");
        await Assert.That(draw.Groups[0].Value)
            .Contains("section.Collapsed ? TexButton.Reveal : TexButton.Collapse");
        await Assert.That(draw.Groups[0].Value)
            .DoesNotContain("IsSectionCollapsed");
        await Assert.That(draw.Groups[0].Value)
            .DoesNotContain("WorkRolesMod.Settings");
    }

    [Test]
    public async Task RolesTabDisplayModeUsesPublishedPreference()
    {
        // The settings-backed game UI cannot execute at the Core boundary.
        // This guard keeps the nested/flat preference in the role-list
        // snapshot used by both the toggle label and the rendered rows.
        string stateSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesListState.cs"));
        string viewSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesTabView.cs"));
        Match builder = Regex.Match(stateSource,
            @"internal RoleListSnapshot Snapshot\(.*?internal RoleSelectionSnapshot",
            RegexOptions.Singleline);
        Match snapshot = Regex.Match(stateSource,
            @"internal sealed class RoleListSnapshot.*?internal sealed class RoleListRowSnapshot",
            RegexOptions.Singleline);
        Match list = Regex.Match(viewSource,
            @"private void DrawRoleList\(.*?// ----- Role-list drag & drop",
            RegexOptions.Singleline);
        Match filters = Regex.Match(viewSource,
            @"private void DrawListFilterRow\(.*?private static void ToggleNestedPreference",
            RegexOptions.Singleline);

        await Assert.That(builder.Success).IsTrue();
        await Assert.That(snapshot.Success).IsTrue();
        await Assert.That(list.Success).IsTrue();
        await Assert.That(filters.Success).IsTrue();
        await Assert.That(builder.Groups[0].Value)
            .Contains("bool nestedPreference = WorkRolesMod.Settings?.nestedRoleTree ?? true");
        await Assert.That(builder.Groups[0].Value)
            .Contains("displayNestedPreference != nestedPreference");
        await Assert.That(builder.Groups[0].Value)
            .Contains("new RoleListSnapshot(rebuiltRows, filtered,");
        await Assert.That(snapshot.Groups[0].Value)
            .Contains("internal bool NestedPreference { get; }");
        int snapshotIndex = list.Groups[0].Value.IndexOf(
            "RoleListSnapshot snapshot = listState.Snapshot",
            StringComparison.Ordinal);
        int filtersIndex = list.Groups[0].Value.IndexOf(
            "DrawListFilterRow", StringComparison.Ordinal);
        await Assert.That(snapshotIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(filtersIndex).IsGreaterThan(snapshotIndex);
        await Assert.That(list.Groups[0].Value)
            .Contains("chrome, snapshot.NestedPreference");
        await Assert.That(filters.Groups[0].Value)
            .Contains("nestedPreference ? chrome.TreeNested : chrome.TreeFlat");
        await Assert.That(filters.Groups[0].Value)
            .Contains("ToggleNestedPreference(nestedPreference)");
        await Assert.That(filters.Groups[0].Value)
            .DoesNotContain("WorkRolesMod.Settings");
    }

    [Test]
    public async Task RoleListEqualRefreshPreservesPublishedIdentity()
    {
        // The RimWorld/Unity producer cannot execute at the Core boundary.
        // This guard protects exact equal-content reuse and store partitioning
        // without introducing a production seam solely for tests.
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesListState.cs"));
        Match builder = Regex.Match(source,
            @"internal RoleListSnapshot Snapshot\(.*?internal RoleSelectionSnapshot",
            RegexOptions.Singleline);
        Match snapshot = Regex.Match(source,
            @"internal sealed class RoleListSnapshot.*?internal sealed class RoleListRowSnapshot",
            RegexOptions.Singleline);
        Match row = Regex.Match(source,
            @"internal sealed class RoleListRowSnapshot.*?internal sealed class RoleListSectionSnapshot",
            RegexOptions.Singleline);
        Match section = Regex.Match(source,
            @"internal sealed class RoleListSectionSnapshot.*?internal sealed class RoleSection",
            RegexOptions.Singleline);
        Match sharedBuilder = Regex.Match(source,
            @"internal static IReadOnlyList<RoleSection> BuildSections\(.*?internal static \(IReadOnlyList<Role>",
            RegexOptions.Singleline);

        await Assert.That(builder.Success).IsTrue();
        await Assert.That(snapshot.Success).IsTrue();
        await Assert.That(row.Success).IsTrue();
        await Assert.That(section.Success).IsTrue();
        await Assert.That(sharedBuilder.Success).IsTrue();
        await Assert.That(source).Contains("private RoleStore displayOwner;");
        await Assert.That(source).Contains("private static RoleStore sectionsCacheOwner;");
        await Assert.That(builder.Groups[0].Value)
            .Contains("bool ownerChanged = !ReferenceEquals(displayOwner, store)");
        await Assert.That(builder.Groups[0].Value)
            .Contains("var rebuilt = new RoleListSnapshot(rebuiltRows, filtered,");
        await Assert.That(builder.Groups[0].Value)
            .Contains("if (ownerChanged || snapshot == null");
        await Assert.That(builder.Groups[0].Value)
            .Contains("|| !snapshot.ContentEquals(rebuilt))");
        await Assert.That(builder.Groups[0].Value)
            .Contains("displayOwner = store;");
        await Assert.That(snapshot.Groups[0].Value)
            .Contains("internal bool ContentEquals(RoleListSnapshot other)");
        await Assert.That(snapshot.Groups[0].Value)
            .Contains("Filtered != other.Filtered");
        await Assert.That(snapshot.Groups[0].Value)
            .Contains("NestedPreference != other.NestedPreference");
        await Assert.That(snapshot.Groups[0].Value)
            .Contains("leftRow.Section.ContentEquals(rightRow.Section)");
        await Assert.That(snapshot.Groups[0].Value)
            .Contains("leftRow.ContentEqualsExcludingSection(rightRow)");
        await Assert.That(row.Groups[0].Value)
            .Contains("internal bool ContentEqualsExcludingSection(");
        foreach (string field in new[]
        {
            "Chip.ContentEquals(other.Chip)", "Depth != other.Depth",
            "VirtualRow != other.VirtualRow", "Invalid != other.Invalid",
            "Label", "Tooltip.ContentEquals(other.Tooltip)",
            "Enabled != other.Enabled", "HasCustomColor != other.HasCustomColor",
            "ColorEquals(Color, other.Color)", "Blocker != other.Blocker",
            "HasTimeRule != other.HasTimeRule",
            "HasLocationRule != other.HasLocationRule",
            "Composite != other.Composite", "VirtualOriginGroupLabel"
        })
            await Assert.That(row.Groups[0].Value).Contains(field);
        await Assert.That(section.Groups[0].Value)
            .Contains("internal bool ContentEquals(RoleListSectionSnapshot other)");
        foreach (string field in new[]
        {
            "Key", "DisplayTitle", "CommandName", "GroupId", "GroupIndex",
            "Collapsed", "Renamable", "Draggable", "DropTarget",
            "FirstRootRoleId", "GroupDragWidth", "nestedRoleIds"
        })
            await Assert.That(section.Groups[0].Value).Contains(field);
        await Assert.That(sharedBuilder.Groups[0].Value)
            .Contains("!ReferenceEquals(sectionsCacheOwner, store)");
        await Assert.That(sharedBuilder.Groups[0].Value)
            .Contains("ReleaseSectionsSnapshot();");
        await Assert.That(source)
            .Contains("sectionsCacheOwner = null;");
    }

    [Test]
    public async Task RolesTabCommitUsesPublishedDeadEntryStateOrPrimitiveCommand()
    {
        // The multiplayer command boundary lives in the game assembly. This
        // guard keeps duplicate model/catalog traversal out of the UI input
        // path without introducing a production seam solely for the test.
        string stateSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RoleEditorState.cs"));
        string viewSource = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RolesTabView.cs"));
        Match publishedState = Regex.Match(stateSource,
            @"internal bool TryGetPublishedDeadEntryState\(.*?internal RoleEntryPresentation",
            RegexOptions.Singleline);
        Match commit = Regex.Match(viewSource,
            @"public void CommitEdits\(\).*?public void Reset",
            RegexOptions.Singleline);

        await Assert.That(publishedState.Success).IsTrue();
        await Assert.That(commit.Success).IsTrue();
        await Assert.That(publishedState.Groups[0].Value)
            .Contains("deadEntriesStamp != UiVersion.Current");
        await Assert.That(publishedState.Groups[0].Value)
            .Contains("deadEntriesRoleId != roleId");
        await Assert.That(publishedState.Groups[0].Value)
            .Contains("deadEntries.Count > 0");
        await Assert.That(publishedState.Groups[0].Value)
            .DoesNotContain("JobOrderCompiler.DeadEntryIndexes");
        await Assert.That(commit.Groups[0].Value)
            .Contains("editorState.TryGetPublishedDeadEntryState");
        await Assert.That(commit.Groups[0].Value)
            .Contains("RoleCommands.ScrubDeadEntries(roleId)");
        await Assert.That(commit.Groups[0].Value)
            .DoesNotContain("RoleStore.Current");
        await Assert.That(commit.Groups[0].Value).DoesNotContain("RoleById");
        await Assert.That(commit.Groups[0].Value)
            .DoesNotContain("JobOrderCompiler.DeadEntryIndexes");
        await Assert.That(commit.Groups[0].Value)
            .DoesNotContain("GameJobCatalog.Instance");
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
            ("src/WorkRoles/UI/RoleEditorState.cs", "private int tipsLanguageRevision"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private List<RoleSkillPresentation> skillsUsed;"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private List<RoleHolderPresentation> holders;"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private HashSet<int> deadEntries;"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private readonly Dictionary<(JobEntryKind kind, string defName)"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private HashSet<string> uncoveredGivers;"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private List<RoleJobTreeNode> treeNodes;"),
            ("src/WorkRoles/UI/RoleEditorState.cs", "private int entrySetsStamp"),
            ("src/WorkRoles/UI/RolesTabView.cs", "private readonly MemoizedFactory<int, System.Action<int, int>>"),
            ("src/WorkRoles/UI/Dialog_RoleFilePicker.cs", "private Location cachedLocation;"),
            ("src/WorkRoles/UI/Dialog_ExportPreview.cs", "private float measuredWidth"),
            ("src/WorkRoles/UI/Dialog_ImportSource.cs", "private string clip;"),
            ("src/WorkRoles/UI/WrToast.cs", "private static readonly List<Toast> toasts"),
            ("src/WorkRoles/UI/Dialog_SmallConfirm.cs", "private DialogChromeSnapshot chromeSnapshot;"),
            ("src/WorkRoles/UI/Dialog_RestorePreview.cs", "private RestoreWarningSnapshot warningSnapshot;"),
            ("src/WorkRoles/UI/Dialog_RoleColorPicker.cs", "private RoleColorPickerSizeSnapshot sizeSnapshot;"),
            ("src/WorkRoles/UI/Dialog_ImportPreview.cs", "private ImportRenderSnapshot renderSnapshot;"),
            ("src/WorkRoles/UI/Dialog_RenameRole.cs", "private RenameChromeSnapshot chromeSnapshot;"),
            ("src/WorkRoles/UI/RolesTabView.cs", "private RolesTabChromeSnapshot chromeSnapshot;"),
            ("src/WorkRoles/UI/RolesTabView.cs", "private RoleStore desiredHeightOwner;"),
            ("src/WorkRoles/UI/RolesListState.cs", "private RoleSelectionSnapshot selectionSnapshot;"),
            ("src/WorkRoles/UI/RoleDrag.cs", "private static RoleChipRenderData pendingRoleGhost;"),
            ("src/WorkRoles/UI/Dialog_PriorityGrid.cs", "private PriorityGridSnapshot gridSnapshot;"),
            ("src/WorkRoles/UI/Dialog_ChangesPreview.cs", "private ChangesPreviewRenderSnapshot renderSnapshot;"),
            ("src/WorkRoles/UI/OptionsTabState.cs", "private OptionsRenderSnapshot snapshot;"),
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

    [Test]
    public async Task ImportPaletteSelectionsUseStableFileIndicesAndPureStoreProjection()
    {
        // The game assembly cannot execute at this test boundary: RoleIO and the
        // dialog depend on Verse and Unity. Guard the stable synced-selection and
        // invalidation contract at their production boundary instead of adding an
        // artificial Core seam solely for this check.
        string roleIo = File.ReadAllText(RepositoryFile("src/WorkRoles/RoleIO.cs"));
        string preview = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_ImportPreview.cs"));
        Match paletteProjection = Regex.Match(roleIo,
            @"public static List<PaletteRow> PaletteMergeRows\(.*?^\s*}\s*$",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Match paletteApply = Regex.Match(roleIo,
            @"else if \(paletteInclude\)(.*?)^\s*}\s*\r?\n\s*if \(rolesInclude\)",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Match mergeGate = Regex.Match(preview,
            @"private void EnsureMergeRows\(\)(.*?)private void EnsureUiText",
            RegexOptions.Singleline);

        await Assert.That(paletteProjection.Success).IsTrue();
        await Assert.That(paletteApply.Success).IsTrue();
        await Assert.That(mergeGate.Success).IsTrue();
        await Assert.That(paletteProjection.Value).Contains("sourceIndex = i");
        await Assert.That(paletteProjection.Value)
            .DoesNotContain("store.SyncSwatchNames()");
        await Assert.That(paletteApply.Groups[1].Value)
            .Contains("doc.palette[sourceIndex]");
        await Assert.That(paletteApply.Groups[1].Value)
            .DoesNotContain("PaletteMergeRows");
        await Assert.That(mergeGate.Groups[1].Value)
            .Contains("ReferenceEquals(mergeOwner, store)");
        await Assert.That(mergeGate.Groups[1].Value)
            .Contains("mergeUiRevision == uiRevision");
        await Assert.That(preview).Contains("UiVersion.Current");
        await Assert.That(preview)
            .Contains("paletteRows = SelectedPaletteSourceIndices()");
    }

    [Test]
    public async Task RecommendationsSteadyDrawConsumesPublishedChromeLabels()
    {
        // Recommendations UI depends on Verse/Unity and cannot execute in the
        // Core test assembly. Keep this focused on the steady draw regions
        // that previously translated their own chrome.
        string view = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/RecommendationsTabView.cs"));
        Match tabDraw = Regex.Match(view,
            @"public void Draw\(Rect rect\)(.*?)private static RecPathView FindPathView",
            RegexOptions.Singleline);
        Match expandedBody = Regex.Match(view,
            @"private void DrawExpandedBody\(.*?private static int DrawOptionSegments",
            RegexOptions.Singleline);
        Match pathBlock = Regex.Match(view,
            @"private float DrawPathBlock\(.*?private static void OpenAddRoleMenu",
            RegexOptions.Singleline);
        Match bandRows = Regex.Match(view,
            @"private void DrawBandRows\(.*?private void CommitBandDrag",
            RegexOptions.Singleline);

        await Assert.That(tabDraw.Success).IsTrue();
        await Assert.That(expandedBody.Success).IsTrue();
        await Assert.That(pathBlock.Success).IsTrue();
        await Assert.That(bandRows.Success).IsTrue();
        await Assert.That(tabDraw.Groups[1].Value).DoesNotContain(".Translate(");
        await Assert.That(expandedBody.Value).DoesNotContain(".Translate(");
        await Assert.That(pathBlock.Value).DoesNotContain(".Translate(");
        await Assert.That(bandRows.Value).DoesNotContain(".Translate(");
        await Assert.That(tabDraw.Groups[1].Value).Contains("order.HeaderLabel");
        await Assert.That(tabDraw.Groups[1].Value).Contains("panels.HeaderLabel");
        await Assert.That(tabDraw.Groups[1].Value).Contains("tuning.HeaderLabel");
        await Assert.That(expandedBody.Value).Contains("detail.TrainingHeader");
        await Assert.That(bandRows.Value).Contains("state.Order.AddLabel");
    }

    [Test]
    public async Task OptionsSteadyDrawConsumesPublishedSnapshot()
    {
        // The Options UI depends on Verse/Unity and has no executable Core
        // boundary. Guard the actual draw method against returning to live model
        // reads, translation, string construction, or settings persistence.
        string view = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/OptionsTabView.cs"));
        string state = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/OptionsTabState.cs"));
        Match draw = Regex.Match(view,
            @"public void Draw\(Rect rect\)(.*?)private static bool\? DisplayToggle",
            RegexOptions.Singleline);
        Match toggle = Regex.Match(view,
            @"private static bool\? DisplayToggle\(.*?^\s*}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        await Assert.That(draw.Success).IsTrue();
        await Assert.That(toggle.Success).IsTrue();
        await Assert.That(draw.Groups[1].Value)
            .Contains("OptionsRenderSnapshot snapshot = state.Snapshot(store)");
        foreach (string forbidden in new[]
        {
            ".Translate(", "Current.Game", "reportVanillaPriorities",
            "WorkRolesMod.Settings", ".Write()", " + \"Tip\""
        })
        {
            await Assert.That(draw.Groups[1].Value).DoesNotContain(forbidden);
            await Assert.That(toggle.Value).DoesNotContain(forbidden);
        }
        await Assert.That(state).Contains("sealed class OptionsRenderSnapshot");
        await Assert.That(state).Contains("ContentEquals(OptionsRenderSnapshot other)");
        await Assert.That(view).Contains("WorkRolesGameComponent.RequestSettingsWrite()");
    }

    [Test]
    public async Task UiSettingsWritesAreDeferredAndCoalescedOutsideOnGui()
    {
        // The game UI assembly is unavailable to executable Core tests. Guard
        // the persistence boundary that previously performed disk I/O in OnGUI.
        string component = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/WorkRolesGameComponent.cs"));
        foreach (string relativePath in new[]
        {
            "src/WorkRoles/UI/ColonistsTabView.cs",
            "src/WorkRoles/UI/ColonistsViewProfile.cs",
            "src/WorkRoles/UI/MainTabWindow_WorkRoles.cs",
            "src/WorkRoles/UI/OptionsTabView.cs",
            "src/WorkRoles/UI/RolesListState.cs",
            "src/WorkRoles/UI/RolesTabView.cs"
        })
        {
            string source = File.ReadAllText(RepositoryFile(relativePath));
            await Assert.That(source).DoesNotContain("WorkRolesMod.Settings?.Write()");
            await Assert.That(source).DoesNotContain("WorkRolesMod.Settings.Write()");
        }

        await Assert.That(component).Contains("public static void RequestSettingsWrite()");
        await Assert.That(component).Contains("settingsWritePending");
        await Assert.That(component).Contains("deferredUi.Enqueue(writeSettingsAction)");
        await Assert.That(component).Contains("settingsWritePending = false");
        await Assert.That(component).Contains("WorkRolesMod.Settings?.Write()");
    }

    [Test]
    public async Task DefinitionDerivedUiCachesObserveDefinitionReloads()
    {
        // These builders depend on Verse defs, so the stable test boundary is
        // the declared invalidation key rather than an artificial Core seam.
        foreach (string relativePath in new[]
        {
            "src/WorkRoles/UI/ActivityState.cs",
            "src/WorkRoles/UI/ColonistSelectedPanelState.cs",
            "src/WorkRoles/UI/ColonistsTabView.cs",
            "src/WorkRoles/UI/RecommendationsTabState.cs",
            "src/WorkRoles/UI/RoleEditorState.cs",
            "src/WorkRoles/UI/RolesListState.cs",
            "src/WorkRoles/UI/Dialog_RestorePreview.cs"
        })
        {
            string source = File.ReadAllText(RepositoryFile(relativePath));
            await Assert.That(source).Contains("DefinitionReloadCoordinator.Revision");
        }
    }

    [Test]
    public async Task RestorePreviewRefreshesRowsAndPreservesSelectionsByStableKey()
    {
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/Dialog_RestorePreview.cs"));

        await Assert.That(source).Contains("EnsureRowsCurrent()");
        await Assert.That(source).Contains("RestoreItemKey");
        await Assert.That(source).Contains("Dictionary<RestoreItemKey, bool>");
        await Assert.That(source).Contains("UiVersion.Current");
        await Assert.That(source).Contains("DefinitionReloadCoordinator.Revision");
        await Assert.That(source).Contains("LanguageChangeCoordinator.Revision");
        await Assert.That(source).Contains("public override void PostClose()");
    }

    [Test]
    public async Task NoOpImportDoesNotPublishARevisionOrSuccessToast()
    {
        string commands = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/RoleCommands.cs"));
        string import = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/RoleIO.cs"));
        Match apply = Regex.Match(commands,
            @"public static void ApplyImport\(.*?^\s*}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Match storeApply = Regex.Match(import,
            @"private static ImportApplyResult ApplyImportToStore\(.*?^\s*}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        await Assert.That(apply.Success).IsTrue();
        await Assert.That(storeApply.Success).IsTrue();
        await Assert.That(import).Contains("readonly struct ImportApplyResult");
        await Assert.That(apply.Value).Contains("if (!result.Changed)");
        await Assert.That(apply.Value).Contains("return;");
        await Assert.That(apply.Value).Contains("UiVersion.Bump()");
        await Assert.That(apply.Value).Contains("WrToast.Show(result.Summary");
    }

    [Test]
    public async Task ColonistRowTextMetricsUseTheirExactRenderingInputs()
    {
        string source = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/ColonistsTabView.cs"));
        Match metrics = Regex.Match(source,
            @"private RowTextMetrics TextMetrics\(\)(.*?)/// The pawn's best skills",
            RegexOptions.Singleline);

        await Assert.That(metrics.Success).IsTrue();
        await Assert.That(metrics.Value).Contains("Text.TinyFontSupported");
        await Assert.That(metrics.Value).Contains("Text.LineHeightOf(GameFont.Small)");
        await Assert.That(metrics.Value).Contains("captionLineHeight");
        await Assert.That(metrics.Value).DoesNotContain("UiVersion.Current");
        await Assert.That(source).Contains("chipLayoutTextMetrics.ContentEquals(textMetrics)");
        await Assert.That(source).Contains("tableLayoutTextMetrics.ContentEquals(textMetrics)");
        await Assert.That(source).Contains("sizeTextMetrics.ContentEquals(textMetrics)");
    }

    [Test]
    public async Task LanguageOnlyStructuredTipsUseLanguageRevisionAndReuseEqualContent()
    {
        foreach (string relativePath in new[]
        {
            "src/WorkRoles/UI/RecommendationsTabState.cs",
            "src/WorkRoles/UI/RoleEditorState.cs"
        })
        {
            string source = File.ReadAllText(RepositoryFile(relativePath));
            Match ensure = Regex.Match(source,
                @"(?:internal|private) void EnsureTips\(\)(.*?)^\s*}",
                RegexOptions.Singleline | RegexOptions.Multiline);
            await Assert.That(ensure.Success).IsTrue();
            await Assert.That(ensure.Value).Contains("LanguageChangeCoordinator.Revision");
            await Assert.That(ensure.Value).Contains("ContentEquals");
            await Assert.That(ensure.Value).DoesNotContain("UiVersion.Current");
        }
    }

    [Test]
    public async Task WorkRolesWindowsRestoreGuiAndScrollOwnershipOnExceptions()
    {
        // Unity's GUI stack cannot execute in Core. Guard the actual ownership
        // boundaries: every WorkRoles content pass has a state scope, while
        // every scroll Begin is paired locally in a finally block.
        string main = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/MainTabWindow_WorkRoles.cs"));
        string scope = File.ReadAllText(RepositoryFile(
            "src/WorkRoles/UI/GuiStateScope.cs"));
        await Assert.That(main).Contains("new GuiStateScope(capture: true)");
        await Assert.That(scope).Contains("Text.CurFontStyle.fontStyle");
        await Assert.That(scope).Contains("GUI.matrix = matrix");

        foreach (string relativePath in new[]
        {
            "src/WorkRoles/UI/ColonistsTabView.cs",
            "src/WorkRoles/UI/Dialog_ChangesPreview.cs",
            "src/WorkRoles/UI/Dialog_ExportPreview.cs",
            "src/WorkRoles/UI/Dialog_ImportPreview.cs",
            "src/WorkRoles/UI/Dialog_PriorityGrid.cs",
            "src/WorkRoles/UI/Dialog_RestorePreview.cs",
            "src/WorkRoles/UI/RecommendationsTabView.cs",
            "src/WorkRoles/UI/RolesTabView.cs"
        })
        {
            string source = File.ReadAllText(RepositoryFile(relativePath));
            int searchAt = 0;
            while ((searchAt = source.IndexOf("Widgets.BeginScrollView(", searchAt,
                       StringComparison.Ordinal)) >= 0)
            {
                int end = source.IndexOf("Widgets.EndScrollView()", searchAt,
                    StringComparison.Ordinal);
                await Assert.That(end).IsGreaterThan(searchAt);
                string ownership = source.Substring(searchAt,
                    end + "Widgets.EndScrollView()".Length - searchAt);
                await Assert.That(ownership).Contains("try");
                await Assert.That(ownership).Contains("finally");
                searchAt = end + 1;
            }
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
