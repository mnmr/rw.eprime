namespace WorkRoles.Core.Tests;

public class UiViewSeparationArchitectureTests
{
    [Test]
    public async Task OptionsViewDelegatesSnapshotAndInvalidationOwnership()
    {
        string view = Source("OptionsTabView.cs");
        string state = Source("OptionsTabState.cs");

        await Assert.That(view)
            .Contains("private readonly OptionsTabState state = new OptionsTabState();");
        await Assert.That(view).Contains("state.EnsureOrder(store,");
        await Assert.That(view).Contains("state.EnsurePaths(store,");
        await Assert.That(view).Contains("state.EnsureTips();");
        await Assert.That(view).DoesNotContain("private int orderStamp");
        await Assert.That(view).DoesNotContain("private int pathStamp");
        await Assert.That(view).DoesNotContain("private int optTipsStamp");
        await Assert.That(view).DoesNotContain("private void EnsureSnapshot(");
        await Assert.That(view).DoesNotContain("private void EnsurePathSnapshot(");

        await Assert.That(state).Contains("internal sealed class OptionsTabState");
        await Assert.That(state).Contains("internal void EnsureOrder(");
        await Assert.That(state).Contains("internal void EnsurePaths(");
        await Assert.That(state).Contains("internal void EnsureTips()");
        await Assert.That(state).Contains("internal void InvalidateLanguageCaches()");
    }

    [Test]
    public async Task RolesViewDelegatesListProjectionOwnership()
    {
        string view = Source("RolesTabView.cs");
        string state = Source("RolesListState.cs");

        await Assert.That(view)
            .Contains("private readonly RolesListState listState = new RolesListState();");
        await Assert.That(view).Contains("listState.Snapshot(");
        await Assert.That(view).DoesNotContain("private static readonly List<RoleSection>[] sectionsCache");
        await Assert.That(view).DoesNotContain("private List<(RoleSection section");
        await Assert.That(view).DoesNotContain("private string roleSearch");

        await Assert.That(state).Contains("internal sealed class RolesListState");
        await Assert.That(state).Contains("internal RoleListSnapshot Snapshot(");
        await Assert.That(state).Contains(
            "internal static IReadOnlyList<RoleSection> BuildSections(");
        await Assert.That(state).Contains(
            "internal static (IReadOnlyList<Role> roots,");
        await Assert.That(state).Contains(
            "internal IReadOnlyList<(RoleSection section, Role role, Role parent, bool virtualRow)> Rows { get; }");
        await Assert.That(state).Contains("internal void InvalidateLanguageCaches()");
    }

    [Test]
    public async Task RolesViewDelegatesEditorProjectionOwnership()
    {
        string view = Source("RolesTabView.cs");
        string state = Source("RoleEditorState.cs");

        await Assert.That(view)
            .Contains("private readonly RoleEditorState editorState = new RoleEditorState();");
        await Assert.That(view).Contains("editorState.TreeNodes(");
        await Assert.That(view).Contains("editorState.EntryPresentation(");
        await Assert.That(view).DoesNotContain("private static List<(string label, bool primary)> skillsUsedCache");
        await Assert.That(view).DoesNotContain("private List<(Pawn pawn, int position)> holdersCache");
        await Assert.That(view).DoesNotContain("private static HashSet<int> deadEntriesCache");
        await Assert.That(view).DoesNotContain("private List<(WorkTypeDef type, WorkGiverDef giver, string label)> treeNodesCache");

        await Assert.That(state).Contains("internal sealed class RoleEditorState");
        await Assert.That(state).Contains("internal IReadOnlyList<RoleSkillPresentation> SkillsUsed(");
        await Assert.That(state).Contains("internal IReadOnlyList<RoleHolderPresentation> Holders(");
        await Assert.That(state).Contains("internal RoleEntryPresentation EntryPresentation(");
        await Assert.That(state).Contains("internal IReadOnlyList<RoleJobTreeNode> TreeNodes(");
        await Assert.That(state).Contains("internal void InvalidateLanguageCaches()");
    }

