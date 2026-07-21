namespace WorkRoles.Core.Tests;

public class RoleCapabilityPresentationArchitectureTests
{
    [Test]
    public async Task CapabilityIsCapturedOnceAndRenderingUsesOnlyTheSnapshot()
    {
        string state = Source("UI", "ColonistRoleCapabilityState.cs");
        string chips = Source("UI", "RoleChipUI.cs");
        string view = Source("UI", "ColonistsTabView.cs");

        await Assert.That(state).Contains("role.Coverage()")
            .Because("the snapshot must classify every concrete job covered by the role");
        await Assert.That(state).Contains("pawn.WorkTypeIsDisabled(def.workType)");
        await Assert.That(state).Contains("pawn.WorkTagIsDisabled(def.workTags)");
        await Assert.That(state).Contains("IsRangedWeapon")
            .Because("hunting is unavailable while the colonist has no ranged weapon");
        await Assert.That(state).Contains("def.workType == WorkTypeDefOf.Hunting")
            .Because("a missing ranged weapon must not suppress non-hunting jobs");
        await Assert.That(state).Contains("role.blocker")
            .Because("blocker roles intentionally veto work and must not warn about that work");
        await Assert.That(state).Contains("Dictionary<(Pawn pawn, int roleId), RoleCapabilityPresentation>")
            .Because("layout and repaint passes must reuse immutable presentation data");

        string[] liveQueries =
        {
            "WorkTypeIsDisabled",
            "WorkTagIsDisabled",
            ".equipment",
            "role.Coverage()",
        };
        foreach (string query in liveQueries)
        {
            await Assert.That(chips).DoesNotContain(query);
            await Assert.That(Method(view, "internal void DrawChipStrip(")).DoesNotContain(query);
        }

        await Assert.That(view).Contains("RoleCapabilityPresentation capability");
        string draw = Method(view, "public void Draw(");
        await Assert.That(draw).Contains("Event.current.type == EventType.Layout");
        await Assert.That(draw).Contains("ObserveSignalChanges(pawns)")
            .Because("mutable capability inputs are observed once per UI frame, not per GUI pass");
        await Assert.That(Method(view, "internal void DrawChipStrip("))
            .Contains("warningSeverity: capability.WarningSeverity");
        await Assert.That(Method(view, "private TipModel BuildRoleTip("))
            .Contains("capability.Tooltip")
            .Because("the capability sentence lives inside the chip's single role tooltip");
        await Assert.That(chips).DoesNotContain("TooltipHandler")
            .Because("a chip has exactly one tooltip; markers must not register their own");
    }

    [Test]
    public async Task PartialAndFullWarningsUseTheRequestedColonistBarIcons()
    {
        string textures = Source("UI", "WorkRolesTex.cs");
        string chips = Source("UI", "RoleChipUI.cs");

        await Assert.That(textures)
            .Contains("UI/Icons/ColonistBar/MentalStateNonAggro");
        await Assert.That(textures)
            .Contains("UI/Icons/ColonistBar/MentalStateAggro");
        await Assert.That(chips).Contains("RoleAssignmentWarningSeverity.Caution");
        await Assert.That(chips).Contains("WorkRolesTex.RoleCapabilityPartial");
        await Assert.That(chips).Contains("WorkRolesTex.RoleCapabilityAll");
    }

    [Test]
    public async Task AwfulRoleWarningIsResolvedFromSnapshotIntoTheExistingChipTooltip()
    {
        string state = Source("UI", "ColonistRoleCapabilityState.cs");
        string chips = Source("UI", "RoleChipUI.cs");
        string view = Source("UI", "ColonistsTabView.cs");

        await Assert.That(state).Contains("PawnSignalSnapshot signalSnapshot");
        await Assert.That(state).Contains("signalSnapshot.WorkTypeBuckets.ForWorkType(");
        await Assert.That(state).Contains("SignalBucket.Awful");
        await Assert.That(state).Contains("RoleAssignmentWarningSummary.From(");
        await Assert.That(view).Contains("SignalSnapshotFor(pawn)")
            .Because("the presentation must consume the shared immutable pawn snapshot");
        await Assert.That(Method(view, "private TipModel BuildRoleTip("))
            .Contains("capability.Tooltip")
            .Because("the Awful explanation belongs inside the chip's existing tooltip");
        await Assert.That(chips).Contains("RoleAssignmentWarningSeverity warningSeverity");

        foreach (string liveQuery in new[]
                 {
                     "MoreThanCapable",
                     "IsBadWork",
                     "WorkTypeBuckets",
                 })
            await Assert.That(chips).DoesNotContain(liveQuery);
        await Assert.That(chips).DoesNotContain("TooltipHandler");
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

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "WorkRoles" }
            .Concat(parts).ToArray()));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