    [Test]
    public async Task ColonistsViewDelegatesRosterProjectionOwnership()
    {
        string view = Source("ColonistsTabView.cs");
        string state = Source("ColonistsRosterState.cs");

        await Assert.That(view)
            .Contains("private readonly ColonistsRosterState rosterState;");
        await Assert.That(view).Contains("rosterState.Sections(store)");
        await Assert.That(view).Contains("rosterState.ListedPawns()");
        await Assert.That(view).DoesNotContain("private readonly PawnListRevisionTracker pawnListRevisions");
        await Assert.That(view).DoesNotContain("private List<GroupSection<Pawn>> sectionsCache");
        await Assert.That(view).DoesNotContain("private string colonistFilter");
        await Assert.That(view).DoesNotContain("private readonly List<SkillDef> skillColumns");

        await Assert.That(state).Contains("internal sealed class ColonistsRosterState");
        await Assert.That(state).Contains("internal IReadOnlyList<Pawn> ListedPawns()");
        await Assert.That(state).Contains("internal IReadOnlyList<GroupSection<Pawn>> Sections(");
        await Assert.That(state).Contains("internal void InvalidateLanguageCaches()");
        await Assert.That(state).Contains("internal void ReleaseSnapshots()");
    }

    [Test]
    public async Task ColonistsViewDelegatesRecommendationProjectionOwnership()
    {
        string view = Source("ColonistsTabView.cs");
        string state = Source("ColonistRecommendationState.cs");

        await Assert.That(view)
            .Contains("private readonly ColonistRecommendationState recommendationState");
        await Assert.That(view).Contains("recommendationState.Preview(");
        await Assert.That(view).Contains("recommendationState.FixEntries(");
        await Assert.That(view).DoesNotContain("private List<PawnFixPlan> planCache");
        await Assert.That(view).DoesNotContain("private List<(Role role, Dialog_ChangesPreview.ChipState state, string tip)> previewChips");
        await Assert.That(view).DoesNotContain("private sealed class PawnFixPlan");

        await Assert.That(state).Contains("internal sealed class ColonistRecommendationState");
        await Assert.That(state).Contains(
            "private IReadOnlyList<PawnFixPlan> previewSource;");
        await Assert.That(state).Contains("internal IReadOnlyList<PawnFixPlan> Plans(");
        await Assert.That(state).DoesNotContain("(List<PawnFixPlan>)current");
        await Assert.That(state).Contains("internal ColonistRecommendationPreview Preview(");
        await Assert.That(state).Contains("internal List<Dialog_ChangesPreview.PawnPreview> FixEntries(");
        await Assert.That(state).Contains("internal void InvalidateLanguageCaches()");
    }

    [Test]
    public async Task ColonistsViewDelegatesSignalAndStatsProjectionOwnership()
    {
        string view = Source("ColonistsTabView.cs");
        string state = Source("ColonistStatsState.cs");

        await Assert.That(view).Contains("private readonly ColonistStatsState statsState;");
        await Assert.That(view).Contains("statsState.Snapshot(selectedPawn)");
        await Assert.That(view).Contains("statsState.PresentationFor(");
        await Assert.That(view).DoesNotContain("private long signalRevision");
        await Assert.That(view).DoesNotContain("private int statsStamp");
        await Assert.That(view).DoesNotContain("private readonly Dictionary<(Pawn pawn, SkillDef skill), SkillPresentation>");

        await Assert.That(state).Contains("internal sealed class ColonistStatsState");
        await Assert.That(state).Contains("internal PawnSignalSnapshot Observe(Pawn pawn)");
        await Assert.That(state).Contains("internal ColonistStatsSnapshot Snapshot(Pawn pawn)");
        await Assert.That(state).Contains("internal ColonistSkillPresentation PresentationFor(");
        await Assert.That(state).Contains("internal void InvalidateLanguageCaches()");
    }

    private static string Source(string file)
    {
        string path = Path.Combine(RepoRoot(), "src", "WorkRoles", "UI", file);
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
